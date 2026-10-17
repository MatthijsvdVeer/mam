using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Mam.Agents.Tools;

/// <summary>
/// Stores generated images on disk and serves them over HTTP, so agents can link to them.
/// </summary>
public static class GeneratedImages
{
    private const string Route = "/images";

    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "mam-images");

    public static async Task<string> SaveAsync(BinaryData bytes, string kind, string baseUrl, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Folder);

        var slug = new string([.. kind.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')]).Trim('-');
        var fileName = $"{(slug.Length > 0 ? slug : "image")}-{Guid.NewGuid():N}.png";
        await File.WriteAllBytesAsync(Path.Combine(Folder, fileName), bytes.ToArray(), cancellationToken);

        return $"{baseUrl}{Route}/{fileName}";
    }

    /// <summary>Serves the generated images at <c>/images/{file}</c>.</summary>
    public static IEndpointRouteBuilder MapGeneratedImages(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet($"{Route}/{{file}}", (string file) =>
        {
            var path = Path.Combine(Folder, Path.GetFileName(file));
            return File.Exists(path) ? Results.File(path, "image/png") : Results.NotFound();
        });

        return endpoints;
    }
}
