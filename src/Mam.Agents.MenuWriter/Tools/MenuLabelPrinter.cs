using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace Mam.Agents.MenuWriter.Tools;

/// <summary>How a menu was laid out on the label.</summary>
public sealed record LabelResult(float FontSize, bool Truncated, int LinesPrinted, int LinesTotal);

/// <summary>
/// Prints a Markdown menu on a single label of a (thermal) label printer, e.g. a 4" x 6" PM-241-BT.
/// The font shrinks until the menu fits on one label; at the minimum size the rest is cut off with an ellipsis.
/// </summary>
[SupportedOSPlatform("windows")]
public static partial class MenuLabelPrinter
{
    public const float MaxFontSize = 9f;
    public const float MinFontSize = 4.5f;
    private const float FontStep = 0.25f;
    private const string FontFamily = "Segoe UI";
    private const string Ellipsis = "…";

    /// <summary>Prints the menu on one label per copy and returns how it was laid out.</summary>
    public static LabelResult Print(string printerName, string restaurantName, string menuMarkdown, int copies)
    {
        using var document = new PrintDocument
        {
            DocumentName = $"Menu - {restaurantName}",
            PrintController = new StandardPrintController(),
            OriginAtMargins = true,
        };

        document.PrinterSettings.PrinterName = printerName;
        if (!document.PrinterSettings.IsValid)
        {
            throw new InvalidOperationException($"Printer '{printerName}' was not found.");
        }

        // About 2.5 mm on every side; the label size comes from the printer's default paper (4" x 6").
        document.DefaultPageSettings.Margins = new Margins(10, 10, 10, 10);

        var blocks = Parse(restaurantName, menuMarkdown);
        LabelResult? result = null;
        var printed = 0;

        document.PrintPage += (_, e) =>
        {
            var area = new RectangleF(0, 0, e.MarginBounds.Width, e.MarginBounds.Height);
            result = Draw(e.Graphics!, area, blocks);
            e.HasMorePages = ++printed < copies;
        };

        document.Print();
        return result ?? throw new InvalidOperationException("The printer didn't print any page.");
    }

    /// <summary>Lays out and draws the menu in <paramref name="area"/>, using the largest font size that fits.</summary>
    public static LabelResult Draw(Graphics graphics, RectangleF area, string restaurantName, string menuMarkdown) =>
        Draw(graphics, area, Parse(restaurantName, menuMarkdown));

    private static LabelResult Draw(Graphics graphics, RectangleF area, IReadOnlyList<Block> blocks)
    {
        // Thermal printers are black and white: no grey anti-aliased edges, measured the same way as drawn.
        graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        graphics.SmoothingMode = SmoothingMode.None;

        Layout? layout = null;
        for (var size = MaxFontSize; size >= MinFontSize - 0.001f; size -= FontStep)
        {
            layout?.Dispose();
            layout = Layout.Create(graphics, area, blocks, size);
            if (layout.Fits)
            {
                break;
            }
        }

        using (layout)
        {
            return layout!.Draw(graphics, area);
        }
    }

    private enum Style { Title, Heading, Text, Rule, Gap }

    private sealed record Block(Style Style, string Text = "");

    private sealed record Line(Style Style, string Text, Font Font, float Height);

    private sealed class Layout : IDisposable
    {
        private readonly Dictionary<Style, Font> _fonts;
        private readonly List<Line> _lines = [];
        private readonly float _fontSize;

        private Layout(float fontSize)
        {
            this._fontSize = fontSize;
            this._fonts = new()
            {
                [Style.Title] = new Font(FontFamily, fontSize * 1.6f, FontStyle.Bold, GraphicsUnit.Point),
                [Style.Heading] = new Font(FontFamily, fontSize * 1.15f, FontStyle.Bold, GraphicsUnit.Point),
                [Style.Text] = new Font(FontFamily, fontSize, FontStyle.Regular, GraphicsUnit.Point),
            };
        }

        public bool Fits { get; private set; }

        public static Layout Create(Graphics graphics, RectangleF area, IReadOnlyList<Block> blocks, float fontSize)
        {
            var layout = new Layout(fontSize);
            var textFont = layout._fonts[Style.Text];
            var textHeight = textFont.GetHeight(graphics);

            foreach (var block in blocks)
            {
                switch (block.Style)
                {
                    case Style.Gap:
                        layout._lines.Add(new Line(Style.Gap, "", textFont, textHeight * 0.4f));
                        break;
                    case Style.Rule:
                        layout._lines.Add(new Line(Style.Rule, "", textFont, textHeight * 0.6f));
                        break;
                    default:
                        var font = layout._fonts[block.Style];
                        var height = font.GetHeight(graphics);
                        foreach (var wrapped in Wrap(graphics, block.Text, font, area.Width))
                        {
                            layout._lines.Add(new Line(block.Style, wrapped, font, height));
                        }

                        break;
                }
            }

            layout.Fits = layout._lines.Sum(l => l.Height) <= area.Height;
            return layout;
        }

