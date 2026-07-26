using Otakarr.Models;

namespace Otakarr.Scrapers;

public class MockScraper : IScraper
{
    public string Name => "mock_scraper";

    public Task<List<SearchResult>> SearchAsync(string? query, int? season, int? episode)
    {
        var results = new List<SearchResult>();

        // If no query provided (RSS feed mode), we can't generate meaningful results
        // because we don't know what the user is looking for. Return empty so the
        // RSS sync doesn't pollute Sonarr with fake results for wrong shows.
        // Actual searches will always have a query (either from q= param or resolved from tvdbid).
        if (string.IsNullOrWhiteSpace(query))
        {
            results.Add(new SearchResult(
                Title: "[OtakarrTest] Indexer Test Release - S01E01 [1080p]",
                Url: "https://example-streaming.com/watch/otakarr-test-s1-e1-1080p",
                Guid: "otakarr-test-s1-e1-1080p",
                PublishDate: DateTimeOffset.UtcNow,
                Size: 1073741824L,
                Category: 5070, // TV/Anime
                Season: 1,
                Episode: 1,
                Resolution: "1080p",
                Source: "OtakarrTest",
                ScraperName: Name
            ));
            return Task.FromResult(results);
        }

        string baseTitle = query;
        int startEp = episode ?? 1;
        int endEp = episode ?? 12; // Return up to 12 episodes if none specified
        int targetSeason = season ?? 1;
        string cleanSlug = baseTitle.ToLower().Replace(" ", "-").Replace(":", "").Replace("'", "");

        for (int e = startEp; e <= endEp; e++)
        {
            var epTitle = $"{baseTitle} - S{targetSeason:D2}E{e:D2}";

            // Add a 1080p Anime release
            results.Add(new SearchResult(
                Title: $"[MockSub] {epTitle} [1080p]",
                Url: $"https://example-streaming.com/watch/{cleanSlug}-s{targetSeason}-e{e}-1080p",
                Guid: $"{Name}-{cleanSlug}-s{targetSeason}-e{e}-1080p",
                PublishDate: DateTimeOffset.UtcNow.AddDays(-(endEp - e)),
                Size: 1073741824L + (e * 50000000L), // ~1GB+
                Category: 5070, // TV/Anime
                Season: targetSeason,
                Episode: e,
                Resolution: "1080p",
                Source: "MockSub",
                ScraperName: Name
            ));

            // Add a 1080p TV/HD release
            results.Add(new SearchResult(
                Title: $"[MockSub] {epTitle} [1080p] (HD)",
                Url: $"https://example-streaming.com/watch/{cleanSlug}-s{targetSeason}-e{e}-1080p-hd",
                Guid: $"{Name}-{cleanSlug}-s{targetSeason}-e{e}-1080p-hd",
                PublishDate: DateTimeOffset.UtcNow.AddDays(-(endEp - e)),
                Size: 1073741824L + (e * 50000000L), // ~1GB+
                Category: 5040, // TV/HD
                Season: targetSeason,
                Episode: e,
                Resolution: "1080p",
                Source: "MockSub",
                ScraperName: Name
            ));

            // Add a 720p TV/SD release
            results.Add(new SearchResult(
                Title: $"[MockSub] {epTitle} [720p]",
                Url: $"https://example-streaming.com/watch/{cleanSlug}-s{targetSeason}-e{e}-720p",
                Guid: $"{Name}-{cleanSlug}-s{targetSeason}-e{e}-720p",
                PublishDate: DateTimeOffset.UtcNow.AddDays(-(endEp - e)),
                Size: 536870912L + (e * 25000000L), // ~500MB+
                Category: 5030, // TV/SD
                Season: targetSeason,
                Episode: e,
                Resolution: "720p",
                Source: "MockSub",
                ScraperName: Name
            ));
        }

        // If doing a generic search (no specific season and episode), add Movie results too
        if (!season.HasValue && !episode.HasValue)
        {
            // Add a 1080p Movie/Anime release
            results.Add(new SearchResult(
                Title: $"[MockSub] {baseTitle} - The Movie [1080p]",
                Url: $"https://example-streaming.com/watch/{cleanSlug}-movie-1080p",
                Guid: $"{Name}-{cleanSlug}-movie-1080p",
                PublishDate: DateTimeOffset.UtcNow.AddDays(-1),
                Size: 4294967296L, // ~4GB
                Category: 2070, // Movies/Anime
                Season: null,
                Episode: null,
                Resolution: "1080p",
                Source: "MockSub",
                ScraperName: Name
            ));

            // Add a 1080p Movies/HD release
            results.Add(new SearchResult(
                Title: $"[MockSub] {baseTitle} - The Movie [1080p] (HD)",
                Url: $"https://example-streaming.com/watch/{cleanSlug}-movie-1080p-hd",
                Guid: $"{Name}-{cleanSlug}-movie-1080p-hd",
                PublishDate: DateTimeOffset.UtcNow.AddDays(-1),
                Size: 4294967296L, // ~4GB
                Category: 2040, // Movies/HD
                Season: null,
                Episode: null,
                Resolution: "1080p",
                Source: "MockSub",
                ScraperName: Name
            ));

            // Add a 720p Movies/SD release
            results.Add(new SearchResult(
                Title: $"[MockSub] {baseTitle} - The Movie [720p]",
                Url: $"https://example-streaming.com/watch/{cleanSlug}-movie-720p",
                Guid: $"{Name}-{cleanSlug}-movie-720p",
                PublishDate: DateTimeOffset.UtcNow.AddDays(-1),
                Size: 2147483648L, // ~2GB
                Category: 2030, // Movies/SD
                Season: null,
                Episode: null,
                Resolution: "720p",
                Source: "MockSub",
                ScraperName: Name
            ));
        }

        return Task.FromResult(results);
    }
}
