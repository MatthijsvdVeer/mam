var builder = DistributedApplication.CreateBuilder(args);

// Existing Microsoft Foundry project endpoint, read from ConnectionStrings:project.
// Format: Endpoint=https://{account}.services.ai.azure.com/api/projects/{project}
// Using a plain connection string means Aspire never deploys anything to Azure.
var project = builder.AddConnectionString("project");

var modelDeployment = builder.AddParameter("modelDeployment")
    .WithDescription("The model deployment name used by all agents, e.g. gpt-4.1.");

var imageDeployment = builder.AddParameter("imageDeployment")
    .WithDescription("The image model deployment used by the brand designer's GenerateImage tool, e.g. gpt-image-2.5-flare.");

// Feature toggle for the menu writer's PrintMenu tool. When false (the default) menus are only logged, not printed.
var printMenus = builder.AddParameter("printMenus")
    .WithDescription("Set to true to print menus on the label printer (MENU_PRINTER, default PM-241-BT). Trees are expensive.");

// One service per agent. Resource names match the agent names registered with AddAIAgent(...)
// so the DevUI aggregator can route to them.
var researcher = builder.AddProject<Projects.Mam_Agents_Researcher>("researcher")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("MODEL_DEPLOYMENT", modelDeployment)
    .WithReference(project);

var menuWriter = builder.AddProject<Projects.Mam_Agents_MenuWriter>("menu-writer")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("MODEL_DEPLOYMENT", modelDeployment)
    .WithEnvironment("PRINT_MENUS", printMenus)
    .WithReference(project);

var brandDesigner = builder.AddProject<Projects.Mam_Agents_BrandDesign>("brand-designer")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("MODEL_DEPLOYMENT", modelDeployment)
    .WithEnvironment("IMAGE_DEPLOYMENT", imageDeployment)
    .WithReference(project);

// The workflow service runs its own in-process copies of the agents and orchestrates them:
// researcher -> (brand-designer + menu-writer in parallel) -> you pick the name -> presenter.
// It also serves the live workflow board for demos at /board (linked in the Aspire dashboard).
var restaurantWorkflow = builder.AddProject<Projects.Mam_Workflow>("restaurant-workflow")
    .WithHttpHealthCheck("/health")
    .WithUrlForEndpoint("https", _ => new() { Url = "/board/", DisplayText = "Workflow board" })
    .WithEnvironment("MODEL_DEPLOYMENT", modelDeployment)
    .WithEnvironment("PRINT_MENUS", printMenus)
    .WithEnvironment("IMAGE_DEPLOYMENT", imageDeployment)
    .WithReference(project);

// The harness service hosts a single lead agent that plans, tracks todos and delegates to in-process copies
// of the three agents as background tasks. Its full-screen terminal version is Mam.Agents.Harness.Console.
var restaurantHarness = builder.AddProject<Projects.Mam_Agents_Harness>("restaurant-harness")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("MODEL_DEPLOYMENT", modelDeployment)
    .WithEnvironment("PRINT_MENUS", printMenus)
    .WithEnvironment("IMAGE_DEPLOYMENT", imageDeployment)
    .WithReference(project);

// DevUI aggregates all agents into a single web UI.
// The agent names must match AddAIAgent("<name>", ...) / AddWorkflow("<name>", ...) in each service's Program.cs.
builder.AddDevUI("devui")
    .WithAgentService(researcher, agents: [new("researcher")])
    .WithAgentService(menuWriter, agents: [new("menu-writer")])
    .WithAgentService(brandDesigner, agents: [new("brand-designer")])
    .WithAgentService(restaurantWorkflow, agents: [new("restaurant-workflow", "Turns an idea into a complete restaurant concept. Asks you to pick the name.")])
    .WithAgentService(restaurantHarness, agents: [new("restaurant-harness", "A lead agent that plans and delegates to the researcher, brand designer and menu writer.")])
    .WaitFor(researcher)
    .WaitFor(menuWriter)
    .WaitFor(brandDesigner)
    .WaitFor(restaurantWorkflow)
    .WaitFor(restaurantHarness);

builder.Build().Run();
