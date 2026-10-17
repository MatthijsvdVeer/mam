using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Mam.Workflow;

/// <summary>
/// The workflow's entry point (no LLM). It speaks the chat protocol, so the workflow can be used as a chat agent:
/// the user's idea arrives as chat messages, followed by a turn token that says "go".
/// </summary>
/// <remarks>
/// It passes the idea on to the researcher without streaming. Only the presenter streams into the chat, because
/// streaming several agents would mix their text into a single chat message.
/// </remarks>
internal sealed class IntakeExecutor() : ChatProtocolExecutor("intake", new ChatProtocolExecutorOptions
{
    StringMessageChatRole = ChatRole.User,
    AutoSendTurnToken = false,
})
{
    protected override async ValueTask TakeTurnAsync(List<ChatMessage> messages, IWorkflowContext context, bool? emitEvents, CancellationToken cancellationToken = default)
    {
        await context.SendMessageAsync(messages, cancellationToken);
        await context.SendMessageAsync(new TurnToken(emitEvents: false), cancellationToken);
    }
}
