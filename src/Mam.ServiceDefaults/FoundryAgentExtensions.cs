using Azure.AI.Projects;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

public static class FoundryAgentExtensions
{
    /// <summary>
    /// Creates an agent backed by the Foundry project (see <see cref="FoundryExtensions.AddFoundryProjectClient"/>),
    /// using the MODEL_DEPLOYMENT model (default gpt-4.1) and wrapped with OpenTelemetry.
    /// </summary>
    public static AIAgent CreateFoundryProjectAgent(
        this IServiceProvider services, string name, string description, string instructions, IList<AITool> tools)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var environment = services.GetRequiredService<IHostEnvironment>();
        var model = configuration["MODEL_DEPLOYMENT"] ?? "gpt-4.1";

        return services.GetRequiredService<AIProjectClient>()
            .AsAIAgent(
                new ChatClientAgentOptions
                {
                    Name = name,
                    Description = description,
                    ChatOptions = new ChatOptions { ModelId = model, Instructions = instructions, Tools = tools },
                    // Run multiple tool calls from one model turn in parallel (e.g. the logo, mood board and interior).
                    AllowConcurrentInvocation = true,
                },
                services: services)
            .AsBuilder()
            .UseOpenTelemetry(sourceName: environment.ApplicationName, configure: agent => agent.EnableSensitiveData = true)
            .Build(services);
    }
}
