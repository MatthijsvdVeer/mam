using Azure.AI.Projects;
using Mam.Agents.BrandDesign;
using Mam.Agents.MenuWriter;
using Mam.Agents.Researcher;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Mam.Agents.Harness;

/// <summary>
/// The restaurant harness: a <see cref="HarnessAgent"/> that plans the work with todos and delegates it to the
/// researcher, brand designer and menu writer, which it runs as background agents.
/// </summary>
/// <remarks>
/// Unlike the workflow, nothing is wired up front: the harness decides which agents to call, in which order and
/// what to ask them. Hosted for DevUI by this service and run full-screen by Mam.Agents.Harness.Console.
/// </remarks>
public static class RestaurantHarnessAgent
{
    /// <summary>The agent name. It must match the name declared for DevUI in the AppHost.</summary>
    public const string Name = "restaurant-harness";

    // gpt-4.1 limits. Together they turn on the harness's in-loop context compaction.
    public const int MaxContextWindowTokens = 1_047_576;
    public const int MaxOutputTokens = 32_768;

    private const string Instructions = """
        You are the Lead of a restaurant studio that turns ideas into restaurant concepts. You have a team of
        specialist background agents and you never do their work yourself:
        - researcher: researches a cuisine, location, period or trend and reports cited findings.
        - brand-designer: creates the brand (name, tagline, vibe, palette, typography) plus a logo, mood board and interior image.
        - menu-writer: writes a complete menu that fits the concept and brand.

        ## How you work
        - Decide which specialists the request actually needs. A full concept needs all three; "just a menu" only needs the menu-writer.
        - Track the work as todos and mark them complete as you go, so the audience can follow your progress.
        - Before you start a background task, tell the user in one short sentence which specialist you are calling and why.
        - Specialists do not see this conversation. Put everything they need in the task input: the idea, the user's
          wishes and, for brand and menu, the key research findings and opportunities.
        - Run independent tasks concurrently: for a full concept, research first, then start the brand-designer and
          the menu-writer together and wait for both.
        - After starting tasks, immediately call background_agents_wait_for_first_completion in the same turn and keep
          waiting until every task is done. Never end your turn while a task is still running; the user cannot see
          results that arrive after you stop.
        - If a result is weak or misses something, continue that task with specific feedback instead of starting over.
        - Keep plans short. In plan mode, ask at most two clarifying questions, each with suggested options.

        ## The final answer
        Combine the results into one polished pitch in Markdown:
        # <Restaurant name> - <tagline>
        - **The idea**: two or three sentences that sell the concept.
        - **Why it works**: the most compelling research insights, with their source URLs.
        - **The brand**: story, vibe, palette and typography, with Markdown links to the logo, mood board and interior images.
        - **The menu**: the full menu, with the signature dishes highlighted.
        - **Next steps**: three concrete next steps to open the restaurant.
        Use the brand designer's recommended name everywhere. Do not invent facts, sources or image URLs.
        """;

    public static AIAgent Create(IServiceProvider services, string name = Name)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var environment = services.GetRequiredService<IHostEnvironment>();
        var model = configuration["MODEL_DEPLOYMENT"] ?? "gpt-4.1";

        IChatClient chatClient = services.GetRequiredService<AIProjectClient>()
            .GetProjectOpenAIClient()
            .GetResponsesClient()
            .AsIChatClient(model);

        return chatClient.AsHarnessAgent(
            new HarnessAgentOptions
            {
                Name = name,
                Description = "Plans the work and delegates it to the researcher, brand designer and menu writer to turn an idea into a restaurant concept.",
                ChatOptions = new ChatOptions { Instructions = Instructions },
                MaxContextWindowTokens = MaxContextWindowTokens,
                MaxOutputTokens = MaxOutputTokens,

                // The team: the same agent definitions the workflow uses, run as concurrent background tasks.
                BackgroundAgents =
                [
                    ResearcherAgent.Create(services),
                    BrandDesignerAgent.Create(services),
                    MenuWriterAgent.Create(services),
                ],

                // The researcher does the web searches, so the lead doesn't need its own.
                DisableWebSearch = true,
                // No Agent Skills in this demo; the provider would scan the working directory for them.
                DisableAgentSkillsProvider = true,
                // Keep the plan files out of the repository.
                FileMemoryStore = new FileSystemAgentFileStore(Path.Combine(Path.GetTempPath(), "mam-harness-memory")),

                // Same trace source as the sub-agents (see CreateFoundryAgent), so a run is one trace in Aspire.
                OpenTelemetrySourceName = environment.ApplicationName,
            },
            services.GetService<ILoggerFactory>(),
            services);
    }
}
