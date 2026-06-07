using Otakarr.Models;

namespace Otakarr.Scrapers;

public class MockScraper : IScraper
{
    public string Name => "mock_scraper";

    public Task<List<SearchResult>> SearchAsync(string? query, int? season, int? episode)
    {
        var results = new List<SearchResult>();
        
        // Default to Frieren if query is null/empty
        bool isFrieren = string.IsNullOrEmpty(query) || query.Contains("frieren", StringComparison.OrdinalIgnoreCase);
        string baseTitle = isFrieren ? "Frieren: Beyond Journey's End" : query!;

        int startEp = episode ?? 1;
        int endEp = episode ?? 5; // Return up to 5 episodes if none specified
        int targetSeason = season ?? 1;

        for (int e = startEp; e <= endEp; e++)
        {
            var epTitle = $"{baseTitle} - S{targetSeason:D2}E{e:D2}";
            
            // Add a 1080p release
            results.Add(new SearchResult(
                Title: $"[MockSub] {epTitle} [1080p]",
                Url: $"https://example-streaming.com/watch/{baseTitle.ToLower().Replace(" ", "-")}-s{targetSeason}-e{e}-1080p",
                Guid: $"{Name}-{baseTitle.ToLower().Replace(" ", "-")}-s{targetSeason}-e{e}-1080p",
                PublishDate: DateTimeOffset.UtcNow.AddDays(-(10 - e)),
                Size: 1073741824L + (e * 50000000L), // ~1GB+
                Category: 5070, // TV/Anime
                Season: targetSeason,
                Episode: e,
                Resolution: "1080p",
                Source: "MockSub",
                ScraperName: Name
            ));

            // Add a 720p release
            results.Add(new SearchResult(
                Title: $"[MockSub] {epTitle} [720p]",
                Url: $"https://example-streaming.com/watch/{baseTitle.ToLower().Replace(" ", "-")}-s{targetSeason}-e{e}-720p",
                Guid: $"{Name}-{baseTitle.ToLower().Replace(" ", "-")}-s{targetSeason}-e{e}-720p",
                PublishDate: DateTimeOffset.UtcNow.AddDays(-(10 - e)),
                Size: 536870912L + (e * 25000000L), // ~500MB+
                Category: 5070, // TV/Anime
                Season: targetSeason,
                Episode: e,
                Resolution: "720p",
                Source: "MockSub",
                ScraperName: Name
            ));
        }

        return Task.FromResult(results);
    }
}
