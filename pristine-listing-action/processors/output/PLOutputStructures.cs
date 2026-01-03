using System.Text.Json.Serialization;
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
    // Do not change the order of properties, it makes it easier to diff them when serialized.
    public string name;
    public string displayName;
    public string version;
    public string unity;
    public string description;
    public List<string> keywords; /* Rarely used, position does not matter. */
    public Dictionary<string, string> dependencies;
    public Dictionary<string, string> vpmDependencies;
    public List<PLOutputSample> samples;
    public string changelogUrl;
    public object author;
    public string documentationUrl;
    public string license;
    public string licensesUrl; /* Rarely used, position does not matter. */
    [Obsolete] public string vrchatVersion;
    public string zipSHA256;
    public bool? hideInEditor; /* Rarely used, position does not matter. */
    public string unityRelease; /* Rarely used, position does not matter. */
    public string url;
    public Dictionary<string, string> legacyFolders;
    public Dictionary<string, string> legacyFiles;
    public List<string> legacyPackages;
    
    [JsonPropertyName("vrc-get")]
    public PLOutputVrcGet? vrcGet;

    internal static PLOutputPackageVersion FromCore(PLCoreOutputPackageVersion version)
    {
        var upmManifest = version.upmManifest;
        return new PLOutputPackageVersion
        {
            name = upmManifest.name,
            displayName = upmManifest.displayName,
            version = upmManifest.version,
            unity = upmManifest.unity,
            description = upmManifest.description,
            dependencies = upmManifest.dependencies,
            vpmDependencies = version.vpmConvention.vpmDependencies,
            samples = upmManifest.samples != null ? upmManifest.samples.Select(PLOutputSample.FromCore).ToList() : null,
            changelogUrl = upmManifest.changelogUrl,
            author = upmManifest.author != null
                ? upmManifest.author.Kind == PLCoreOutputAuthorKind.Object ? PLOutputAuthorObject.FromCore(upmManifest.author.AsObject()) : upmManifest.author.AsString()
                : null,
            documentationUrl = upmManifest.documentationUrl,
            license = upmManifest.license,
            vrchatVersion = version.vrcConvention.vrchatVersion,
            zipSHA256 = version.listingConvention.zipSHA256,
            url = version.listingConvention.url,
            legacyFolders = version.vrcConvention.legacyFolders,
            legacyFiles = version.vrcConvention.legacyFiles,
            legacyPackages = version.vrcConvention.legacyPackages,
            hideInEditor = upmManifest.hideInEditor,
            keywords = upmManifest.keywords,
            licensesUrl = upmManifest.licensesUrl,
            unityRelease = upmManifest.unityRelease,
            vrcGet = version.alcomConvention != null ? PLOutputVrcGet.FromCore(version.alcomConvention) : null,
        };
    }
}

internal class PLOutputAuthorObject
{
    public string name;
    public string email;
    public string url;

    public static PLOutputAuthorObject FromCore(PLCoreOutputAuthorObject outputAuthorObject)
    {
        return new PLOutputAuthorObject
        {
            name = outputAuthorObject.name,
            email = outputAuthorObject.email,
            url = outputAuthorObject.url
        };
    }
}

internal class PLOutputSample
{
    public string displayName;
    public string description;
    public string path;

    public static PLOutputSample FromCore(PLCoreOutputSample outputSample)
    {
        return new PLOutputSample
        {
            displayName = outputSample.displayName,
            description = outputSample.description,
            path = outputSample.path
        };
    }
}

internal class PLOutputVrcGet
{
    public object? yanked;

    public static PLOutputVrcGet FromCore(PLCoreOutputPackageALCOMConvention convention)
    {
        var result = new PLOutputVrcGet();
        if (convention.yanked is PLCoreOutputALCOMYanked yanked)
        {
            result.yanked = yanked.Kind == PLCoreOutputALCOMYankedKind.Bool ? yanked.AsBool() : yanked.AsString();
        }
        return result;
    }
}