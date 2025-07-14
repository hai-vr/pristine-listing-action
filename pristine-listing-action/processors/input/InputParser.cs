using System.ComponentModel;
using Hai.PristineListing.Core;
using Newtonsoft.Json;

namespace Hai.PristineListing.Input;

public class InputParser
{
    public PLCoreInput Parse(string inputJson)
    {
        var input = JsonConvert.DeserializeObject<PLInput>(inputJson, new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.Populate
        });
        if (input.settings.defaultMode == PLInputMode.Undefined) input.settings.defaultMode = PLInputMode.PackageJsonAssetOnly;
        
        return new PLCoreInput
        {
            listingData = new PLCoreInputListingData
            {
                name = input.listingData.name,
                author = input.listingData.author,
                id = input.listingData.id,
                url = input.listingData.url,
            },
            settings = new PLCoreInputSettings
            {
                excessiveModeToleratesPackageJsonAssetMissing = input.settings.excessiveModeToleratesPackageJsonAssetMissing,
                includeDownloadCount = input.settings.includeDownloadCount,
                forceOutputAuthorAsObject = input.settings.forceOutputAuthorAsObject
            },
            products = input.products
                .Select(product => new PLCoreInputProduct
                {
                    repository = product.repository,
                    includePrereleases = product.includePrereleases ?? input.settings.defaultIncludePrereleases,
                    onlyPackageNames = product.onlyPackageNames ?? new List<string>(),
                    mode = product.mode is null or PLInputMode.Undefined ? (PLCoreInputMode)(int)input.settings.defaultMode : (PLCoreInputMode)(int)product.mode
                })
                .ToList()
        };
    }
}

internal class PLInput
{
    public PLInputListingData listingData;
    public PLInputSettings settings;
    public List<PLInputProduct> products;
}

internal class PLInputSettings
{
    [DefaultValue(true)]
    public bool defaultIncludePrereleases;
    [DefaultValue(1)] // PackageJsonAssetOnly
    public PLInputMode defaultMode;
    [DefaultValue(true)]
    public bool excessiveModeToleratesPackageJsonAssetMissing;
    [DefaultValue(false)]
    public bool includeDownloadCount;
    [DefaultValue(false)]
    public bool forceOutputAuthorAsObject;
}

internal class PLInputListingData
{
    public string name;
    public string author;
    public string url;
    public string id;
}

internal class PLInputProduct
{
    public string repository;
    [DefaultValue(null)]
    public bool? includePrereleases;
    [DefaultValue(0)]
    public PLInputMode? mode;

    public List<string>? onlyPackageNames;
}

internal enum PLInputMode
{
    Undefined = 0,
    PackageJsonAssetOnly = 1,
    ExcessiveWhenNeeded = 2,
    ExcessiveAlways = 3
}