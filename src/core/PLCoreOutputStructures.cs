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

    internal int totalDownloadCount;
    internal string repositoryUrl;
}

public class PLCoreOutputPackageVersion
{
    public PLCoreOutputPackageUPMSpecification upmManifest;
    
    // VPM properties
    /*VPM*/ public Dictionary<string, string> vpmDependencies;
    
    // VRC properties
    /*VRC*/ public string vrchatVersion;
    /*VRC*/ public Dictionary<string, string> legacyFolders;
    /*VRC*/ public List<string> legacyPackages;
    
    // Listing-only properties
    /*LIST*/ public string url;
    /*VCC*/ public string zipSHA256;

    // Metadata
    internal int downloadCount;
    internal SemVersion semver;
    internal string? unitypackageUrl;
    internal int? unitypackageDownloadCount;
}

public class PLCoreOutputPackageUPMSpecification
{
    // Required properties
    /*REQ*/ public string name;
    /*REQ*/ public string version;
    
    // Recommended properties
    /*rcm*/ public string description;
    /*rcm*/ public string displayName;
    /*rcm*/ public string unity;
    
    // Optional properties
    /*opt*/ public PLCoreOutputAuthor author;
    /*opt*/ public string changelogUrl;
    /*opt*/ public Dictionary<string, string> dependencies;
    /*opt*/ public List<PLCoreOutputSample> samples;
    /*opt*/ public string documentationUrl;
    /*opt*/ public string license;

    /*opt*/ public bool? hideInEditor;
    /*opt*/ public List<string> keywords;
    /*opt*/ public string licensesUrl;
    /*opt*/ public string unityRelease;
}

public class PLCoreOutputAuthor
{
    public PLCoreOutputAuthorKind Kind { get; private init; }
    private string _stringForm;
    private PLCoreOutputAuthorObject _objectForm;

    private PLCoreOutputAuthor() {}

    public static PLCoreOutputAuthor FromString(string str)
    {
        return new PLCoreOutputAuthor
        {
            Kind = PLCoreOutputAuthorKind.String,
            _stringForm = str
        };
    }

    public static PLCoreOutputAuthor FromObject(string name, string email, string url)
    {
        return new PLCoreOutputAuthor
        {
            Kind = PLCoreOutputAuthorKind.Object,
            _objectForm = new PLCoreOutputAuthorObject
            {
                name = name,
                email = email,
                url = url
            }
        };
    }

    public string AsString()
    {
        if (Kind != PLCoreOutputAuthorKind.String) throw new InvalidCastException();
        return _stringForm;
    }

    public PLCoreOutputAuthorObject AsObject()
    {
        if (Kind != PLCoreOutputAuthorKind.Object) throw new InvalidCastException();
        return _objectForm;
    }
}

public enum PLCoreOutputAuthorKind
{
    String,
    Object
}

public class PLCoreOutputAuthorObject
{
    /*REQ*/ public string name;
    public string email;
    public string url;
}

public class PLCoreOutputSample
{
    public string displayName;
    public string description;
    public string path;
}