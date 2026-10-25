using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mam.Agents.MenuWriter.Tools;

/// <summary>
/// Menu tools. <see cref="PrintMenu"/> prints the menu on a label printer (MENU_PRINTER, default PM-241-BT),
/// but only when the PRINT_MENUS feature toggle is on. Otherwise the menu is only logged.
/// </summary>
public sealed class MenuTools(IServiceProvider services)
{
    public const string DefaultPrinter = "PM-241-BT";
    public const int MaxCopies = 5;

    [Description("Sends the final menu to the printer and returns a print job confirmation.")]
    public PrintJob PrintMenu(
        [Description("The restaurant name to print as the menu title.")] string restaurantName,
        [Description("The full menu in Markdown.")] string menuMarkdown,
        [Description("Number of copies to print.")] int copies = 1)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILogger<MenuTools>>();
        var jobId = $"menu-{Guid.NewGuid():N}"[..13];
        var printer = configuration["MENU_PRINTER"] is { Length: > 0 } name ? name : DefaultPrinter;
        copies = Math.Clamp(copies, 1, MaxCopies);

        PrintJob Job(bool printed, string status, LabelResult? label = null) => new(
            jobId, restaurantName, copies, menuMarkdown.Length, printed, printed ? printer : null,
            label?.FontSize, label?.Truncated ?? false, status);

        if (!bool.TryParse(configuration["PRINT_MENUS"], out var enabled) || !enabled)
        {
            logger.LogInformation(
                "Printing is disabled (PRINT_MENUS). Menu {JobId} for {RestaurantName} ({Copies} copies) was not printed:\n{Menu}",
                jobId, restaurantName, copies, menuMarkdown);
            return Job(false, "Printing is disabled (PRINT_MENUS is off), so the menu was logged but not printed.");
        }

        if (!OperatingSystem.IsWindows())
        {
            logger.LogWarning("Menu {JobId} was not printed: printing is only supported on Windows.", jobId);
            return Job(false, "Not printed: printing is only supported on Windows.");
        }

        try
        {
            var label = MenuLabelPrinter.Print(printer, restaurantName, menuMarkdown, copies);
            logger.LogInformation(
                "Printed menu {JobId} for {RestaurantName} on {Printer}: {Copies} copies at {FontSize}pt, truncated: {Truncated}",
                jobId, restaurantName, printer, copies, label.FontSize, label.Truncated);

            var status = label.Truncated
                ? $"Printed on {printer}, but the menu didn't fit on the label: {label.LinesPrinted} of {label.LinesTotal} lines were printed at {label.FontSize}pt. Consider a shorter menu."
                : $"Printed on {printer} at {label.FontSize}pt.";
            return Job(true, status, label);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to print menu {JobId} on {Printer}", jobId, printer);
            return Job(false, $"Not printed: {ex.Message}");
        }
    }

    public sealed record PrintJob(
        string JobId,
        string RestaurantName,
        int Copies,
        int Characters,
        bool Printed,
        string? Printer,
        float? FontSize,
        bool Truncated,
        string Status);
}
