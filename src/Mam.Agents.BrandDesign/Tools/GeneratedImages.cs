using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Mam.Agents.BrandDesign.Tools;

/// <summary>
/// Stores generated images on disk and serves them over HTTP, so agents can link to them.
/// </summary>
public static class GeneratedImages
{
    private const string Route = "/images";

    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "mam-images");

    /// <summary>
    /// Saves the image and returns its URL: served over HTTP when <paramref name="baseUrl"/> is set, otherwise a
    /// <c>file://</c> URL (e.g. when the agent runs in the harness console app, which has no web server).
    /// </summary>
    public static async Task<string> SaveAsync(BinaryData bytes, string kind, string? baseUrl, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Folder);

        var slug = new string([.. kind.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')]).Trim('-');
        var fileName = $"{(slug.Length > 0 ? slug : "image")}-{Guid.NewGuid():N}.png";
        var path = Path.Combine(Folder, fileName);
        await File.WriteAllBytesAsync(path, bytes.ToArray(), cancellationToken);

        return baseUrl is null ? new Uri(path).AbsoluteUri : $"{baseUrl}{Route}/{fileName}";
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
