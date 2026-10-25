using Mam.Agents.Researcher;
using Microsoft.Agents.AI.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddFoundryProjectClient("project");

// The workflow service references this project to reuse the agent definition (instructions + tools).
builder.AddAIAgent(ResearcherAgent.Name, (sp, name) => ResearcherAgent.Create(sp, name));

builder.AddOpenAIResponses();
builder.AddOpenAIConversations();

var app = builder.Build();

// DevUI (via the Aspire aggregator) talks to the agent through these OpenAI-compatible endpoints.
app.MapOpenAIResponses();
app.MapOpenAIConversations();

app.MapDefaultEndpoints();

app.Run();
