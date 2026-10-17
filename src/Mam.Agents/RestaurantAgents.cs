using Azure.AI.Projects;
using Mam.Agents.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Mam.Agents;

/// <summary>
/// Definitions of the restaurant agents, shared by the standalone agent services and the workflow service.
/// </summary>
public static class RestaurantAgents
{
    public static AIAgent CreateResearcher(IServiceProvider services, string name = AgentNames.Researcher) => Create(
        services,
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

    public static AIAgent CreateMenuWriter(IServiceProvider services, string name = AgentNames.MenuWriter) => Create(
        services,
        name,
        description: "Writes a complete restaurant menu that fits a concept and brand vibe.",
        instructions: """
            You are the MenuWriter for a team that turns a single idea into a complete restaurant concept.
            Given a restaurant concept (and optionally research notes and a brand vibe) you write a menu that:
            - Has clear sections (e.g. Snacks, Starters, Mains, Sides, Desserts, Drinks), 3-6 items each.
            - Gives every dish an evocative name, a one-line description with key ingredients, and a price in EUR.
            - Marks dietary info with (V) vegetarian, (VG) vegan and (GF) gluten-free.
            - Matches the tone of voice of the brand and includes one or two signature dishes.
            If research notes are present in the conversation, build on them and weave in the opportunities they identify.
            Format the menu as Markdown. When the user is happy with the menu, call the PrintMenu tool.
            """,
        tools: [AIFunctionFactory.Create(MenuTools.PrintMenu)]);

    public static AIAgent CreateBrandDesigner(IServiceProvider services, string name = AgentNames.BrandDesigner) => Create(
        services,
        name,
        description: "Turns a restaurant idea into a visual brand and overall vibe.",
        instructions: """
            You are the BrandDesign agent for a team that turns a single idea into a complete restaurant concept.
            Given an idea, you create a brand identity containing:
            - Three name options with a one-line rationale each, and your recommended pick.
            - A tagline and a short brand story (max 80 words).
            - The vibe: interior, music, service style and tone of voice, in a few vivid bullets.
            - A colour palette of 4-5 colours with names and hex codes.
            - Typography suggestions (heading and body font).
            - A logo concept.
            If research notes are present in the conversation, ground the brand in them.
            Use the GenerateImage tool to create exactly three images, calling it for all three at once:
            a logo (kind 'logo'), a mood board (kind 'mood-board') and a photorealistic picture of the proposed
            dining room (kind 'interior', style 'photorealistic interior photography') that matches the vibe and palette.
            Include the returned image URLs as Markdown links, e.g. [Logo](url), [Mood board](url), [Interior](url).
            """,
        tools: [AIFunctionFactory.Create(new BrandTools(services).GenerateImageAsync, name: "GenerateImage")]);

    public static AIAgent CreatePresenter(IServiceProvider services, string name = AgentNames.Presenter) => Create(
        services,
        name,
        description: "Combines research, brand and menu into one polished restaurant concept.",
        instructions: """
            You are the Presenter for a team that turns a single idea into a complete restaurant concept.
            The conversation contains the original idea, the Researcher's findings, the BrandDesign agent's brand
            identity and the MenuWriter's menu. Combine them into one polished concept pitch in Markdown:
            # <Restaurant name> - <tagline>
            - **The idea**: two or three sentences that sell the concept.
            - **Why it works**: the most compelling research insights (keep the source URLs).
            - **The brand**: story, vibe, colour palette and typography, with Markdown links to the logo, mood-board and interior images.
            - **The menu**: the full menu, with the signature dishes highlighted.
            - **Next steps**: three concrete next steps to open the restaurant.
            If the brand and menu disagree (e.g. on the name), pick the brand's recommended name and stay consistent.
            Do not invent new facts or sources.
            """,
        tools: []);

    private static AIAgent Create(IServiceProvider services, string name, string description, string instructions, IList<AITool> tools)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var environment = services.GetRequiredService<IHostEnvironment>();
        var model = configuration["MODEL_DEPLOYMENT"] ?? "gpt-4.1";

        return services.GetRequiredService<AIProjectClient>()
            .AsAIAgent(
                new ChatClientAgentOptions
                {
                    Name = name,
                    Description = description,
                    ChatOptions = new ChatOptions { ModelId = model, Instructions = instructions, Tools = tools },
                    // Run multiple tool calls from one model turn in parallel (e.g. the logo, mood board and interior).
                    AllowConcurrentInvocation = true,
                },
                services: services)
            .AsBuilder()
            .UseOpenTelemetry(sourceName: environment.ApplicationName, configure: agent => agent.EnableSensitiveData = true)
            .Build(services);
    }
}
