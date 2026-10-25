using Harness.Shared.Console;
using Harness.Shared.Console.ToolFormatters;
using Mam.Harness;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Runs the restaurant harness full-screen in the terminal, using the sample console from the Agent Framework
// Harness samples (see HarnessConsole/README.md). It shows the current mode (plan/execute), the todo list, every
// tool call (including the background tasks on the researcher, brand designer and menu writer) and token usage.
var builder = Host.CreateApplicationBuilder(args);

// The terminal is the UI, so no log lines on screen. Telemetry still goes to the Aspire dashboard when
// OTEL_EXPORTER_OTLP_ENDPOINT is set.
builder.Logging.ClearProviders();

UseAppHostSettings(builder.Configuration);

builder.AddServiceDefaults();
builder.AddFoundryProjectClient("project");

using var host = builder.Build();
await host.StartAsync();

var agent = RestaurantHarnessAgent.Create(host.Services);

await HarnessConsole.RunAgentAsync(
    agent,
    userPrompt: "Describe your restaurant idea, e.g. an homage to early medieval Utrecht.",
    new HarnessConsoleOptions
    {
        Observers = HarnessConsoleOptions.BuildObserversWithPlanning(
            agent,
            planModeName: "plan",
            executionModeName: "execute",
            maxContextWindowTokens: RestaurantHarnessAgent.MaxContextWindowTokens,
            maxOutputTokens: RestaurantHarnessAgent.MaxOutputTokens,
            toolFormatters: ToolCallFormatter.BuildDefaultToolFormatters()),
        CommandHandlers = HarnessConsoleOptions.BuildDefaultCommandHandlers(agent),
    });

await host.StopAsync();

// Reads the Foundry endpoint and deployments from the AppHost's appsettings.json (copied next to the executable),
// unless they're already set, e.g. through environment variables or user secrets.
static void UseAppHostSettings(ConfigurationManager configuration)
{
    var appHost = new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "apphost.appsettings.json"), optional: true)
        .Build();

    configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:project"] = configuration.GetConnectionString("project") ?? appHost.GetConnectionString("project"),
        ["MODEL_DEPLOYMENT"] = configuration["MODEL_DEPLOYMENT"] ?? appHost["Parameters:modelDeployment"],
        ["IMAGE_DEPLOYMENT"] = configuration["IMAGE_DEPLOYMENT"] ?? appHost["Parameters:imageDeployment"],
    });
}
