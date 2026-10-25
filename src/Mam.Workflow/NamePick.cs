namespace Mam.Workflow;

/// <summary>A name option from the brand designer, with the logo made for it.</summary>
public sealed record NameOption(string Name, string Rationale, string? LogoUrl);

/// <summary>The brand designer's name options, as extracted by the shortlist step.</summary>
public sealed record NameShortlist(List<NameOption> Options, string Recommended);

/// <summary>
/// The request the workflow sends out through the <c>pick-name</c> request port: "which name do we use?".
/// The board shows the options as cards and answers with a <see cref="NamePickResponse"/>.
/// </summary>
public sealed record NamePickRequest(List<NameOption> Options, string Recommended);

/// <summary>The board's answer to a <see cref="NamePickRequest"/>: the picked name.</summary>
public sealed record NamePickResponse(string Name);

/// <summary>
/// A pick typed in the chat (DevUI): a number or (part of) a name. The intake forwards the user's next chat message
/// as a reply while a pick is open.
/// </summary>
/// <remarks>
/// The OpenAI Responses hosting doesn't surface workflow requests as approvals (yet), so DevUI can't answer the
/// request port. Typing the answer as the next message works in any chat client.
/// </remarks>
public sealed record NameReply(string Text);

/// <summary>Workflow state shared between the intake and the shortlist step.</summary>
internal static class NamePickState
{
    public const string Scope = "name-pick";
    public const string AwaitingKey = "awaiting";
}
