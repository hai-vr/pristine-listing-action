namespace Hai.PristineListing;

public class PLOutputListing
{
    public string name;
    public string author;
    public string url;
    public string id;
    public Dictionary<string, PLPackage> packages;
}

public class PLPackage
{
    public Dictionary<string, PLPackageVersion> versions;
}

public class PLPackageVersion
{
    public string name;
    public string displayName;
    public string version;
    public string unity;
    public string description;
    public Dictionary<string, string> dependencies;
    public Dictionary<string, string> vpmDependencies;
    public PLAuthor author;
    public string url;
    public string documentationUrl;
    public string changelogUrl;
    public string license;
    public string zipSHA256;
    
    // VRC-specific
    public string vrchatVersion;
    public Dictionary<string, string> legacyFolders;
}

public class PLAuthor
{
    public string name;
}