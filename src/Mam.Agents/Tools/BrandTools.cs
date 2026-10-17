using System.ComponentModel;

namespace Mam.Agents.Tools;

/// <summary>
/// Stubbed brand tools. They return canned data so the demo runs without external services.
/// </summary>
public static class BrandTools
{
    [Description("Generates an image (e.g. a logo or mood board) from a prompt and returns its URL.")]
    public static GeneratedImage GenerateImage(
        [Description("A detailed description of the image to generate.")] string prompt,
        [Description("The kind of image, e.g. 'logo', 'mood-board', 'interior', 'menu-cover'.")] string kind = "logo",
        [Description("Visual style, e.g. 'flat vector', 'watercolour', 'photorealistic'.")] string style = "flat vector")
    {
        // TODO: replace with a real image model (e.g. gpt-image-1 deployed in Foundry).
        var text = Uri.EscapeDataString($"{kind}: {style}");
        return new GeneratedImage(
            Url: $"https://placehold.co/1024x1024/png?text={text}",
            Kind: kind,
            Style: style,
            Prompt: prompt,
            Note: "Placeholder image (stub) - no image was actually generated.");
    }

    public sealed record GeneratedImage(string Url, string Kind, string Style, string Prompt, string Note);
}
