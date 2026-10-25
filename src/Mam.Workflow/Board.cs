using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mam.Agents.BrandDesign;
using Mam.Agents.MenuWriter;
using Mam.Agents.Presenter;
using Mam.Agents.Researcher;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Mam.Workflow;

/// <summary>
/// The backend of the live workflow board (<c>/board</c>): it runs the restaurant workflow in-process and streams
/// its events to the browser as Server-Sent Events.
/// </summary>
public static partial class Board
{
    private static readonly HashSet<string> AgentIds =
        [ResearcherAgent.Name, BrandDesignerAgent.Name, MenuWriterAgent.Name, PresenterAgent.Name];

    public static IEndpointRouteBuilder MapBoard(this IEndpointRouteBuilder endpoints)
    {
        // /board/ itself is served by the static files (wwwroot/board/index.html).
        var api = endpoints.MapGroup("/board/api");

        // The graph is read from the workflow definition itself, so the board always matches the code.
        api.MapGet("/graph", (IServiceProvider services) =>
        {
            var workflow = RestaurantWorkflow.Build(services);
            var ports = workflow.ReflectPorts().Keys.ToHashSet();
            var nodes = workflow.ReflectExecutors().Keys.Select(NodeId).Select(id => new
            {
                id,
                kind = ports.Contains(id) ? "human" : AgentIds.Contains(id) ? "agent" : "code",
            });
            var edges = workflow.ReflectEdges().Values
                .SelectMany(set => set)
                .SelectMany(edge => edge.Connection.SourceIds.SelectMany(source => edge.Connection.SinkIds.Select(sink => new
                {
                    source = NodeId(source),
                    sink = NodeId(sink),
                    kind = edge.Kind.ToString(),
                })))
                .Distinct()
                // The chat-only shortcut (the pick typed in DevUI) would cross the whole graph; the board never uses it.
                .Where(edge => !(edge.source == "intake" && edge.sink == "shortlist"));

            return Results.Ok(new { start = NodeId(workflow.StartExecutorId), nodes, edges });
        });

        api.MapPost("/runs", (StartRunRequest request, BoardRuns runs) =>
            string.IsNullOrWhiteSpace(request.Idea)
                ? Results.BadRequest("An idea is required.")
                : Results.Ok(new { id = runs.Start(request.Idea.Trim()).Id }));

        api.MapGet("/runs/{id}/events", (string id, BoardRuns runs, CancellationToken cancellationToken) =>
            runs.TryGet(id) is { } run
                ? TypedResults.ServerSentEvents(run.ReadAsync(cancellationToken))
                : Results.NotFound());

        api.MapPost("/runs/{id}/choice", async (string id, ChoiceRequest request, BoardRuns runs) =>
            runs.TryGet(id) is { } run && await run.PickNameAsync(request.Name)
                ? Results.Ok()
                : Results.Conflict("This run isn't waiting for a name."));

        return endpoints;
    }

    /// <summary>
    /// Agent executors get a unique id per workflow (e.g. <c>brand_designer_3f2a...</c>). The board uses the agent
    /// name (<c>brand-designer</c>), which is the same for every run.
    /// </summary>
    public static string NodeId(string executorId)
    {
        var match = AgentExecutorId().Match(executorId);
        return match.Success ? match.Groups[1].Value.Replace('_', '-') : executorId;
    }

    [GeneratedRegex("^(.+)_[0-9a-f]{32}$")]
    private static partial Regex AgentExecutorId();

    public sealed record StartRunRequest(string Idea);

    public sealed record ChoiceRequest(string Name);
}

/// <summary>An event for the board. <see cref="At"/> is the number of milliseconds since the run started.</summary>
public sealed record BoardEvent(string Type, double At, string? Executor = null, object? Data = null);

/// <summary>Keeps the board's runs in memory (it's a demo).</summary>
public sealed class BoardRuns(IServiceProvider services, ILogger<BoardRuns> logger)
{
    private readonly ConcurrentDictionary<string, BoardRun> _runs = new();

    public BoardRun Start(string idea)
    {
        var run = new BoardRun(Guid.NewGuid().ToString("N")[..12], idea, logger);
        this._runs[run.Id] = run;

        // A fresh workflow per run: executors keep state, so runs can't share a workflow instance.
        var workflow = RestaurantWorkflow.Build(services, streamAllAgents: true);
        _ = Task.Run(() => run.ExecuteAsync(workflow));
        return run;
    }

    public BoardRun? TryGet(string id) => this._runs.GetValueOrDefault(id);
}

/// <summary>One workflow run on the board: runs the workflow and records its events for any number of viewers.</summary>
public sealed class BoardRun(string id, string idea, ILogger logger)
{
    private readonly List<BoardEvent> _events = [];
    private readonly Lock _lock = new();
    private readonly long _started = Stopwatch.GetTimestamp();
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _finished;
    private StreamingRun? _run;
    private ExternalRequest? _pendingNamePick;

    public string Id { get; } = id;

