using System.Xml.Linq;
using Otakarr.Models;

namespace Otakarr;

public static class Newznab
{
    private static readonly XNamespace NewznabNs = "http://www.newznab.com/DTD/2010/feeds/attributes/";
    private static readonly XNamespace TorznabNs = "http://torznab.com/schemas/2015/feed";

    public static string GetCapabilitiesXml()
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("caps",
                new XElement("server", 
                    new XAttribute("version", "1.0"), 
                    new XAttribute("title", "Otakarr"), 
                    new XAttribute("active", "true")),
                new XElement("limits", 
                    new XAttribute("max", "100"), 
                    new XAttribute("default", "50")),
                new XElement("registration", 
                    new XAttribute("available", "no"), 
                    new XAttribute("open", "no")),
                new XElement("searching",
                    new XElement("search", 
                        new XAttribute("available", "yes"), 
                        new XAttribute("supportedParams", "q")),
                    new XElement("tv-search", 
                        new XAttribute("available", "yes"), 
                        new XAttribute("supportedParams", "q,season,ep")),
                    new XElement("movie-search", 
                        new XAttribute("available", "yes"), 
                        new XAttribute("supportedParams", "q,imdbid,tmdbid"))
                ),
                new XElement("categories",
                    new XElement("category", new XAttribute("id", "2000"), new XAttribute("name", "Movies"),
                        new XElement("category", new XAttribute("id", "2030"), new XAttribute("name", "Movies/SD")),
                        new XElement("category", new XAttribute("id", "2040"), new XAttribute("name", "Movies/HD")),
                        new XElement("category", new XAttribute("id", "2070"), new XAttribute("name", "Movies/Anime"))
                    ),
                    new XElement("category", new XAttribute("id", "5000"), new XAttribute("name", "TV"),
                        new XElement("category", new XAttribute("id", "5030"), new XAttribute("name", "TV/SD")),
                        new XElement("category", new XAttribute("id", "5040"), new XAttribute("name", "TV/HD")),
                        new XElement("category", new XAttribute("id", "5070"), new XAttribute("name", "TV/Anime"))
                    )
                )
            )
        );
        return doc.ToString();
    }

    public static string GetSearchRssXml(IEnumerable<SearchResult> results, string downloaderBaseUrl, string hostUrl, int offset = 0, int totalResults = -1)
    {
        var resultsList = results.ToList();
        var total = totalResults >= 0 ? totalResults : resultsList.Count;

        var channel = new XElement("channel",
            new XElement("title", "Otakarr"),
            new XElement("description", "Otakarr Stateless Newznab Indexer"),
            new XElement("link", hostUrl),
            new XElement(NewznabNs + "response",
                new XAttribute("offset", offset),
                new XAttribute("total", total))
        );

        foreach (var res in resultsList)
        {
            var payload = new DownloaderPayload(
                Site: res.ScraperName,
                Id: res.Guid,
                Title: res.Title,
                Season: res.Season,
                Episode: res.Episode,
                StreamUrl: res.Url,
                Resolution: res.Resolution,
                Source: res.Source
            );

            var downloadUrl = EncodePayload(payload, downloaderBaseUrl);

            var item = new XElement("item",
                new XElement("title", res.Title),
                new XElement("guid", new XAttribute("isPermaLink", "false"), res.Guid),
                new XElement("link", downloadUrl),
                new XElement("pubDate", res.PublishDate.ToString("ddd, dd MMM yyyy HH:mm:ss zzz", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("size", res.Size),
                new XElement("category", res.Category),
                new XElement("enclosure", 
                    new XAttribute("url", downloadUrl), 
                    new XAttribute("length", res.Size), 
                    new XAttribute("type", "application/x-nzb")),
                
                new XElement(NewznabNs + "attr", new XAttribute("name", "category"), new XAttribute("value", res.Category)),
                new XElement(NewznabNs + "attr", new XAttribute("name", "size"), new XAttribute("value", res.Size)),

                new XElement(TorznabNs + "attr", new XAttribute("name", "category"), new XAttribute("value", res.Category)),
                new XElement(TorznabNs + "attr", new XAttribute("name", "size"), new XAttribute("value", res.Size))
            );

            if (res.Season.HasValue)
            {
                item.Add(new XElement(NewznabNs + "attr", new XAttribute("name", "season"), new XAttribute("value", res.Season.Value)));
                item.Add(new XElement(TorznabNs + "attr", new XAttribute("name", "season"), new XAttribute("value", res.Season.Value)));
            }
            if (res.Episode.HasValue)
            {
                item.Add(new XElement(NewznabNs + "attr", new XAttribute("name", "episode"), new XAttribute("value", res.Episode.Value)));
                item.Add(new XElement(TorznabNs + "attr", new XAttribute("name", "episode"), new XAttribute("value", res.Episode.Value)));
            }

            channel.Add(item);
        }

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("rss",
                new XAttribute("version", "2.0"),
                new XAttribute(XNamespace.Xmlns + "newznab", NewznabNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "torznab", TorznabNs.NamespaceName),
                channel
            )
        );

        return doc.ToString();
    }

    public static string EncodePayload(DownloaderPayload payload, string downloaderBaseUrl)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var base64 = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        
        var separator = downloaderBaseUrl.Contains('?') ? "&" : "?";
        return $"{downloaderBaseUrl}{separator}payload={base64}";
    }

    public static DownloaderPayload DecodePayload(string url)
    {
        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var base64 = query["payload"] ?? throw new ArgumentException("No payload query parameter found");
        
        base64 = base64.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        
        var bytes = Convert.FromBase64String(base64);
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        return System.Text.Json.JsonSerializer.Deserialize<DownloaderPayload>(json)!;
    }

    public static string GetErrorXml(int code, string description)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("error",
                new XAttribute("code", code),
                new XAttribute("description", description)
            )
        );
        return doc.ToString();
    }
}
