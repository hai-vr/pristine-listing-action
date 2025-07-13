using Hai.PristineListing.Core;

namespace Hai.PristineListing.Outputter;

internal class PLOutputListing
{
    public string name;
    public string author;
    public string url;
    public string id;
    public Dictionary<string, PLOutputPackage> packages;

    public static PLOutputListing FromCore(PLCoreOutputListing outputListing)
    {
        return new PLOutputListing
        {
            name = outputListing.name,
            author = outputListing.author,
            url = outputListing.url,
            id = outputListing.id,
            packages = outputListing.packages
                .ToDictionary(packageKv => packageKv.Key, packageKv => PLOutputPackage.FromCore(packageKv.Value))
        };
    }
}

internal class PLOutputPackage
{
    public Dictionary<string, PLOutputPackageVersion> versions;

    internal static PLOutputPackage FromCore(PLCoreOutputPackage outputPackage)
    {
        return new PLOutputPackage
        {
            versions = outputPackage.versions
                .ToDictionary(versionKv => versionKv.Key, versionKv => PLOutputPackageVersion.FromCore(versionKv.Value))
        };
    }
}

internal class PLOutputPackageVersion
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
    public PLOutputAuthor author;
    public string documentationUrl;
    public string license;
    public string vrchatVersion; // VRC-specific
    public string zipSHA256;
    public string url;
    public Dictionary<string, string> legacyFolders; // VRC-specific
    public List<string> legacyPackages; // VRC-specific

    internal static PLOutputPackageVersion FromCore(PLCoreOutputPackageVersion version)
    {
        return new PLOutputPackageVersion
        {
            name = version.name,
            displayName = version.displayName,
            version = version.version,
            unity = version.unity,
            description = version.description,
            dependencies = version.dependencies,
            vpmDependencies = version.vpmDependencies,
            // samples = ...
            changelogUrl = version.changelogUrl,
            author = new PLOutputAuthor
            {
                name = version.author.name,
                email = version.author.email,
                url = version.author.url,
            },
            documentationUrl = version.documentationUrl,
            license = version.license,
            vrchatVersion = version.vrchatVersion,
            zipSHA256 = version.zipSHA256,
            url = version.url,
            legacyFolders = version.legacyFolders,
            legacyPackages = version.legacyPackages
        };
    }
}

internal class PLOutputAuthor
{
    public string name;
    public string email;
    public string url;
}

internal class PLOutputSample
{
    public string displayName;
    public string description;
    public string path;
}