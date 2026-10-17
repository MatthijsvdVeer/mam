using Mam.Agents;
using Microsoft.Agents.AI.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddFoundryProjectClient("project");

// The agent definition (instructions + tools) lives in Mam.Agents so the workflow service can reuse it.
builder.AddAIAgent(AgentNames.Researcher, (sp, name) => RestaurantAgents.CreateResearcher(sp, name));

builder.AddOpenAIResponses();
builder.AddOpenAIConversations();

var app = builder.Build();

// DevUI (via the Aspire aggregator) talks to the agent through these OpenAI-compatible endpoints.
app.MapOpenAIResponses();
app.MapOpenAIConversations();

app.MapDefaultEndpoints();

app.Run();
