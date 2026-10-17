var builder = DistributedApplication.CreateBuilder(args);

// Existing Microsoft Foundry project endpoint, read from ConnectionStrings:project.
// Format: Endpoint=https://{account}.services.ai.azure.com/api/projects/{project}
// Using a plain connection string means Aspire never deploys anything to Azure.
var project = builder.AddConnectionString("project");

var modelDeployment = builder.AddParameter("modelDeployment")
    .WithDescription("The model deployment name used by all agents, e.g. gpt-4.1.");

// One service per agent. Resource names match the agent names registered with AddAIAgent(...)
// so the DevUI aggregator can route to them.
var researcher = builder.AddProject<Projects.Mam_Researcher>("researcher")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("MODEL_DEPLOYMENT", modelDeployment)
    .WithReference(project);

var menuWriter = builder.AddProject<Projects.Mam_MenuWriter>("menu-writer")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("MODEL_DEPLOYMENT", modelDeployment)
    .WithReference(project);

var brandDesigner = builder.AddProject<Projects.Mam_BrandDesign>("brand-designer")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("MODEL_DEPLOYMENT", modelDeployment)
    .WithReference(project);

// The workflow service runs its own in-process copies of the agents and orchestrates them:
// researcher -> (brand-designer + menu-writer in parallel) -> presenter.
var restaurantWorkflow = builder.AddProject<Projects.Mam_Workflow>("restaurant-workflow")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("MODEL_DEPLOYMENT", modelDeployment)
    .WithReference(project);

// DevUI aggregates all agents into a single web UI.
// The agent names must match AddAIAgent("<name>", ...) / AddWorkflow("<name>", ...) in each service's Program.cs.
builder.AddDevUI("devui")
    .WithAgentService(researcher, agents: [new("researcher")])
    .WithAgentService(menuWriter, agents: [new("menu-writer")])
    .WithAgentService(brandDesigner, agents: [new("brand-designer")])
    .WithAgentService(restaurantWorkflow, agents: [new("restaurant-workflow", "Turns an idea into a complete restaurant concept.")])
    .WaitFor(researcher)
    .WaitFor(menuWriter)
    .WaitFor(brandDesigner)
    .WaitFor(restaurantWorkflow);

builder.Build().Run();
