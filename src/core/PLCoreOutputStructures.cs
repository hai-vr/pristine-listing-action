using Semver;

namespace Hai.PristineListing.Core;

public class PLCoreOutputListing
{
    public string name;
    public string author;
    public string url;
    public string id;
    public Dictionary<string, PLCoreOutputPackage> packages;
}

public class PLCoreOutputPackage
{
    public Dictionary<string, PLCoreOutputPackageVersion> versions;

    internal int totalDownloadCount; // This is internal so that it doesn't get serialized to Json
    internal string repositoryUrl;
}

public class PLCoreOutputPackageVersion
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
    public PLCoreOutputAuthor author;
    public string documentationUrl;
    public string license;
    public string vrchatVersion; // VRC-specific
    public string zipSHA256;
    public string url;
    public Dictionary<string, string> legacyFolders; // VRC-specific

    internal int downloadCount; // This is internal so that it doesn't get serialized to Json
    internal SemVersion semver;
    internal string? unitypackageUrl;
    internal int? unitypackageDownloadCount;
}

public class PLCoreOutputAuthor
{
    public string name;
    public string email;
    public string url;
}

public class PLCoreOutputSample
{
    public string displayName;
    public string description;
    public string path;
}