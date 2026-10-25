using Mam.Agents.BrandDesign;
using Mam.Agents.BrandDesign.Tools;
using Mam.Agents.MenuWriter;
using Mam.Agents.Presenter;
using Mam.Agents.Researcher;
using Mam.Workflow;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddFoundryProjectClient("project");

// The workflow hosts its own, in-process instances of the agent definitions from the agent projects.
builder.AddAIAgent(ResearcherAgent.Name, (sp, name) => ResearcherAgent.Create(sp, name));
builder.AddAIAgent(BrandDesignerAgent.Name, (sp, name) => BrandDesignerAgent.Create(sp, name));
builder.AddAIAgent(MenuWriterAgent.Name, (sp, name) => MenuWriterAgent.Create(sp, name));
builder.AddAIAgent(PresenterAgent.Name, (sp, name) => PresenterAgent.Create(sp, name));

// The graph lives in RestaurantWorkflow.Build, so the board can build its own copy per run.
builder.AddWorkflow(RestaurantWorkflow.Name, (sp, name) => RestaurantWorkflow.Build(sp, name))
    .AddAsAIAgent(includeWorkflowOutputsInResponse: true)
    // Keeps the workflow session between chat turns, so the reply with the picked name resumes the paused run.
    // No isolation: this local demo has no signed-in users to partition the sessions by.
    .WithInMemorySessionStore(withIsolation: false);

builder.AddOpenAIResponses();
builder.AddOpenAIConversations();
builder.Services.AddSingleton<BoardRuns>();

var app = builder.Build();

// DevUI (via the Aspire aggregator) talks to the workflow through these OpenAI-compatible endpoints.
app.MapOpenAIResponses();
app.MapOpenAIConversations();

// Serves the images created by the brand designer's GenerateImage tool.
app.MapGeneratedImages();

// The live workflow board for demos: /board.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapBoard();

app.MapDefaultEndpoints();

app.Run();
