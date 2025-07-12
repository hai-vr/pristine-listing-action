using System.ComponentModel;

namespace Hai.PristineListing;

public class PLInput
{
    public PLInputListingData listingData;
    public PLSettings settings;
    public List<PLProduct> products;
}

public class PLSettings
{
    [DefaultValue(true)]
    public bool defaultIncludePrereleases;
    [DefaultValue(true)]
    public bool excessiveModeToleratesPackageJsonAssetMissing;
    [DefaultValue(1)] // PackageJsonAssetOnly
    public PLMode defaultMode;
}

public class PLInputListingData
{
    public string name;
    public string author;
    public string url;
    public string id;
}

public class PLProduct
{
    public string repository;
    [DefaultValue(null)]
    public bool? includePrereleases;
    [DefaultValue(0)]
    public PLMode? mode;

    public List<string>? onlyPackageNames;
}

public enum PLMode
{
    Undefined = 0,
    PackageJsonAssetOnly = 1,
    ExcessiveWhenNeeded = 2,
    ExcessiveAlways = 3
}