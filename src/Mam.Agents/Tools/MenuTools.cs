using System.ComponentModel;

namespace Mam.Agents.Tools;

/// <summary>
/// Stubbed menu tools. They return canned data so the demo runs without external services.
/// </summary>
public static class MenuTools
{
    [Description("Sends the final menu to the printer and returns a print job confirmation.")]
    public static PrintJob PrintMenu(
        [Description("The restaurant name to print as the menu title.")] string restaurantName,
        [Description("The full menu in Markdown.")] string menuMarkdown,
        [Description("Number of copies to print.")] int copies = 1)
    {
        // TODO: replace with real rendering (e.g. Markdown -> PDF) and a print/export service.
        return new PrintJob(
            JobId: $"menu-{Guid.NewGuid():N}"[..13],
            RestaurantName: restaurantName,
            Copies: copies,
            Characters: menuMarkdown.Length,
            Status: "Queued (stub) - no menu was actually printed.");
    }

    public sealed record PrintJob(string JobId, string RestaurantName, int Copies, int Characters, string Status);
}
