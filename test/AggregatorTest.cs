using System.Net;
using Hai.PristineListing.Aggregator;
using Hai.PristineListing.Core;
using Moq;
using Moq.Protected;
using Newtonsoft.Json.Linq;
using Shouldly;

namespace Hai.PristineListing.Tests;

public class AggregatorTest
{
    private PLAggregator _sut;
    private Mock<HttpMessageHandler> _handlerMock;

    [SetUp]
    public void SetUp()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        // TODO: Can we just mock HttpClient? Not doing it right now
        // because apparently non-virtual methods cannot be mocked or something
        var httpClient = new HttpClient(_handlerMock.Object);
        _sut = new PLAggregator(httpClient);
    }
    
    [Test]
    public async Task It_should_request_listing_and_aggregate_results()
    {
        // Given
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
"""
{
    "name": "Name",
    "author": "Author",
    "url": "https://this-is-a-different-url.example.com/other-listing.json",
    "id": "com.example.listing",
    "packages": {
        "com.example.package": {
            "versions": {
                "1.0.0": {
                    "name": "com.example.package",
                    "version": "1.0.0",
                    "displayName" : "Display name"
                }
            }
        }
    }
}
""")
        };
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(response)
            .Verifiable();

        
        // When
        PLCoreAggregation result = await _sut.DownloadAndAggregate(new PLCoreInput
        {
            listingData = new PLCoreInputListingData(),
            settings = new PLCoreInputSettings(),
            products = [],
            aggregateListings = [
                new PLCoreAggregateListing
                {
                    listingUrl = "https://example.com/other-listing.json"
                }            
            ]
        });
        
        // Then
        _handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.RequestUri == new Uri("https://example.com/other-listing.json")),
            ItExpr.IsAny<CancellationToken>()
        );
        
        // Shouldly.ShouldBeEquivalentTo does not handle nested dictionaries.
        if (false)
        {
            var expected = new PLCoreAggregation
            {
                aggregated =
                [
                    new PLCoreAggregationListing
                    {
                        name = "Name",
                        author = "Author",
                        url = "https://this-is-a-different-url.example.com/other-listing.json",
                        id = "com.example.listing",
                        listingUrl = "https://example.com/other-listing.json",
                        packages = new Dictionary<string, PLCoreAggregationPackage>
                        {
                            {
                                "com.example.package", new PLCoreAggregationPackage
                                {
                                    versions = new Dictionary<string, PLCoreAggregationVersion>
                                    {
                                        {
                                            "1.0.0", new PLCoreAggregationVersion
                                            {
                                                data = new JObject
                                                {
                                                    { "name", JToken.FromObject("com.example.package") },
                                                    { "version", "1.0.0" },
                                                    { "displayName", "Display name" }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                ]
            };
            result.ShouldBeEquivalentTo(expected);
        }
        result.aggregated.Count.ShouldBe(1);
        var itemAggregation = result.aggregated[0];
        itemAggregation.name.ShouldBe("Name");
        itemAggregation.author.ShouldBe("Author");
        itemAggregation.url.ShouldBe("https://this-is-a-different-url.example.com/other-listing.json");
        itemAggregation.id.ShouldBe("com.example.listing");
        itemAggregation.listingUrl.ShouldBe("https://example.com/other-listing.json");
        itemAggregation.packages.Count.ShouldBe(1);
        itemAggregation.packages.ShouldContainKey("com.example.package");
        var itemPackage = itemAggregation.packages["com.example.package"];
        itemPackage.versions.Count.ShouldBe(1);
        itemPackage.versions.ShouldContainKey("1.0.0");
        var itemVersion = itemPackage.versions["1.0.0"];
        itemVersion.data.ShouldNotBeNull();
        itemVersion.data["name"].Value<string>().ShouldBe("com.example.package");
        itemVersion.data["version"].Value<string>().ShouldBe("1.0.0");
        itemVersion.data["displayName"].Value<string>().ShouldBe("Display name");
    }
}