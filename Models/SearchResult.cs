namespace Otakarr.Models;

public record SearchResult(
    string Title,
    string Url,
    string Guid,
    DateTimeOffset PublishDate,
    long Size,
    int Category,
    int? Season,
    int? Episode,
    string Resolution,
    string Source,
    string ScraperName,
    int Seeders = 100,
    int Peers = 10
);
