using Newtonsoft.Json.Linq;
using Semver;

namespace Hai.PristineListing.Core;

public class PLCoreAggregation
{
    public List<PLCoreAggregationListing> aggregated;
}

public class PLCoreAggregationListing
{
    public string name;
    public string author;
    public string url;
    public string id;
    public Dictionary<string, PLCoreAggregationPackage> packages;

    public string listingUrl;
}

public class PLCoreAggregationPackage
{
    public Dictionary<string, PLCoreAggregationVersion> versions;
}

public class PLCoreAggregationVersion
{
    public JObject data;
    
    internal SemVersion semver;
}