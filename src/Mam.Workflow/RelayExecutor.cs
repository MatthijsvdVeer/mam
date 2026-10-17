using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Mam.Workflow;

/// <summary>
/// A plain code executor (no LLM) that collects the conversation it receives, removes duplicates and passes it on
/// to the next step, together with the turn token that tells the next agent(s) to start.
/// </summary>
/// <remarks>
/// Used twice in the restaurant workflow:
/// <list type="bullet">
/// <item><c>brief</c>: hands the research to the brand designer and menu writer, which run in parallel.</item>
/// <item><c>gather</c>: sits behind the fan-in barrier, merges both parallel results and hands everything to the presenter.</item>
/// </list>
/// Only the presenter streams token-by-token into the chat; streaming several agents would mix their text into a
/// single chat message. The other agents post their full answer as a workflow event when they are done.
/// </remarks>
/// <param name="id">The executor id, shown in DevUI and in the traces.</param>
/// <param name="streamNextStep">Whether the next agent(s) should stream their output.</param>
[SendsMessage(typeof(List<ChatMessage>))]
[SendsMessage(typeof(TurnToken))]
internal sealed class RelayExecutor(string id, bool streamNextStep) : Executor<List<ChatMessage>>(id)
{
    private readonly List<ChatMessage> _messages = [];
    private readonly HashSet<string> _seen = [];

    public override ValueTask HandleAsync(List<ChatMessage> message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        foreach (var chatMessage in message)
        {
            // Pass on plain text only: the next agent needs the content, not the tool calls or raw model payloads.
            if (string.IsNullOrWhiteSpace(chatMessage.Text))
            {
                continue;
            }

            // Parallel agents each forward the shared context (idea + research), so keep only one copy.
            if (this._seen.Add($"{chatMessage.Role}|{chatMessage.AuthorName}|{chatMessage.Text}"))
            {
                this._messages.Add(new ChatMessage(chatMessage.Role, chatMessage.Text) { AuthorName = chatMessage.AuthorName });
            }
        }

        return ValueTask.CompletedTask;
    }

    protected override async ValueTask OnMessageDeliveryFinishedAsync(IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (this._messages.Count == 0)
        {
            return;
        }

        List<ChatMessage> conversation = [.. this._messages];
        this._messages.Clear();
        this._seen.Clear();

        await context.SendMessageAsync(conversation, cancellationToken);
        await context.SendMessageAsync(new TurnToken(emitEvents: streamNextStep), cancellationToken);
    }
}