    public async Task ExecuteAsync(Microsoft.Agents.AI.Workflows.Workflow workflow)
    {
        this.Append("run-started", data: new { idea });
        try
        {
            await using var run = await InProcessExecution.RunStreamingAsync(workflow, new List<ChatMessage> { new(ChatRole.User, idea) }, this.Id);
            this._run = run;
            await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

            // Blocks (rather than ends) while the workflow waits for the name pick.
            await foreach (var evt in run.WatchStreamAsync(blockOnPendingRequest: true))
            {
                this.Map(evt);
                if (evt is WorkflowErrorEvent)
                {
                    break;
                }
            }

            this.Append("run-completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Board run {RunId} failed", this.Id);
            this.Append("run-failed", data: new { error = ex.Message });
        }
        finally
        {
            lock (this._lock)
            {
                this._finished = true;
                this._changed.TrySetResult();
            }
        }
    }

    public async Task<bool> PickNameAsync(string name)
    {
        var request = Interlocked.Exchange(ref this._pendingNamePick, null);
        if (request is null || this._run is null || string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        this.Append("name-picked", RestaurantWorkflow.PickNamePortId, new { name });
        await this._run.SendResponseAsync(request.CreateResponse(new NamePickResponse(name)));
        return true;
    }

    /// <summary>Replays everything that happened so far, then follows the run live.</summary>
    public async IAsyncEnumerable<BoardEvent> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var next = 0;

        // Tells a viewer that joins late how long the run has been going.
        yield return new BoardEvent("clock", Stopwatch.GetElapsedTime(this._started).TotalMilliseconds);

        while (!cancellationToken.IsCancellationRequested)
        {
            BoardEvent[] batch;
            Task changed;
            bool finished;
            lock (this._lock)
            {
                batch = [.. this._events.Skip(next)];
                next = this._events.Count;
                changed = this._changed.Task;
                finished = this._finished;
            }

            foreach (var evt in batch)
            {
                yield return evt;
            }

            if (finished)
            {
                yield break;
            }

            try
            {
                await changed.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
        }
    }

    private void Map(WorkflowEvent evt)
    {
        switch (evt)
        {
            case SuperStepStartedEvent step:
                this.Append("step", data: new { step = step.StepNumber, senders = step.StartInfo?.SendingExecutors ?? [] });
                break;

            case ExecutorInvokedEvent invoked:
                this.Append("executor-invoked", invoked.ExecutorId);
                break;

            case ExecutorCompletedEvent completed:
                this.Append("executor-completed", completed.ExecutorId);
                break;

            case ExecutorFailedEvent failed:
                this.Append("executor-failed", failed.ExecutorId, new { error = failed.Data?.Message });
                break;

            case WorkflowErrorEvent error:
                this.Append("run-failed", data: new { error = (error.Data as Exception)?.Message ?? error.Data?.ToString() });
                break;

            case AgentResponseUpdateEvent update:
                foreach (var content in update.Update.Contents)
                {
                    switch (content)
                    {
                        case TextContent { Text.Length: > 0 } text:
                            this.Append("text", update.ExecutorId, new { text = text.Text });
                            break;
                        case FunctionCallContent call:
                            this.Append("tool-call", update.ExecutorId, new { callId = call.CallId, name = call.Name, arguments = call.Arguments });
                            break;
                        case FunctionResultContent result:
                            this.Append("tool-result", update.ExecutorId, new { callId = result.CallId, result = ToJson(result.Result) });
                            break;
                    }
                }

                break;

            case RequestInfoEvent request when request.Request.TryGetDataAs<NamePickRequest>(out var pick):
                this._pendingNamePick = request.Request;
                this.Append("pick-name", request.Request.PortInfo.PortId, new { options = pick.Options, recommended = pick.Recommended });
                break;

            case WorkflowOutputEvent output:
                this.Append("output", output.ExecutorId, new { text = TextOf(output.Data) });
                break;
        }
    }

    private void Append(string type, string? executor = null, object? data = null)
    {
        lock (this._lock)
        {
            this._events.Add(new BoardEvent(type, Stopwatch.GetElapsedTime(this._started).TotalMilliseconds, executor is null ? null : Board.NodeId(executor), data));
            var changed = this._changed;
            this._changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            changed.TrySetResult();
        }
    }

    private static JsonElement? ToJson(object? value)
    {
        try
        {
            return value switch
            {
                null => null,
                JsonElement element => element,
                _ => JsonSerializer.SerializeToElement(value, value.GetType(), JsonSerializerOptions.Web),
            };
        }
        catch (Exception)
        {
            return JsonSerializer.SerializeToElement(value!.ToString());
        }
    }

    private static string? TextOf(object? data) => data switch
    {
        AgentResponse response => response.Text,
        ChatMessage message => message.Text,
        IEnumerable<ChatMessage> messages => string.Join("\n\n", messages.Select(m => m.Text)),
        string text => text,
        _ => data?.ToString(),
    };
}
