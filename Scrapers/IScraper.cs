using Otakarr.Models;

namespace Otakarr.Scrapers;

public interface IScraper
{
    string Name { get; }
    Task<List<SearchResult>> SearchAsync(string? query, int? season, int? episode, string? searchType = null);
}
