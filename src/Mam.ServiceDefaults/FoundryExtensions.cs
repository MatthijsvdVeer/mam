using System.Data.Common;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

public static class FoundryExtensions
{
    /// <summary>
    /// Registers an <see cref="AIProjectClient"/> for the Foundry project that Aspire injects via
    /// <c>ConnectionStrings:{connectionName}</c> (format: <c>Endpoint=https://{account}.services.ai.azure.com/api/projects/{project}</c>).
    /// </summary>
    public static TBuilder AddFoundryProjectClient<TBuilder>(this TBuilder builder, string connectionName)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSingleton(_ =>
        {
            var connectionString = builder.Configuration.GetConnectionString(connectionName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{connectionName}' is missing. Run the app through the Aspire AppHost or set ConnectionStrings:{connectionName}.");

            var csb = new DbConnectionStringBuilder { ConnectionString = connectionString };
            var endpoint = csb.TryGetValue("Endpoint", out var value) ? value.ToString()! : connectionString;

            // The Aspire endpoint may carry an api-version query; AIProjectClient manages that itself.
            var projectEndpoint = new UriBuilder(endpoint) { Query = string.Empty }.Uri;

            // DefaultAzureCredential is convenient for demos; prefer a specific credential (e.g. ManagedIdentityCredential) in production.
            return new AIProjectClient(projectEndpoint, new DefaultAzureCredential());
        });

        return builder;
    }
}
