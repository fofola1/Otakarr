using System.Text.Json.Serialization;

namespace Otakarr.Models;

public record DownloaderPayload(
    [property: JsonPropertyName("site")] string Site,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("season")] int? Season,
    [property: JsonPropertyName("ep")] int? Episode,
    [property: JsonPropertyName("stream_url")] string StreamUrl,
    [property: JsonPropertyName("resolution")] string Resolution,
    [property: JsonPropertyName("source")] string Source
);
