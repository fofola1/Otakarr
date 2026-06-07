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

// Simple health check endpoint
app.MapGet("/", () => Results.Ok(new { status = "online", service = "Otakarr" }));

// Support both /api and /api/torznab endpoints
app.MapGet("/api", HandleTorznabRequestAsync);
app.MapGet("/api/torznab", HandleTorznabRequestAsync);

async Task<IResult> HandleTorznabRequestAsync(
    [FromQuery] string? t,
    [FromQuery] string? q,
    [FromQuery] int? season,
    [FromQuery] int? ep,
    [FromQuery] string? tvdbid,
    [FromQuery] string? imdbid,
    [FromQuery] string? apikey,
    HttpContext httpContext,
    IHttpClientFactory httpClientFactory,
    ScraperManager scraperManager)
{
    // 1. Authenticate Request
    if (!string.IsNullOrEmpty(configuredApiKey) && !string.Equals(configuredApiKey, apikey, StringComparison.Ordinal))
    {
        return Results.Unauthorized();
    }

    // 2. Capabilities Check
    if (string.Equals(t, "caps", StringComparison.OrdinalIgnoreCase))
    {
        var capsXml = Torznab.GetCapabilitiesXml();
        return Results.Content(capsXml, "application/xml", System.Text.Encoding.UTF8);
    }

    // 3. Search Queries
    if (string.Equals(t, "tvsearch", StringComparison.OrdinalIgnoreCase) || 
        string.Equals(t, "search", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(t, "movie", StringComparison.OrdinalIgnoreCase))
    {
        string? searchQuery = q;

        // Resolve show title if Sonarr sent external IDs (TVDB / IMDB) without a text query
        if (string.IsNullOrEmpty(searchQuery) && (!string.IsNullOrEmpty(tvdbid) || !string.IsNullOrEmpty(imdbid)))
        {
            var httpClient = httpClientFactory.CreateClient();
            searchQuery = await ResolveShowTitleAsync(httpClient, tvdbid, imdbid);
            Console.WriteLine($"[Torznab] Resolved external ID (tvdb={tvdbid}, imdb={imdbid}) to title: '{searchQuery}'");
        }

        // Search streaming targets
        var searchResults = await scraperManager.SearchAllAsync(searchQuery, season, ep);

        // Get the indexer base host URL
        var hostUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}";
        
        // Generate Torznab search RSS response
        var rssXml = Torznab.GetSearchRssXml(searchResults, downloaderUrl, hostUrl);
        return Results.Content(rssXml, "application/xml", System.Text.Encoding.UTF8);
    }

    // Invalid or missing query parameters
    return Results.BadRequest(new { error = $"Unsupported 't' parameter value: '{t}'" });
}

async Task<string?> ResolveShowTitleAsync(HttpClient httpClient, string? tvdbId, string? imdbId)
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
        Console.WriteLine($"[Torznab] TVmaze ID resolution lookup failed: {ex.Message}");
    }
    return null;
}

app.Run();
