using System.Data;
using System.Net;
using Hai.PristineListing.Core;
using Hai.PristineListing.Gatherer;
using Moq;
using Moq.Protected;
using Shouldly;

namespace Hai.PristineListing.Tests;

public class GathererTest
{
    private PLGatherer _sut;
    private Mock<HttpMessageHandler> _handlerMock;

    [SetUp]
    public void SetUp()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        // TODO: Can we just mock HttpClient? Not doing it right now
        // because apparently non-virtual methods cannot be mocked or something
        var httpClient = new HttpClient(_handlerMock.Object);
        _sut = new PLGatherer("myGithubToken", httpClient);
    }
    
    [Test]
    public async Task It_should_request_releases_and_throw_exception_when_repository_has_no_packages()
    {
        // Given
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]")
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
        try
        {
            PLCoreOutputListing result = await _sut.DownloadAndAggregate(new PLCoreInput
            {
                listingData = new PLCoreInputListingData
                {
                    id = "Id",
                    author = "Author",
                    name = "Name",
                    url = "https://example.com/index.json"
                },
                settings = new PLCoreInputSettings
                {
                    excessiveModeToleratesPackageJsonAssetMissing = false
                },
                products =
                [
                    new PLCoreInputProduct
                    {
                        repository = "test/our-repository",
                        mode = PLCoreInputMode.PackageJsonAssetOnly,
                        includePrereleases = true,
                        onlyPackageNames = null
                    }
                ]
            });
            
            Assert.Fail("Should have thrown an exception");
        }
        catch (DataException e)
        {
            e.Message.ShouldBe("No packages found in test/our-repository, this is not normal. Aborting");
        }
        
        // Then
        _handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.RequestUri == new Uri("https://api.github.com/repos/test/our-repository/releases?per_page=100")),
            ItExpr.IsAny<CancellationToken>()
        );
        // result.ShouldBeEquivalentTo(new PLCoreOutputListing
        // {
        //     id = "Id",
        //     author = "Author",
        //     name = "Name",
        //     url = "https://example.com/index.json",
        //     packages = new Dictionary<string, PLCoreOutputPackage>()
        // });
    }
}