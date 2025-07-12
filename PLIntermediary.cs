namespace Hai.PristineListing;

public class PLPackageVersionFetchResult
{
    public bool success;
    public PLPackageVersion version;
}

public class PLIntermediary
{
    public bool success;
    public string hashHexNullableIfJson;
    public string packageJson;
}

public class PLUnitypackageIntermediary
{
    public string downloadUrl;
}

internal class PLPackageToFetch
{
    public string urlOfDataToFetch; // May be package.json
    public int downloadCountOfActualPayload; // Always refers to the zip itself
    
    public bool useExcessiveMode;
    
    public string urlOfPackageDownloadToStore;
    public PLUnitypackageIntermediary unityPackageNullable;
}