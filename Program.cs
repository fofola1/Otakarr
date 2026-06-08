using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Net.Http;
using Otakarr;
using Otakarr.Scrapers;

var builder = WebApplication.CreateBuilder(args);

// Load PORT from environment variable or default to 8000
var portStr = Environment.GetEnvironmentVariable("PORT") ?? "8000";
if (int.TryParse(portStr, out var port))
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(port);
    });
}

// Add services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IScraper, MockScraper>();
builder.Services.AddSingleton<ScraperManager>();

var app = builder.Build();

// Configuration values
var downloaderUrl = Environment.GetEnvironmentVariable("DOWNLOADER_URL") ?? "http://localhost:8080/download";
var configuredApiKey = Environment.GetEnvironmentVariable("API_KEY");

// Enable static and default files serving (e.g., serves index.html from wwwroot)
app.UseDefaultFiles();
app.UseStaticFiles();

// Expose a route for config status check (UI)
app.MapGet("/api/config-status", () => 
{
    var scrapers = app.Services.GetRequiredService<IEnumerable<IScraper>>();
    return Results.Ok(new 
    {
        downloaderUrl = downloaderUrl,
        apiKeyConfigured = !string.IsNullOrEmpty(configuredApiKey),
        activeScrapers = scrapers.Select(s => s.Name).ToList(),
        port = port
    });
});

// JSON Search endpoint for the UI dashboard (avoids XML parsing in frontend)
app.MapGet("/api/search-json", async (
    [FromQuery] string? q,
    [FromQuery] int? season,
    [FromQuery] int? ep,
    ScraperManager scraperManager) =>
{
    var results = await scraperManager.SearchAllAsync(q, season, ep);
    return Results.Ok(results);
});

// Support /api, /api/newznab and legacy /api/torznab endpoints
app.MapGet("/api", HandleNewznabRequestAsync);
app.MapGet("/api/newznab", HandleNewznabRequestAsync);
app.MapGet("/api/torznab", HandleNewznabRequestAsync);

async Task<IResult> HandleNewznabRequestAsync(
    [FromQuery] string? t,
    [FromQuery] string? q,
    [FromQuery] int? season,
    [FromQuery] int? ep,
    [FromQuery] string? tvdbid,
    [FromQuery] string? imdbid,
    [FromQuery] string? tvmazeid,
    [FromQuery] string? cat,
    [FromQuery] int? offset,
    [FromQuery] int? limit,
    [FromQuery] string? apikey,
    HttpContext httpContext,
    IHttpClientFactory httpClientFactory,
    ScraperManager scraperManager)
{
    // 1. Authenticate Request
    if (!string.IsNullOrEmpty(configuredApiKey) && !string.Equals(configuredApiKey, apikey, StringComparison.Ordinal))
    {
        var errorXml = Newznab.GetErrorXml(100, "Incorrect user credentials");
        return Results.Text(errorXml, "application/xml", System.Text.Encoding.UTF8, 401);
    }

    // 2. Missing Command Param
    if (string.IsNullOrEmpty(t))
    {
        var errorXml = Newznab.GetErrorXml(200, "Missing parameter: 't'");
        return Results.Text(errorXml, "application/xml", System.Text.Encoding.UTF8, 400);
    }

    // 3. Capabilities Check
    if (string.Equals(t, "caps", StringComparison.OrdinalIgnoreCase))
    {
        var capsXml = Newznab.GetCapabilitiesXml();
        return Results.Content(capsXml, "application/xml", System.Text.Encoding.UTF8);
    }

    // 4. Search Queries
    if (string.Equals(t, "tvsearch", StringComparison.OrdinalIgnoreCase) || 
        string.Equals(t, "search", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(t, "movie", StringComparison.OrdinalIgnoreCase))
    {
        string? searchQuery = q;

        // Resolve show title if Sonarr/Radarr sent external IDs (TVDB / IMDB / TVmaze) without a text query
        if (string.IsNullOrEmpty(searchQuery) && (!string.IsNullOrEmpty(tvdbid) || !string.IsNullOrEmpty(imdbid) || !string.IsNullOrEmpty(tvmazeid)))
        {
            var httpClient = httpClientFactory.CreateClient();
            searchQuery = await ResolveShowTitleAsync(httpClient, tvdbid, imdbid, tvmazeid);
            Console.WriteLine($"[Newznab] Resolved external ID (tvdb={tvdbid}, imdb={imdbid}, tvmaze={tvmazeid}) to title: '{searchQuery}'");
        }

        // Search streaming targets
        var searchResults = await scraperManager.SearchAllAsync(searchQuery, season, ep);

        // Filter by requested category if present
        if (!string.IsNullOrEmpty(cat))
        {
            try
            {
                var requestedCats = cat.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                       .Select(int.Parse)
                                       .ToHashSet();
                if (requestedCats.Any())
                {
                    var expandedCats = new HashSet<int>(requestedCats);
                    if (requestedCats.Contains(5000))
                    {
                        expandedCats.Add(5030);
                        expandedCats.Add(5040);
                        expandedCats.Add(5070);
                    }
                    if (requestedCats.Contains(2000))
                    {
                        expandedCats.Add(2030);
                        expandedCats.Add(2040);
                        expandedCats.Add(2070);
                    }
                    searchResults = searchResults.Where(r => expandedCats.Contains(r.Category)).ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Newznab] Failed to parse category filters '{cat}': {ex.Message}");
            }
        }

        // Apply Pagination (offset/limit)
        var startOffset = offset ?? 0;
        var fetchLimit = limit ?? 100;
        var paginatedResults = searchResults.Skip(startOffset).Take(fetchLimit);

        // Get the indexer base host URL
        var hostUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}";
        
        // Generate Newznab search RSS response
        var rssXml = Newznab.GetSearchRssXml(paginatedResults, downloaderUrl, hostUrl);
        return Results.Content(rssXml, "application/xml", System.Text.Encoding.UTF8);
    }

    // Unknown/unsupported function command
    var unknownFuncXml = Newznab.GetErrorXml(201, $"Unknown function: '{t}'");
    return Results.Text(unknownFuncXml, "application/xml", System.Text.Encoding.UTF8, 400);
}

async Task<string?> ResolveShowTitleAsync(HttpClient httpClient, string? tvdbId, string? imdbId, string? tvmazeId)
{
    try
    {
        string? url = null;
        if (!string.IsNullOrEmpty(tvdbId))
        {
            url = $"https://api.tvmaze.com/lookup/shows?thetvdb={tvdbId}";
        }
        else if (!string.IsNullOrEmpty(imdbId))
        {
            url = $"https://api.tvmaze.com/lookup/shows?imdb={imdbId}";
        }
        else if (!string.IsNullOrEmpty(tvmazeId))
        {
            url = $"https://api.tvmaze.com/shows/{tvmazeId}";
        }

        if (string.IsNullOrEmpty(url)) return null;

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "Otakarr/1.0");

        var response = await httpClient.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            if (doc.RootElement.TryGetProperty("name", out var nameProp))
            {
                return nameProp.GetString();
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Torznab] ID resolution lookup failed: {ex.Message}");
    }
    return null;
}

app.Run();
