using Mam.Agents;
using Mam.Agents.Tools;
using Mam.Workflow;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddFoundryProjectClient("project");

// The workflow hosts its own, in-process instances of the shared agent definitions.
var researcher = builder.AddAIAgent(AgentNames.Researcher, (sp, name) => RestaurantAgents.CreateResearcher(sp, name));
var brandDesigner = builder.AddAIAgent(AgentNames.BrandDesigner, (sp, name) => RestaurantAgents.CreateBrandDesigner(sp, name));
var menuWriter = builder.AddAIAgent(AgentNames.MenuWriter, (sp, name) => RestaurantAgents.CreateMenuWriter(sp, name));
var presenter = builder.AddAIAgent(AgentNames.Presenter, (sp, name) => RestaurantAgents.CreatePresenter(sp, name));

//  idea ─► intake ─► researcher ─► brief ─┬─► brand-designer ─┐
//                                         └─► menu-writer ────┴─► gather ─► presenter ─► concept
builder.AddWorkflow("restaurant-workflow", (sp, name) =>
{
    AIAgent Agent(IHostedAgentBuilder agent) => sp.GetRequiredKeyedService<AIAgent>(agent.Name);

    // Plain code executors (no LLM). Intake accepts the idea from the chat; brief and gather pass the conversation along.
    // They decide whether the next agent streams: only the presenter streams into the chat, the other steps post
    // their full answer as a workflow event when done.
    var intake = new IntakeExecutor();
    var brief = new RelayExecutor("brief", streamNextStep: false);
    var gather = new RelayExecutor("gather", streamNextStep: true);

    // Agents become workflow executors. Each one passes what it received plus its own answer on to the next step.
    var stepOptions = new AIAgentHostOptions { ForwardIncomingMessages = true, EmitAgentResponseEvents = true };
    var research = Agent(researcher).BindAsExecutor(stepOptions);
    var brand = Agent(brandDesigner).BindAsExecutor(stepOptions);
    var menu = Agent(menuWriter).BindAsExecutor(stepOptions);

    var present = Agent(presenter).BindAsExecutor(new AIAgentHostOptions { ForwardIncomingMessages = false });

    return new WorkflowBuilder(intake)
        .WithName(name)
        .WithDescription("Turns a restaurant idea into a complete concept: research, then brand and menu in parallel, then a final pitch.")
        .AddEdge(intake, research)
        .AddEdge(research, brief)
        .AddFanOutEdge(brief, [brand, menu])
        .AddFanInBarrierEdge([brand, menu], gather)
        .AddEdge(gather, present)
        .WithOutputFrom(present)
        .Build();
}).AddAsAIAgent(includeWorkflowOutputsInResponse: true);

builder.AddOpenAIResponses();
builder.AddOpenAIConversations();

var app = builder.Build();

// DevUI (via the Aspire aggregator) talks to the workflow through these OpenAI-compatible endpoints.
app.MapOpenAIResponses();
app.MapOpenAIConversations();

// Serves the images created by the brand designer's GenerateImage tool.
app.MapGeneratedImages();

app.MapDefaultEndpoints();

app.Run();
