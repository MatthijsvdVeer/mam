using System.ClientModel.Primitives;
using System.ComponentModel;
using System.Data.Common;
using System.Diagnostics;
using Azure.Core;
using OpenAI;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI.Images;

// The Entra ID (AuthenticationPolicy) ImageClient constructor is marked experimental.
#pragma warning disable OPENAI001

namespace Mam.Agents.BrandDesign.Tools;

/// <summary>
/// Brand tools. <see cref="GenerateImageAsync"/> generates real images with an image model deployed in the same
/// Foundry project (IMAGE_DEPLOYMENT, default gpt-image-2.5-flare).
/// </summary>
public sealed class BrandTools(IServiceProvider services)
{
    [Description("Generates an image (e.g. a logo or mood board) from a prompt and returns its URL.")]
    public async Task<GeneratedImageResult> GenerateImageAsync(
        [Description("A detailed description of the image to generate.")] string prompt,
        [Description("The kind of image, e.g. 'logo', 'mood-board', 'interior', 'menu-cover'.")] string kind = "logo",
        [Description("Visual style, e.g. 'flat vector', 'watercolour', 'photorealistic'.")] string style = "flat vector",
        CancellationToken cancellationToken = default)
    {
        var client = this._imageClient.Value;
        var logger = services.GetRequiredService<ILogger<BrandTools>>();
        logger.LogInformation("Generating {Kind} image with {Deployment}", kind, client.Model);
        var started = Stopwatch.GetTimestamp();

        GeneratedImage image = await client.GenerateImageAsync(
            $"A {kind} for a restaurant, in a {style} style. {prompt}",
            new ImageGenerationOptions { Size = GeneratedImageSize.W1024xH1024 },
            cancellationToken);

        var url = await GeneratedImages.SaveAsync(image.ImageBytes, kind, BaseUrl(), cancellationToken);
        logger.LogInformation("Generated {Kind} image in {Elapsed}: {Url}", kind, Stopwatch.GetElapsedTime(started), url);
        return new GeneratedImageResult(url, kind, style, prompt);
    }

    private readonly Lazy<ImageClient> _imageClient = new(() => CreateImageClient(
        services.GetRequiredService<IConfiguration>(), services.GetRequiredService<TokenCredential>()));

    private static ImageClient CreateImageClient(IConfiguration configuration, TokenCredential credential)
    {
        var deployment = configuration["IMAGE_DEPLOYMENT"] ?? "gpt-image-2.5-flare";

        // The image model is deployed in the same Foundry account as the project:
        // https://{account}.services.ai.azure.com/api/projects/{project} -> https://{account}.services.ai.azure.com/openai/v1
        var connectionString = configuration.GetConnectionString("project")
            ?? throw new InvalidOperationException("Connection string 'project' is missing.");
        var csb = new DbConnectionStringBuilder { ConnectionString = connectionString };
        var projectEndpoint = new Uri(csb.TryGetValue("Endpoint", out var value) ? value.ToString()! : connectionString);
        var endpoint = new Uri(projectEndpoint, "/openai/v1");

        // Entra ID instead of an API key, with the same credential as the agents (see AddFoundryProjectClient).
        var authentication = new BearerTokenPolicy(credential, "https://cognitiveservices.azure.com/.default");
        return new ImageClient(deployment, authentication, new OpenAIClientOptions { Endpoint = endpoint });
    }

    // The images are served by the service that generated them (see GeneratedImages.MapGeneratedImages).
    // Without a web server (the harness console app), there's no base URL and the images are linked as files.
    private string? BaseUrl()
    {
        if (services.GetService<IServer>() is not { } server)
        {
            return null;
        }

        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses ?? [];
        var address = addresses.FirstOrDefault(a => a.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            ?? addresses.FirstOrDefault()
            ?? "http://localhost";

        var uri = new UriBuilder(address.Replace("://+", "://localhost").Replace("://*", "://localhost"));
        if (uri.Host is "[::]" or "0.0.0.0")
        {
            uri.Host = "localhost";
        }

        return uri.Uri.GetLeftPart(UriPartial.Authority);
    }

    public sealed record GeneratedImageResult(string Url, string Kind, string Style, string Prompt);
}
