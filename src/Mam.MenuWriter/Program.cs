using Azure.AI.Projects;
using Mam.MenuWriter;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddFoundryProjectClient("project");

var model = builder.Configuration["MODEL_DEPLOYMENT"] ?? "gpt-4.1";

builder.AddAIAgent("menu-writer", (sp, name) => sp.GetRequiredService<AIProjectClient>()
    .AsAIAgent(
        model: model,
        name: name,
        description: "Writes a complete restaurant menu that fits a concept and brand vibe.",
        instructions: """
            You are the MenuWriter for a team that turns a single idea into a complete restaurant concept.
            Given a restaurant concept (and optionally research notes and a brand vibe) you write a menu that:
            - Has clear sections (e.g. Snacks, Starters, Mains, Sides, Desserts, Drinks), 3-6 items each.
            - Gives every dish an evocative name, a one-line description with key ingredients, and a price in EUR.
            - Marks dietary info with (V) vegetarian, (VG) vegan and (GF) gluten-free.
            - Matches the tone of voice of the brand and includes one or two signature dishes.
            Format the menu as Markdown. When the user is happy with the menu, call the PrintMenu tool.
            """,
        tools: [AIFunctionFactory.Create(MenuTools.PrintMenu)],
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
