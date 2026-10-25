using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Mam.Workflow;

/// <summary>
/// Sits between the fan-in (<c>gather</c>) and the presenter. It pulls the brand designer's name options out of the
/// conversation, asks a human to pick one through the <c>pick-name</c> request port and hands the conversation plus
/// the picked name to the presenter.
/// </summary>
/// <remarks>
/// The workflow pauses while the request is open. Everything needed after the pause is kept in workflow state
/// (not in fields), so it survives the checkpoint the workflow-as-agent makes between two chat turns.
/// </remarks>
/// <param name="extractor">A small agent that turns the brand designer's answer into a <see cref="NameShortlist"/>.</param>
/// <param name="portId">The id of the request port that asks the human.</param>
/// <param name="presenterId">The id of the presenter executor.</param>
internal sealed partial class ShortlistExecutor(AIAgent extractor, string portId, string presenterId) : Executor("shortlist")
{
    private const string ConversationKey = "conversation";
    private const string OptionsKey = "options";
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder) => protocolBuilder
        .ConfigureRoutes(routes => routes
            .AddHandler<List<ChatMessage>>(this.ShortlistAsync)
            .AddHandler<NamePickResponse>((response, context, ct) => this.PickedAsync(response.Name, context, ct))
            .AddHandler<NameReply>((reply, context, ct) => this.PickedAsync(reply.Text, context, ct))
            .AddHandler<TurnToken>(IgnoreTurnTokenAsync))
        .SendsMessage<NamePickRequest>()
        .SendsMessage<List<ChatMessage>>()
        .SendsMessage<TurnToken>();

    private async ValueTask ShortlistAsync(List<ChatMessage> conversation, IWorkflowContext context, CancellationToken cancellationToken)
    {
        var response = await extractor.RunAsync<NameShortlist>(conversation, cancellationToken: cancellationToken);
        var options = WithRealLogos(response.Result.Options.Where(o => !string.IsNullOrWhiteSpace(o.Name)).Take(3).ToList(), conversation);
        if (options.Count == 0)
        {
            throw new InvalidOperationException("The brand designer didn't propose any names.");
        }

        var recommended = options.FirstOrDefault(o => o.Name.Equals(response.Result.Recommended, StringComparison.OrdinalIgnoreCase))?.Name
            ?? options[0].Name;

        await context.QueueStateUpdateAsync(ConversationKey, conversation.Select(StoredMessage.From).ToList(), cancellationToken: cancellationToken);
        await context.QueueStateUpdateAsync(OptionsKey, options, cancellationToken: cancellationToken);
        await context.QueueStateUpdateAsync(NamePickState.AwaitingKey, true, NamePickState.Scope, cancellationToken);

        await this.SayAsync(context, DescribeOptions(options, recommended), cancellationToken);
        await context.SendMessageAsync(new NamePickRequest(options, recommended), portId, cancellationToken);
    }

    private async ValueTask PickedAsync(string answer, IWorkflowContext context, CancellationToken cancellationToken)
    {
        // Only the first answer counts: the board and the chat can't both pick.
        if (!await context.ReadStateAsync<bool>(NamePickState.AwaitingKey, NamePickState.Scope, cancellationToken))
        {
            return;
        }

        var options = await context.ReadStateAsync<List<NameOption>>(OptionsKey, cancellationToken: cancellationToken) ?? [];
        if (Match(answer, options) is not { } picked)
        {
            await this.SayAsync(context, $"I couldn't match \"{answer.Trim()}\" to a name. Reply with 1 to {options.Count}, or the name itself.\n", cancellationToken);
            return;
        }

        // The pick is made: stop routing chat messages here.
        await context.QueueStateUpdateAsync(NamePickState.AwaitingKey, false, NamePickState.Scope, cancellationToken);
        await this.SayAsync(context, $"\n\n**{picked.Name}** it is! Handing over to the presenter...\n\n", cancellationToken);

        var stored = await context.ReadStateAsync<List<StoredMessage>>(ConversationKey, cancellationToken: cancellationToken) ?? [];
        List<ChatMessage> conversation = [.. stored.Select(m => m.ToChatMessage())];
        conversation.Add(new ChatMessage(ChatRole.User,
            $"The restaurant's name is **{picked.Name}**{(picked.LogoUrl is null ? "" : $" and its logo is {picked.LogoUrl}")}. Use this name and logo."));

        await context.SendMessageAsync(conversation, presenterId, cancellationToken);
        await context.SendMessageAsync(new TurnToken(emitEvents: true), presenterId, cancellationToken);
    }

    // "2", "#2", "Pintxo Afloat" or "afloat" all work.
    private static NameOption? Match(string answer, List<NameOption> options)
    {
        var text = answer.Trim().Trim('#', '.', '!', '"', '*').Trim();
        if (int.TryParse(text, out var number) && number >= 1 && number <= options.Count)
        {
            return options[number - 1];
        }

        return text.Length == 0
            ? null
            : options.FirstOrDefault(o => o.Name.Equals(text, StringComparison.OrdinalIgnoreCase))
                ?? options.FirstOrDefault(o => o.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
                ?? options.FirstOrDefault(o => text.Contains(o.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static ValueTask IgnoreTurnTokenAsync(TurnToken token, IWorkflowContext context, CancellationToken cancellationToken) => default;

    // Models sometimes write a placeholder ("Coming soon...") before the images exist. Replace anything that isn't a
    // generated logo with the logos that really are in the conversation, in the order they appear.
    private static List<NameOption> WithRealLogos(List<NameOption> options, List<ChatMessage> conversation)
    {
        var found = conversation
            .SelectMany(m => m.Contents)
            .Select(c => c switch
            {
                TextContent text => text.Text,
                FunctionResultContent result => result.Result?.ToString(),
                _ => null,
            })
            .SelectMany(text => LogoUrl().Matches(text ?? "").Select(m => m.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var spare = new Queue<string>(found.Where(url => !options.Any(o => string.Equals(o.LogoUrl, url, StringComparison.OrdinalIgnoreCase))));
        return [.. options.Select(o => o.LogoUrl is { } url && LogoUrl().IsMatch(url)
            ? o
            : o with { LogoUrl = spare.TryDequeue(out var real) ? real : null })];
    }

    [GeneratedRegex(@"(?:https?://[^\s()\[\]""'<>]+)?/images/logo-[\w-]+\.(?:png|jpe?g|webp)", RegexOptions.IgnoreCase)]
    private static partial Regex LogoUrl();

    // Shows up in the chat (DevUI) and in the shortlist lane on the board.
    private ValueTask SayAsync(IWorkflowContext context, string text, CancellationToken cancellationToken) =>
        context.AddEventAsync(new AgentResponseUpdateEvent(this.Id, new AgentResponseUpdate(ChatRole.Assistant, text)
        {
            AuthorName = this.Id,
            MessageId = $"{this.Id}-{Guid.NewGuid():N}",
        }), cancellationToken);

    private static string DescribeOptions(List<NameOption> options, string recommended)
    {
        var text = new StringBuilder("The brand designer proposes these names:\n\n");
        foreach (var (option, i) in options.Select((o, i) => (o, i)))
        {
            text.Append($"{i + 1}. **{option.Name}**{(option.Name == recommended ? " (recommended)" : "")} - {option.Rationale}");
            text.AppendLine(option.LogoUrl is null ? "" : $" - [Logo]({option.LogoUrl})");
        }

        return text.Append($"\nWhich one will it be? Pick a card on the board, or reply with 1 to {options.Count}.\n").ToString();
    }

    /// <summary>A plain-text chat message that can be stored in workflow state.</summary>
    internal sealed record StoredMessage(string Role, string? AuthorName, string Text)
    {
        public static StoredMessage From(ChatMessage message) => new(message.Role.Value, message.AuthorName, message.Text);

        public ChatMessage ToChatMessage() => new(new ChatRole(this.Role), this.Text) { AuthorName = this.AuthorName };
    }
}
