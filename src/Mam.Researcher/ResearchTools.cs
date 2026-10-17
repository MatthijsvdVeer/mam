using System.ComponentModel;

namespace Mam.Researcher;

/// <summary>
/// Stubbed research tools. They return canned data so the demo runs without external services.
/// </summary>
public static class ResearchTools
{
    [Description("Searches the web and returns a list of results with title, URL and snippet.")]
    public static IReadOnlyList<SearchResult> WebSearch(
        [Description("The search query.")] string query,
        [Description("Maximum number of results to return.")] int maxResults = 5)
    {
        // TODO: replace with a real search provider (e.g. Bing grounding / Foundry web search tool).
        var slug = Uri.EscapeDataString(query.ToLowerInvariant().Replace(' ', '-'));
        SearchResult[] results =
        [
            new($"{query}: the complete guide", $"https://example.com/guides/{slug}", $"Everything you need to know about {query}, from history to modern takes."),
            new($"Top 10 trends in {query}", $"https://example.com/trends/{slug}", $"Chefs and critics share where {query} is heading next year."),
            new($"Why diners love {query}", $"https://example.com/stories/{slug}", $"A look at what makes {query} resonate with guests."),
            new($"{query} on a budget", $"https://example.com/business/{slug}", $"Food cost, sourcing and pricing considerations for {query}."),
            new($"Reviews: the best {query} spots", $"https://example.com/reviews/{slug}", $"Our critics visited the most talked-about {query} places in town."),
        ];

        return results.Take(Math.Clamp(maxResults, 1, results.Length)).ToArray();
    }

    [Description("Fetches a web page and returns its main textual content.")]
    public static string WebFetch([Description("The absolute URL of the page to fetch.")] string url)
    {
        // TODO: replace with a real HTTP fetch + HTML-to-text extraction.
        return $"""
            # Content of {url}

            (stubbed content) This page discusses the topic in depth. Key points:
            - Guests increasingly value authenticity, seasonality and a clear story behind the food.
            - Sharing plates and flexible portion sizes are popular with groups.
            - A distinctive visual identity strongly drives social media word-of-mouth.
            - Price sensitivity is rising; a few accessible signature items help conversion.
            """;
    }

    public sealed record SearchResult(string Title, string Url, string Snippet);
}
