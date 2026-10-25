using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Mam.Workflow;

/// <summary>
/// The workflow's entry point (no LLM). It speaks the chat protocol, so the workflow can be used as a chat agent:
/// the user's idea arrives as chat messages, followed by a turn token that says "go".
/// </summary>
/// <remarks>
/// <para>
/// While the workflow waits for the name pick, the user's next chat message is the pick (for example "2"): intake
/// forwards it to the shortlist step instead of starting a new run.
/// </para>
/// <para>
/// In the chat (DevUI) it passes the idea on to the researcher without streaming. Only the presenter streams into
/// the chat, because streaming several agents would mix their text into a single chat message. The board streams
/// every agent, each into its own lane.
/// </para>
/// </remarks>
/// <param name="researcherId">Where a new idea goes.</param>
/// <param name="shortlistId">Where the name pick goes.</param>
/// <param name="streamNextStep">Whether the researcher should stream its output.</param>
internal sealed class IntakeExecutor(string researcherId, string shortlistId, bool streamNextStep) : ChatProtocolExecutor("intake", new ChatProtocolExecutorOptions
{
    StringMessageChatRole = ChatRole.User,
    AutoSendTurnToken = false,
})
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder) =>
        base.ConfigureProtocol(protocolBuilder).SendsMessage<NameReply>();

    protected override async ValueTask TakeTurnAsync(List<ChatMessage> messages, IWorkflowContext context, bool? emitEvents, CancellationToken cancellationToken = default)
    {
        // A turn token without text (a resume without a new message): nothing to do.
        var text = string.Join("\n", messages.Where(m => m.Role == ChatRole.User).Select(m => m.Text).Where(t => !string.IsNullOrWhiteSpace(t)));
        if (text.Length == 0)
        {
            return;
        }

        if (await context.ReadStateAsync<bool>(NamePickState.AwaitingKey, NamePickState.Scope, cancellationToken))
        {
            var reply = messages.Last(m => m.Role == ChatRole.User && !string.IsNullOrWhiteSpace(m.Text)).Text;
            await context.SendMessageAsync(new NameReply(reply), shortlistId, cancellationToken);
            return;
        }

        await context.SendMessageAsync(messages, researcherId, cancellationToken);
        await context.SendMessageAsync(new TurnToken(emitEvents: streamNextStep), researcherId, cancellationToken);
    }
}
