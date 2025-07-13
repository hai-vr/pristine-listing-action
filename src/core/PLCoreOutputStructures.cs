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
    public List<PLCoreOutputSample> samples;
    public string changelogUrl;
    public PLCoreOutputAuthor author;
    public string documentationUrl;
    public string license;
    public string vrchatVersion; // VRC-specific
    public string zipSHA256;
    public string url;
    public Dictionary<string, string> legacyFolders; // VRC-specific
    public List<string> legacyPackages; // VRC-specific

    internal int downloadCount; // This is internal so that it doesn't get serialized to Json
    internal SemVersion semver;
    internal string? unitypackageUrl;
    internal int? unitypackageDownloadCount;
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