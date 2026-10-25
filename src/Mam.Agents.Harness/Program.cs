using Mam.Agents.BrandDesign.Tools;
using Mam.Agents.Harness;
using Microsoft.Agents.AI.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddFoundryProjectClient("project");

// The harness hosts its own, in-process instances of the three agents as background agents.
builder.AddAIAgent(RestaurantHarnessAgent.Name, (sp, name) => RestaurantHarnessAgent.Create(sp, name));

builder.AddOpenAIResponses();
builder.AddOpenAIConversations();

var app = builder.Build();

// DevUI (via the Aspire aggregator) talks to the harness through these OpenAI-compatible endpoints.
app.MapOpenAIResponses();
app.MapOpenAIConversations();

// Serves the images created by the brand designer's GenerateImage tool.
app.MapGeneratedImages();

app.MapDefaultEndpoints();

app.Run();
