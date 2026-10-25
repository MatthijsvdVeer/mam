using Mam.Agents.Researcher.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Mam.Agents.Researcher;

/// <summary>
/// The researcher agent, hosted by this service and reused by the workflow service.
/// </summary>
public static class ResearcherAgent
{
    /// <summary>The agent name. It must match the name declared for DevUI in the AppHost.</summary>
    public const string Name = "researcher";

    public static AIAgent Create(IServiceProvider services, string name = Name) => services.CreateFoundryAgent(
        name,
        description: "Researches a topic, such as a cuisine, a location or a restaurant trend, and reports cited findings.",
        instructions: """
            You are the Researcher for a team that turns a single idea into a complete restaurant concept.
            Given a topic or restaurant idea (a cuisine, a period in history, a neighbourhood, a trend, ...) you:
            1. Use the WebSearch tool to find relevant sources.
            2. Use the WebFetch tool to read the most promising pages.
            3. Summarise your findings as concise bullet points grouped by theme, citing the source URL for each claim.
            Finish with a short "Opportunities" section the brand and menu teams can build on.
            Never invent sources; only cite URLs returned by your tools.
            """,
        tools:
        [
            AIFunctionFactory.Create(ResearchTools.WebSearch),
            AIFunctionFactory.Create(ResearchTools.WebFetch),
        ]);
}
