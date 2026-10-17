using Azure.AI.Projects;
using Mam.BrandDesign;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddFoundryProjectClient("project");

var model = builder.Configuration["MODEL_DEPLOYMENT"] ?? "gpt-4.1";

builder.AddAIAgent("brand-designer", (sp, name) => sp.GetRequiredService<AIProjectClient>()
    .AsAIAgent(
        model: model,
        name: name,
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
            Use the GenerateImage tool to create a logo and a mood-board image, and include the returned image URLs as Markdown images.
            """,
        tools: [AIFunctionFactory.Create(BrandTools.GenerateImage)],
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
