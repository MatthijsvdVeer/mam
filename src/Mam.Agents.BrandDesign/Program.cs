using Mam.Agents.BrandDesign;
using Mam.Agents.BrandDesign.Tools;
using Microsoft.Agents.AI.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddFoundryProjectClient("project");

// The workflow service references this project to reuse the agent definition (instructions + tools).
builder.AddAIAgent(BrandDesignerAgent.Name, (sp, name) => BrandDesignerAgent.Create(sp, name));

builder.AddOpenAIResponses();
builder.AddOpenAIConversations();

var app = builder.Build();

// DevUI (via the Aspire aggregator) talks to the agent through these OpenAI-compatible endpoints.
app.MapOpenAIResponses();
app.MapOpenAIConversations();

// Serves the images created by the GenerateImage tool.
app.MapGeneratedImages();

app.MapDefaultEndpoints();

app.Run();
