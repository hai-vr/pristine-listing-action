using Hai.PristineListing.Core;

namespace Hai.PristineListing.Gatherer;

internal class PLPackageVersionFetchResult
{
    public bool success;
    public PLCoreOutputPackageVersion version;
}

internal class PLIntermediary
{
    public bool success;
    public string hashHexNullableIfJson;
    public string packageJson;
}

internal class PLUnitypackageIntermediary
{
    public string downloadUrl;
    public int downloadCount;
}

internal class PLPackageToFetch
{
    public string urlOfDataToFetch; // May be package.json
    public int downloadCountOfActualPayload; // Always refers to the zip itself
    
    public bool useExcessiveMode;
    
    public string urlOfPackageDownloadToStore;
    public PLUnitypackageIntermediary unityPackageNullable;
}