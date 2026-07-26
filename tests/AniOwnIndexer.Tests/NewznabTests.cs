using Xunit;
using System.Xml.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Otakarr;
using Otakarr.Models;
using Otakarr.Scrapers;
using Otakarr.Services;

namespace Otakarr.Tests;

public class NewznabTests
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
        var urlWithPayload = Newznab.EncodePayload(originalPayload, downloaderBaseUrl);
        var decodedPayload = Newznab.DecodePayload(urlWithPayload);

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
        var xmlString = Newznab.GetCapabilitiesXml();
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
        
        var animeSubcat = tvCategory.Elements("category")
            .FirstOrDefault(s => s.Attribute("id")?.Value == "5070");
        Assert.NotNull(animeSubcat);
        Assert.Equal("TV/Anime", animeSubcat.Attribute("name")?.Value);

        var movieCategory = categories.Elements("category")
            .FirstOrDefault(c => c.Attribute("id")?.Value == "2000");
        Assert.NotNull(movieCategory);

        var movieAnimeSubcat = movieCategory.Elements("category")
            .FirstOrDefault(s => s.Attribute("id")?.Value == "2070");
        Assert.NotNull(movieAnimeSubcat);
        Assert.Equal("Movies/Anime", movieAnimeSubcat.Attribute("name")?.Value);
    }

    [Fact]
    public void TestSearchRssXml()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            new SearchResult(
                Title: "[AniCli] Frieren - S01E05 [1080p]",
                Url: "https://example.com/frieren-5",
                Guid: "ani-cli-frieren-s1-e5-1080p",
                PublishDate: DateTimeOffset.UtcNow,
                Size: 1073741824,
                Category: 5070,
                Season: 1,
                Episode: 5,
                Resolution: "1080p",
                Source: "AniCli",
                ScraperName: "ani-cli"
            )
        };
        var downloaderBaseUrl = "http://aniown-downloader:8080/download";
        var hostUrl = "http://localhost:8000";

        // Act
        var xmlString = Newznab.GetSearchRssXml(results, downloaderBaseUrl, hostUrl);
        var doc = XDocument.Parse(xmlString);

        // Assert
        Assert.NotNull(doc.Root);
        Assert.Equal("rss", doc.Root.Name.LocalName);

        var channel = doc.Root.Element("channel");
        Assert.NotNull(channel);

        var item = channel.Element("item");
        Assert.NotNull(item);
        Assert.Equal("[AniCli] Frieren - S01E05 [1080p]", item.Element("title")?.Value);
        Assert.Equal("ani-cli-frieren-s1-e5-1080p", item.Element("guid")?.Value);

        var enclosure = item.Element("enclosure");
        Assert.NotNull(enclosure);
        var enclosureUrl = enclosure.Attribute("url")?.Value;
        Assert.NotNull(enclosureUrl);
        Assert.StartsWith(downloaderBaseUrl, enclosureUrl);
        Assert.Equal("application/x-nzb", enclosure.Attribute("type")?.Value);

        var decoded = Newznab.DecodePayload(enclosureUrl);
        Assert.Equal("ani-cli", decoded.Site);
        Assert.Equal("ani-cli-frieren-s1-e5-1080p", decoded.Id);
        Assert.Equal(1, decoded.Season);
        Assert.Equal(5, decoded.Episode);

        XNamespace newznabNs = "http://www.newznab.com/DTD/2010/feeds/attributes/";
        var attrs = item.Elements(newznabNs + "attr").ToList();
        Assert.NotEmpty(attrs);
        
        var catAttr = attrs.FirstOrDefault(a => a.Attribute("name")?.Value == "category");
        Assert.NotNull(catAttr);
        Assert.Equal("5070", catAttr.Attribute("value")?.Value);

        var sizeAttr = attrs.FirstOrDefault(a => a.Attribute("name")?.Value == "size");
        Assert.NotNull(sizeAttr);
        Assert.Equal("1073741824", sizeAttr.Attribute("value")?.Value);

        var seasonAttr = attrs.FirstOrDefault(a => a.Attribute("name")?.Value == "season");
        Assert.NotNull(seasonAttr);
        Assert.Equal("1", seasonAttr.Attribute("value")?.Value);

        var epAttr = attrs.FirstOrDefault(a => a.Attribute("name")?.Value == "episode");
        Assert.NotNull(epAttr);
        Assert.Equal("5", epAttr.Attribute("value")?.Value);

        // Ensure torrent-specific fields are NOT in the XML
        var seedersAttr = attrs.FirstOrDefault(a => a.Attribute("name")?.Value == "seeders");
        Assert.Null(seedersAttr);
    }

    [Fact]
    public void TestErrorXml()
    {
        // Act
        var xmlString = Newznab.GetErrorXml(100, "Incorrect user credentials");
        var doc = XDocument.Parse(xmlString);

        // Assert
        Assert.NotNull(doc.Root);
        Assert.Equal("error", doc.Root.Name.LocalName);
        Assert.Equal("100", doc.Root.Attribute("code")?.Value);
        Assert.Equal("Incorrect user credentials", doc.Root.Attribute("description")?.Value);
    }

    [Fact]
    public async Task TestAniListScraperSearch()
    {
        using var client = new HttpClient();
        var scraper = new AniListScraper(client);

        var results = await scraper.SearchAsync("Witch Hat Atelier", 1, 1);

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Title.Contains("Witch Hat Atelier", System.StringComparison.OrdinalIgnoreCase) ||
                                     r.Title.Contains("Tongari Boushi no Atelier", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TestAniListScraperRssFeed()
    {
        using var client = new HttpClient();
        var scraper = new AniListScraper(client);

        // Empty query triggers RSS feed mode
        var results = await scraper.SearchAsync("", null, null);

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(5070, r.Category));
    }

    [Fact]
    public async Task TestAnimeIdResolverTvdbMapping()
    {
        using var client = new HttpClient();
        var resolver = new AnimeIdResolver(client);

        // Frieren TVDB ID is 424536 in Fribb map
        var title = await resolver.ResolveTvdbIdAsync("424536");

        Assert.NotNull(title);
        Assert.True(title.Contains("Frieren", System.StringComparison.OrdinalIgnoreCase) ||
                    title.Contains("Sousou", System.StringComparison.OrdinalIgnoreCase));
    }
}
