using System.Net.Http.Headers;
using Hai.PristineListing.Core;
using Newtonsoft.Json.Linq;
using Semver;

namespace Hai.PristineListing.Aggregator;

public class PLAggregator
{
    private readonly HttpClient _http;
    
    public PLAggregator() : this(new HttpClient()) { }
    
    public PLAggregator(HttpClient httpClient)
    {
        _http = httpClient;
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("pristine-listing-action", "1.0.0"));
    }
    
    public virtual async Task<PLCoreAggregation> DownloadAndAggregate(PLCoreInput input)
    {
        var results = await Task.WhenAll(input.aggregateListings.Select(Resolve));
        return new PLCoreAggregation
        {
            aggregated = results.ToList()
        };
    }

    private async Task<PLCoreAggregationListing> Resolve(PLCoreAggregateListing aggregateListing)
    {
        Console.WriteLine($"Getting listing at {aggregateListing.listingUrl}...");
        var json = await _http.GetStringAsync(aggregateListing.listingUrl);
        var response = JObject.Parse(json);

        var corePackages = new Dictionary<string, PLCoreAggregationPackage>();
        var packages = response["packages"].Value<JObject>();
        foreach (var packageEntry in packages)
        {
            var packageName = packageEntry.Key;
            var packageVersions = packageEntry.Value["versions"].Value<JObject>();
            
            var coreVersions = new Dictionary<string, PLCoreAggregationVersion>();
            foreach (var packageVersion in packageVersions)
            {
                var versionNumber = packageVersion.Key;
                coreVersions.Add(versionNumber, new PLCoreAggregationVersion
                {
                    data = packageVersion.Value.Value<JObject>(),
                    semver = SemVersion.Parse(versionNumber, SemVersionStyles.Any)
                });
            }

            corePackages.Add(packageName, new PLCoreAggregationPackage
            {
                versions = coreVersions
            });
        }
        
        return new PLCoreAggregationListing
        {
            name = response.Value<string>("name"),
            author = response.Value<string>("author"),
            url = response.Value<string>("url"),
            id = response.Value<string>("id"),
            
            packages = corePackages,
            
            listingUrl = aggregateListing.listingUrl
        };
    }
}