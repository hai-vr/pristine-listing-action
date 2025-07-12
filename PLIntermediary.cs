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
    
    // TODO: For future expansion: We may have a mode where we use non-excessive mode when possible,
    // but excessive mode when necessary (i.e. releases with no package.json). 
    public bool useExcessiveMode;
    
    public string urlOfPackageDownloadToStore;
    public PLUnitypackageIntermediary unityPackageNullable;
}