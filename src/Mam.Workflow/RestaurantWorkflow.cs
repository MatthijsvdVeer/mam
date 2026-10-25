using Mam.Agents.BrandDesign;
using Mam.Agents.MenuWriter;
using Mam.Agents.Presenter;
using Mam.Agents.Researcher;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace Mam.Workflow;

/// <summary>
/// Builds the restaurant workflow. Used by the workflow-as-agent (DevUI) and by the board, which builds a fresh
/// instance per run.
/// </summary>
public static class RestaurantWorkflow
{
    public const string Name = "restaurant-workflow";

    public const string Description =
        "Turns a restaurant idea into a complete concept: research, then brand and menu in parallel, a human picks the name, then a final pitch.";

    /// <summary>The id of the request port where a human picks the restaurant name.</summary>
    public const string PickNamePortId = "pick-name";

    //  idea ─► intake ─► researcher ─► brief ─┬─► brand-designer ─┐
    //            │                            └─► menu-writer ────┴─► gather ─► shortlist ─► presenter ─► concept
    //            │                                                                 ▲   ▲ │
    //            └──── the pick, typed in the chat (DevUI) ────────────────────────┘   │ ▼
    //                                                                           pick-name (board)
    /// <param name="services">The service provider with the agents registered by name.</param>
    /// <param name="streamAllAgents">
    /// <c>true</c> to stream every agent's output (the board shows each agent in its own lane); <c>false</c> to only
    /// stream the presenter, because streaming several agents would mix their text into a single chat message.
    /// </param>
    public static Microsoft.Agents.AI.Workflows.Workflow Build(IServiceProvider services, string name = Name, bool streamAllAgents = false)
    {
        AIAgent Agent(string agentName) => services.GetRequiredKeyedService<AIAgent>(agentName);

        // Plain code executors (no LLM). Intake accepts the idea from the chat; brief and gather pass the conversation along.
        var brief = new RelayExecutor("brief", streamNextStep: streamAllAgents);
        var gather = new RelayExecutor("gather", streamNextStep: false);

        // Agents become workflow executors. Each one passes what it received plus its own answer on to the next step.
        var stepOptions = new AIAgentHostOptions { ForwardIncomingMessages = true, EmitAgentResponseEvents = true };
        var research = Agent(ResearcherAgent.Name).BindAsExecutor(stepOptions);
        var brand = Agent(BrandDesignerAgent.Name).BindAsExecutor(stepOptions);
        var menu = Agent(MenuWriterAgent.Name).BindAsExecutor(stepOptions);
        var present = Agent(PresenterAgent.Name).BindAsExecutor(new AIAgentHostOptions { ForwardIncomingMessages = false });

        // Human in the loop: the workflow pauses at the request port until someone picks a name.
        var pickName = RequestPort.Create<NamePickRequest, NamePickResponse>(PickNamePortId);
        var shortlist = new ShortlistExecutor(CreateNameExtractor(services), pickName.Id, present.Id);
        var intake = new IntakeExecutor(research.Id, shortlist.Id, streamNextStep: streamAllAgents);

        return new WorkflowBuilder(intake)
            .WithName(name)
            .WithDescription(Description)
            .AddEdge(intake, research)
            .AddEdge(intake, shortlist)
            .AddEdge(research, brief)
            .AddFanOutEdge(brief, [brand, menu])
            .AddFanInBarrierEdge([brand, menu], gather)
            .AddEdge(gather, shortlist)
            .AddEdge(shortlist, pickName)
            .AddEdge(pickName, shortlist)
            .AddEdge(shortlist, present)
            .WithOutputFrom(present)
            .Build();
    }

    private static AIAgent CreateNameExtractor(IServiceProvider services) => services.CreateFoundryAgent(
        "name-extractor",
        description: "Extracts the brand designer's name options from the conversation.",
        instructions: """
            The conversation contains a BrandDesign agent's answer with three restaurant name options, each with a
            rationale and a logo link. Extract every name option exactly as written, with its one-line rationale and
            its logo URL (null if there is none), and the name the brand designer recommends.
            """,
        tools: []);
}
