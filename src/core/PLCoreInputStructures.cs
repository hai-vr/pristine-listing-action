namespace Hai.PristineListing.Core;

public class PLCoreInput
{
    public PLCoreInputListingData listingData;
    public PLCoreInputSettings settings;
    public List<PLCoreInputProduct> products;
}

public class PLCoreInputSettings
{
    public bool excessiveModeToleratesPackageJsonAssetMissing;
}

public class PLCoreInputListingData
{
    public string name;
    public string author;
    public string url;
    public string id;
}

public class PLCoreInputProduct
{
    public string repository;
    public bool includePrereleases;
    public PLCoreInputMode mode;
    public List<string> onlyPackageNames;
}

public enum PLCoreInputMode
{
    PackageJsonAssetOnly = 1,
    ExcessiveWhenNeeded = 2,
    ExcessiveAlways = 3
}