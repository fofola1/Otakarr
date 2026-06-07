using Otakarr.Models;

namespace Otakarr.Scrapers;

public class ScraperManager
{
    private readonly List<IScraper> _scrapers;

    public ScraperManager(IEnumerable<IScraper> scrapers)
    {
        _scrapers = scrapers.ToList();
    }

    public async Task<List<SearchResult>> SearchAllAsync(string? query, int? season, int? episode)
    {
        var tasks = _scrapers.Select(async scraper =>
        {
            try
            {
                return await scraper.SearchAsync(query, season, episode);
            }
            catch (Exception ex)
            {
                // Log and swallow error to prevent one scraper from failing the entire search
                Console.WriteLine($"Error running scraper {scraper.Name}: {ex.Message}");
                return new List<SearchResult>();
            }
        });

        var resultsArray = await Task.WhenAll(tasks);
        return resultsArray.SelectMany(results => results).ToList();
    }
}
