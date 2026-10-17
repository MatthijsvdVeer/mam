using Mam.Agents;
using Mam.Agents.Tools;
using Microsoft.Agents.AI.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddFoundryProjectClient("project");

// The agent definition (instructions + tools) lives in Mam.Agents so the workflow service can reuse it.
builder.AddAIAgent(AgentNames.BrandDesigner, (sp, name) => RestaurantAgents.CreateBrandDesigner(sp, name));

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
