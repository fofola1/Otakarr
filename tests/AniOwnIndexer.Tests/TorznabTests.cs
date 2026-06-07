using Xunit;
using System.Xml.Linq;
using Otakarr;
using Otakarr.Models;

namespace Otakarr.Tests;

public class TorznabTests
{
    [Fact]
    public void TestEncodeDecodePayload()
    {
        // Arrange
        var originalPayload = new DownloaderPayload(
            Site: "mock_scraper",
            Id: "frieren-s01e05",
            Title: "Frieren: Beyond Journey's End",
            Season: 1,
            Episode: 5,
            StreamUrl: "https://example.com/watch/frieren-5",
            Resolution: "1080p",
            Source: "MockSub"
        );
        var downloaderBaseUrl = "http://aniown-downloader:8080/download";

        // Act
        var urlWithPayload = Torznab.EncodePayload(originalPayload, downloaderBaseUrl);
        var decodedPayload = Torznab.DecodePayload(urlWithPayload);

        // Assert
        Assert.StartsWith(downloaderBaseUrl, urlWithPayload);
        Assert.Contains("payload=", urlWithPayload);
        Assert.Equal(originalPayload.Site, decodedPayload.Site);
        Assert.Equal(originalPayload.Id, decodedPayload.Id);
        Assert.Equal(originalPayload.Title, decodedPayload.Title);
        Assert.Equal(originalPayload.Season, decodedPayload.Season);
        Assert.Equal(originalPayload.Episode, decodedPayload.Episode);
        Assert.Equal(originalPayload.StreamUrl, decodedPayload.StreamUrl);
        Assert.Equal(originalPayload.Resolution, decodedPayload.Resolution);
        Assert.Equal(originalPayload.Source, decodedPayload.Source);
    }

    [Fact]
    public void TestCapabilitiesXml()
    {
        // Act
        var xmlString = Torznab.GetCapabilitiesXml();
        var doc = XDocument.Parse(xmlString);

        // Assert
        Assert.NotNull(doc.Root);
        Assert.Equal("caps", doc.Root.Name.LocalName);
        
        var server = doc.Root.Element("server");
        Assert.NotNull(server);
        Assert.Equal("Otakarr", server.Attribute("title")?.Value);

        var categories = doc.Root.Element("categories");
        Assert.NotNull(categories);
        
        var tvCategory = categories.Elements("category")
            .FirstOrDefault(c => c.Attribute("id")?.Value == "5000");
        Assert.NotNull(tvCategory);
        
        var animeSubcat = tvCategory.Elements("subcat")
            .FirstOrDefault(s => s.Attribute("id")?.Value == "5070");
        Assert.NotNull(animeSubcat);
        Assert.Equal("TV/Anime", animeSubcat.Attribute("name")?.Value);
    }

    [Fact]
    public void TestSearchRssXml()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            new SearchResult(
                Title: "[MockSub] Frieren - S01E05 [1080p]",
                Url: "https://example.com/frieren-5",
                Guid: "mock_scraper-frieren-s1-e5-1080p",
                PublishDate: DateTimeOffset.UtcNow,
                Size: 1073741824,
                Category: 5070,
                Season: 1,
                Episode: 5,
                Resolution: "1080p",
                Source: "MockSub",
                ScraperName: "mock_scraper"
            )
        };
        var downloaderBaseUrl = "http://aniown-downloader:8080/download";
        var hostUrl = "http://localhost:8000";

        // Act
        var xmlString = Torznab.GetSearchRssXml(results, downloaderBaseUrl, hostUrl);
        var doc = XDocument.Parse(xmlString);

        // Assert
        Assert.NotNull(doc.Root);
        Assert.Equal("rss", doc.Root.Name.LocalName);

        var channel = doc.Root.Element("channel");
        Assert.NotNull(channel);

        var item = channel.Element("item");
        Assert.NotNull(item);
        Assert.Equal("[MockSub] Frieren - S01E05 [1080p]", item.Element("title")?.Value);
        Assert.Equal("mock_scraper-frieren-s1-e5-1080p", item.Element("guid")?.Value);

        var enclosure = item.Element("enclosure");
        Assert.NotNull(enclosure);
        var enclosureUrl = enclosure.Attribute("url")?.Value;
        Assert.NotNull(enclosureUrl);
        Assert.StartsWith(downloaderBaseUrl, enclosureUrl);

        var decoded = Torznab.DecodePayload(enclosureUrl);
        Assert.Equal("mock_scraper", decoded.Site);
        Assert.Equal("mock_scraper-frieren-s1-e5-1080p", decoded.Id);
        Assert.Equal(1, decoded.Season);
        Assert.Equal(5, decoded.Episode);

        XNamespace torznabNs = "http://torznab.com/schemas/2015/feed";
        var attrs = item.Elements(torznabNs + "attr").ToList();
        Assert.NotEmpty(attrs);
        
        var catAttr = attrs.FirstOrDefault(a => a.Attribute("name")?.Value == "category");
        Assert.NotNull(catAttr);
        Assert.Equal("5070", catAttr.Attribute("value")?.Value);

        var seasonAttr = attrs.FirstOrDefault(a => a.Attribute("name")?.Value == "season");
        Assert.NotNull(seasonAttr);
        Assert.Equal("1", seasonAttr.Attribute("value")?.Value);

        var epAttr = attrs.FirstOrDefault(a => a.Attribute("name")?.Value == "episode");
        Assert.NotNull(epAttr);
        Assert.Equal("5", epAttr.Attribute("value")?.Value);
    }
}