        public LabelResult Draw(Graphics graphics, RectangleF area)
        {
            using var centered = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center };
            using var left = new StringFormat(StringFormat.GenericTypographic);
            using var pen = new Pen(Color.Black, 1);

            var ellipsisHeight = this._fonts[Style.Text].GetHeight(graphics);
            var y = area.Top;
            var drawn = 0;
            var truncated = false;

            foreach (var line in this._lines)
            {
                var isLast = drawn == this._lines.Count - 1;
                var reserve = this.Fits || isLast ? 0 : ellipsisHeight;
                if (y + line.Height + reserve > area.Bottom)
                {
                    truncated = true;
                    break;
                }

                switch (line.Style)
                {
                    case Style.Rule:
                        var middle = y + (line.Height / 2);
                        graphics.DrawLine(pen, area.Left, middle, area.Right, middle);
                        break;
                    case Style.Title:
                    case Style.Heading:
                    case Style.Text:
                        var format = line.Style == Style.Title ? centered : left;
                        graphics.DrawString(line.Text, line.Font, Brushes.Black, new RectangleF(area.Left, y, area.Width, line.Height), format);
                        break;
                }

                y += line.Height;
                drawn++;
            }

            if (truncated)
            {
                graphics.DrawString(Ellipsis, this._fonts[Style.Text], Brushes.Black, new RectangleF(area.Left, y, area.Width, ellipsisHeight), centered);
            }

            return new LabelResult(this._fontSize, truncated, drawn, this._lines.Count);
        }

        public void Dispose()
        {
            foreach (var font in this._fonts.Values)
            {
                font.Dispose();
            }
        }
    }

    private static IEnumerable<string> Wrap(Graphics graphics, string text, Font font, float width)
    {
        // Keep leading indentation (e.g. "• ") on the first line and indent continuation lines to match.
        var indent = text.StartsWith("• ", StringComparison.Ordinal) ? "   " : "";
        using var format = new StringFormat(StringFormat.GenericTypographic) { FormatFlags = StringFormatFlags.MeasureTrailingSpaces };
        var current = "";

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if (current.Length > 0 && graphics.MeasureString(candidate, font, int.MaxValue, format).Width > width)
            {
                yield return current;
                current = indent + word;
            }
            else
            {
                current = candidate;
            }
        }

        if (current.Length > 0)
        {
            yield return current;
        }
    }

    /// <summary>Turns the Markdown into a flat list of blocks; only what makes sense on a small label is kept.</summary>
    private static List<Block> Parse(string restaurantName, string markdown)
    {
        var blocks = new List<Block> { new(Style.Title, restaurantName.Trim()), new(Style.Rule) };

        foreach (var raw in markdown.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                AddGap(blocks);
                continue;
            }

            if (RuleRegex().IsMatch(line) || TableSeparatorRegex().IsMatch(line))
            {
                continue;
            }

            if (HeadingRegex().Match(line) is { Success: true } heading)
            {
                var text = Inline(heading.Groups[2].Value);

                // The restaurant name is already printed as the title.
                if (!string.Equals(text, restaurantName.Trim(), StringComparison.OrdinalIgnoreCase) && text.Length > 0)
                {
                    AddGap(blocks);
                    blocks.Add(new Block(Style.Heading, text.ToUpperInvariant()));
                }

                continue;
            }

            if (BulletRegex().Match(line) is { Success: true } bullet)
            {
                blocks.Add(new Block(Style.Text, $"• {Inline(bullet.Groups[1].Value)}"));
                continue;
            }

            if (line.StartsWith('|'))
            {
                var cells = line.Trim('|').Split('|').Select(c => Inline(c.Trim())).Where(c => c.Length > 0);
                blocks.Add(new Block(Style.Text, string.Join("  ", cells)));
                continue;
            }

            blocks.Add(new Block(Style.Text, Inline(line.TrimStart('>').Trim())));
        }

        while (blocks[^1].Style == Style.Gap)
        {
            blocks.RemoveAt(blocks.Count - 1);
        }

        return blocks;
    }

    private static void AddGap(List<Block> blocks)
    {
        if (blocks[^1].Style is not (Style.Gap or Style.Rule))
        {
            blocks.Add(new Block(Style.Gap));
        }
    }

    private static string Inline(string text)
    {
        text = ImageRegex().Replace(text, "");
        text = LinkRegex().Replace(text, "$1");
        text = EmphasisRegex().Replace(text, "$2");
        text = text.Replace("`", "");
        return WhitespaceRegex().Replace(text, " ").Trim();
    }

    [GeneratedRegex(@"^(#{1,6})\s*(.*?)\s*#*$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^(?:[-*+]|\d+[.)])\s+(.*)$")]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"^([-*_])(\s*\1){2,}$")]
    private static partial Regex RuleRegex();

    [GeneratedRegex(@"^\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?$")]
    private static partial Regex TableSeparatorRegex();

    [GeneratedRegex(@"!\[[^\]]*\]\([^)]*\)")]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"(\*\*|__|\*|_)(\S(?:.*?\S)?)\1")]
    private static partial Regex EmphasisRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
