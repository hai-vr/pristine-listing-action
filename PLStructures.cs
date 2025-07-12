using Semver;

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

    internal int totalDownloadCount; // This is internal so that it doesn't get serialized to Json
    internal string repositoryUrl;
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
    // public List<PLSamples> samples;
    public string changelogUrl;
    public PLAuthor author;
    public string documentationUrl;
    public string license;
    public string vrchatVersion; // VRC-specific
    public string zipSHA256;
    public string url;
    public Dictionary<string, string> legacyFolders; // VRC-specific

    internal int downloadCount; // This is internal so that it doesn't get serialized to Json
    internal SemVersion semver;
    internal string? unitypackageUrl;
}

public class PLAuthor
{
    public string name;
    public string email;
    public string url;
}

public class PLSample
{
    public string displayName;
    public string description;
    public string path;
}