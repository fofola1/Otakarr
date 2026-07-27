using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Otakarr.Models;

namespace Otakarr.Scrapers;

public class AniListScraper : IScraper
{
    private readonly HttpClient _httpClient;

    public AniListScraper(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Name => "ani-cli";

    public async Task<List<SearchResult>> SearchAsync(string? query, int? season, int? episode)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await FetchRssFeedAsync();
        }

        return await SearchAnimeAsync(query.Trim(), season, episode);
    }

    private async Task<List<SearchResult>> SearchAnimeAsync(string query, int? season, int? episode)
    {
        var results = new List<SearchResult>();
        int targetSeason = season ?? 1;

        string searchQuery = query;
        int? targetEpisode = episode;

        // If episode parameter was not explicitly passed, check if query ends with an episode number (e.g. "Witch Hat Atelier 06")
        if (!targetEpisode.HasValue)
        {
            var match = Regex.Match(query, @"^(.*?)\s+(\d{1,3})$");
            if (match.Success)
            {
                searchQuery = match.Groups[1].Value.Trim();
                if (int.TryParse(match.Groups[2].Value, out var parsedEp))
                {
                    targetEpisode = parsedEp;
                }
            }
        }

        try
        {
            var graphqlQuery = new
            {
                query = @"query ($search: String) {
                    Page(page: 1, perPage: 5) {
                        media(search: $search, type: ANIME) {
                            id
                            title { english romaji native }
                            episodes
                            nextAiringEpisode { episode }
                            status
                        }
                    }
                }",
                variables = new { search = searchQuery }
            };

            var json = JsonSerializer.Serialize(graphqlQuery);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, "https://graphql.anilist.co")
            {
                Content = content
            };
            request.Headers.Add("User-Agent", "Otakarr/1.0");

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                if (doc.RootElement.TryGetProperty("data", out var dataEl) &&
                    dataEl.TryGetProperty("Page", out var pageEl) &&
                    pageEl.TryGetProperty("media", out var mediaArrEl) &&
                    mediaArrEl.ValueKind == JsonValueKind.Array &&
                    mediaArrEl.GetArrayLength() > 0)
                {
                    foreach (var media in mediaArrEl.EnumerateArray())
                    {
                        string? englishTitle = null;
                        string? romajiTitle = null;
                        if (media.TryGetProperty("title", out var titleEl))
                        {
                            if (titleEl.TryGetProperty("english", out var engProp)) englishTitle = engProp.GetString();
                            if (titleEl.TryGetProperty("romaji", out var romProp)) romajiTitle = romProp.GetString();
                        }

                        int totalEpisodes = 12; // Default fallback
                        if (media.TryGetProperty("episodes", out var epProp) && epProp.ValueKind == JsonValueKind.Number)
                        {
                            totalEpisodes = epProp.GetInt32();
                        }
                        else if (media.TryGetProperty("nextAiringEpisode", out var nextEpEl) &&
                                 nextEpEl.ValueKind == JsonValueKind.Object &&
                                 nextEpEl.TryGetProperty("episode", out var nextEpNumProp) &&
                                 nextEpNumProp.ValueKind == JsonValueKind.Number)
                        {
                            totalEpisodes = Math.Max(1, nextEpNumProp.GetInt32() - 1);
                        }

                        int startEp = targetEpisode ?? 1;
                        int endEp = targetEpisode ?? (totalEpisodes > 0 ? Math.Min(totalEpisodes, 100) : 12);

                        var titlesToUse = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        if (!string.IsNullOrWhiteSpace(englishTitle)) titlesToUse.Add(englishTitle);
                        if (!string.IsNullOrWhiteSpace(romajiTitle)) titlesToUse.Add(romajiTitle);

                        // If neither English nor Romaji was returned, fallback to searchQuery
                        if (titlesToUse.Count == 0) titlesToUse.Add(searchQuery);

                        foreach (var animeTitle in titlesToUse)
                        {
                            string cleanSlug = CleanSlug(animeTitle);

                            for (int e = startEp; e <= endEp; e++)
                            {
                                var epSeasonStr = $"S{targetSeason:D2}E{e:D2}";
                                var absEpStr = $"{e:D2}";

                                // 1. Standard + Absolute format (e.g. [AniCli] Witch Hat Atelier - S01E01 - 01 [1080p])
                                results.Add(new SearchResult(
                                    Title: $"[AniCli] {animeTitle} - {epSeasonStr} - {absEpStr} [1080p]",
                                    Url: $"ani-cli:stream/{cleanSlug}-s{targetSeason}-e{e}-1080p",
                                    Guid: $"{Name}-{cleanSlug}-s{targetSeason}-e{e}-abs-1080p",
                                    PublishDate: DateTimeOffset.UtcNow.AddDays(-(endEp - e)),
                                    Size: 1073741824L + (e * 50000000L),
                                    Category: 5070, // TV/Anime
                                    Season: targetSeason,
                                    Episode: e,
                                    Resolution: "1080p",
                                    Source: "AniCli",
                                    ScraperName: Name
                                ));

                                // 2. Standard format (e.g. [AniCli] Witch Hat Atelier - S01E01 [1080p])
                                results.Add(new SearchResult(
                                    Title: $"[AniCli] {animeTitle} - {epSeasonStr} [1080p]",
                                    Url: $"ani-cli:stream/{cleanSlug}-s{targetSeason}-e{e}-1080p-std",
                                    Guid: $"{Name}-{cleanSlug}-s{targetSeason}-e{e}-std-1080p",
                                    PublishDate: DateTimeOffset.UtcNow.AddDays(-(endEp - e)),
                                    Size: 1073741824L + (e * 50000000L),
                                    Category: 5070,
                                    Season: targetSeason,
                                    Episode: e,
                                    Resolution: "1080p",
                                    Source: "AniCli",
                                    ScraperName: Name
                                ));
                            }
                        }

                        // Stop after the best matching show from search results
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AniListScraper] Search failed for query '{query}': {ex.Message}");
        }

        // Fallback: If AniList search returned no results, generate results directly using searchQuery
        if (results.Count == 0 && !string.IsNullOrWhiteSpace(searchQuery))
        {
            int startEp = targetEpisode ?? 1;
            int endEp = targetEpisode ?? 12;
            string cleanSlug = CleanSlug(searchQuery);

            for (int e = startEp; e <= endEp; e++)
            {
                var epSeasonStr = $"S{targetSeason:D2}E{e:D2}";
                var absEpStr = $"{e:D2}";

                results.Add(new SearchResult(
                    Title: $"[AniCli] {searchQuery} - {epSeasonStr} - {absEpStr} [1080p]",
                    Url: $"ani-cli:stream/{cleanSlug}-s{targetSeason}-e{e}-1080p",
                    Guid: $"{Name}-{cleanSlug}-s{targetSeason}-e{e}-abs-1080p",
                    PublishDate: DateTimeOffset.UtcNow.AddDays(-(endEp - e)),
                    Size: 1073741824L + (e * 50000000L),
                    Category: 5070,
                    Season: targetSeason,
                    Episode: e,
                    Resolution: "1080p",
                    Source: "AniCli",
                    ScraperName: Name
                ));

                results.Add(new SearchResult(
                    Title: $"[AniCli] {searchQuery} - {epSeasonStr} [1080p]",
                    Url: $"ani-cli:stream/{cleanSlug}-s{targetSeason}-e{e}-1080p-std",
                    Guid: $"{Name}-{cleanSlug}-s{targetSeason}-e{e}-std-1080p",
                    PublishDate: DateTimeOffset.UtcNow.AddDays(-(endEp - e)),
                    Size: 1073741824L + (e * 50000000L),
                    Category: 5070,
                    Season: targetSeason,
                    Episode: e,
                    Resolution: "1080p",
                    Source: "AniCli",
                    ScraperName: Name
                ));
            }
        }

        return results;
    }

    private async Task<List<SearchResult>> FetchRssFeedAsync()
    {
        var results = new List<SearchResult>();

        try
        {
            var graphqlQuery = new
            {
                query = @"query {
                    Page(page: 1, perPage: 25) {
                        airingSchedules(notYetAired: false, sort: TIME_DESC) {
                            episode
                            media {
                                title { english romaji }
                                episodes
                            }
                        }
                    }
                }"
            };

            var json = JsonSerializer.Serialize(graphqlQuery);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, "https://graphql.anilist.co")
            {
                Content = content
            };
            request.Headers.Add("User-Agent", "Otakarr/1.0");

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                if (doc.RootElement.TryGetProperty("data", out var dataEl) &&
                    dataEl.TryGetProperty("Page", out var pageEl) &&
                    pageEl.TryGetProperty("airingSchedules", out var schedulesEl) &&
                    schedulesEl.ValueKind == JsonValueKind.Array)
                {
                    int index = 0;
                    foreach (var schedule in schedulesEl.EnumerateArray())
                    {
                        int episodeNum = schedule.TryGetProperty("episode", out var epProp) ? epProp.GetInt32() : 1;
                        if (schedule.TryGetProperty("media", out var mediaEl) && mediaEl.ValueKind == JsonValueKind.Object)
                        {
                            string? englishTitle = null;
                            string? romajiTitle = null;
                            if (mediaEl.TryGetProperty("title", out var titleEl))
                            {
                                if (titleEl.TryGetProperty("english", out var engProp)) englishTitle = engProp.GetString();
                                if (titleEl.TryGetProperty("romaji", out var romProp)) romajiTitle = romProp.GetString();
                            }

                            var titlesToUse = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            if (!string.IsNullOrWhiteSpace(englishTitle)) titlesToUse.Add(englishTitle);
                            if (!string.IsNullOrWhiteSpace(romajiTitle)) titlesToUse.Add(romajiTitle);

                            foreach (var animeTitle in titlesToUse)
                            {
                                string cleanSlug = CleanSlug(animeTitle);
                                var epSeasonStr = $"S01E{episodeNum:D2}";
                                var absEpStr = $"{episodeNum:D2}";

                                results.Add(new SearchResult(
                                    Title: $"[AniCli] {animeTitle} - {epSeasonStr} - {absEpStr} [1080p]",
                                    Url: $"ani-cli:stream/{cleanSlug}-s1-e{episodeNum}-1080p",
                                    Guid: $"{Name}-{cleanSlug}-s1-e{episodeNum}-abs-1080p",
                                    PublishDate: DateTimeOffset.UtcNow.AddMinutes(-index * 10),
                                    Size: 1073741824L,
                                    Category: 5070,
                                    Season: 1,
                                    Episode: episodeNum,
                                    Resolution: "1080p",
                                    Source: "AniCli",
                                    ScraperName: Name
                                ));
                            }

                            index++;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AniListScraper] RSS feed fetch failed: {ex.Message}");
        }

        return results;
    }

    private static string CleanSlug(string title)
    {
        return title.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace(":", "")
            .Replace("'", "")
            .Replace("?", "")
            .Replace("!", "");
    }
}
