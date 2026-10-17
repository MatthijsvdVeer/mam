using Azure.AI.Projects;
using Mam.Researcher;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddFoundryProjectClient("project");

var model = builder.Configuration["MODEL_DEPLOYMENT"] ?? "gpt-4.1";

builder.AddAIAgent("researcher", (sp, name) => sp.GetRequiredService<AIProjectClient>()
    .AsAIAgent(
        model: model,
        name: name,
        description: "Researches a topic, such as a cuisine, a location or a restaurant trend, and reports cited findings.",
        instructions: """
            You are the Researcher for a team that turns a single idea into a complete restaurant concept.
            Given a topic (a cuisine, a neighbourhood, a trend, competitors, ingredients, ...) you:
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
        ],
        services: sp)
    .AsBuilder()
    .UseOpenTelemetry(sourceName: builder.Environment.ApplicationName, configure: agent => agent.EnableSensitiveData = true)
    .Build(sp));

builder.AddOpenAIResponses();
builder.AddOpenAIConversations();

var app = builder.Build();

// DevUI (via the Aspire aggregator) talks to the agent through these OpenAI-compatible endpoints.
app.MapOpenAIResponses();
app.MapOpenAIConversations();

app.MapDefaultEndpoints();

app.Run();
