using Hai.PristineListing.Core;

namespace Hai.PristineListing.Modifier;

public class PLModifier
{
    private readonly bool _devOnly;

    public PLModifier(bool devOnly)
    {
        _devOnly = devOnly;
    }

    public virtual void Modify(PLCoreInput input, PLCoreOutputListing outputListing)
    {
        if (input.settings.includeDownloadCount)
        {
            foreach (var outputListingPackage in outputListing.packages.Values)
            {
                var totalDownloadCount = outputListingPackage.totalDownloadCount;
                foreach (var version in outputListingPackage.versions.Values)
                {
                    var description = version.upmManifest.description ?? "";
                    if (_devOnly)
                    {
                        version.upmManifest.displayName = $"{(version.upmManifest.displayName ?? "")} 🔽{version.downloadCount}/{totalDownloadCount}";
                    }
                    version.upmManifest.description = $"{description} (Downloaded {version.downloadCount} times)";
                }
            }
        }
    }
}