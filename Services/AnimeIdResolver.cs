using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Otakarr.Services;

public class AnimeIdResolver
{
    private static readonly ConcurrentDictionary<int, int> TvdbToAnilistMap = new();
    private static bool _isLoaded = false;
    private static readonly SemaphoreSlim LoadingSemaphore = new(1, 1);

    private readonly HttpClient _httpClient;

    public AnimeIdResolver(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task EnsureMappingsLoadedAsync()
    {
        if (_isLoaded) return;

        await LoadingSemaphore.WaitAsync();
        try
        {
            if (_isLoaded) return;

            Console.WriteLine("[AnimeIdResolver] Fetching Fribb anime-lists mapping JSON...");
            var response = await _httpClient.GetAsync("https://raw.githubusercontent.com/Fribb/anime-lists/master/anime-list-full.json");
            if (response.IsSuccessStatusCode)
            {
                using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);

                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    int mappedCount = 0;
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.TryGetProperty("tvdb_id", out var tvdbProp) && tvdbProp.ValueKind == JsonValueKind.Number)
                        {
                            int tvdbId = tvdbProp.GetInt32();
                            if (item.TryGetProperty("anilist_id", out var anilistProp) && anilistProp.ValueKind == JsonValueKind.Number)
                            {
                                int anilistId = anilistProp.GetInt32();
                                TvdbToAnilistMap[tvdbId] = anilistId;
                                mappedCount++;
                            }
                        }
                    }
                    Console.WriteLine($"[AnimeIdResolver] Successfully mapped {mappedCount} TVDB IDs to AniList IDs.");
                }
            }
            else
            {
                Console.WriteLine($"[AnimeIdResolver] Warning: Fribb anime-list fetch returned status {response.StatusCode}");
            }
            _isLoaded = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AnimeIdResolver] Failed to load Fribb anime-lists mapping: {ex.Message}");
        }
        finally
        {
            LoadingSemaphore.Release();
        }
    }

    public async Task<string?> ResolveTvdbIdAsync(string tvdbId)
    {
        if (!int.TryParse(tvdbId, out var tvdbIdInt))
        {
            return null;
        }

        await EnsureMappingsLoadedAsync();

        if (TvdbToAnilistMap.TryGetValue(tvdbIdInt, out var anilistId))
        {
            Console.WriteLine($"[AnimeIdResolver] TVDB ID {tvdbId} mapped to AniList ID {anilistId}. Querying AniList...");
            return await GetTitleByAnilistIdAsync(anilistId);
        }

        Console.WriteLine($"[AnimeIdResolver] TVDB ID {tvdbId} not found in Fribb map.");
        return null;
    }

    public async Task<string?> GetTitleByAnilistIdAsync(int anilistId)
    {
        try
        {
            var graphqlQuery = new
            {
                query = "query ($id: Int) { Media (id: $id, type: ANIME) { title { english romaji } } }",
                variables = new { id = anilistId }
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
                    dataEl.TryGetProperty("Media", out var mediaEl) &&
                    mediaEl.ValueKind == JsonValueKind.Object &&
                    mediaEl.TryGetProperty("title", out var titleEl))
                {
                    if (titleEl.TryGetProperty("english", out var engProp) && !string.IsNullOrEmpty(engProp.GetString()))
                    {
                        return engProp.GetString();
                    }
                    if (titleEl.TryGetProperty("romaji", out var romProp) && !string.IsNullOrEmpty(romProp.GetString()))
                    {
                        return romProp.GetString();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AnimeIdResolver] AniList lookup by ID {anilistId} failed: {ex.Message}");
        }

        return null;
    }
}
