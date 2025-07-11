using System.Data;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Hai.PristineListing;

internal class PLGatherer
{
    private const string ReleaseAsset = "assets";
    private const string AssetName = "name";
    private const string PackageJsonAuthorName = "name";
    private const string PackageName = "name";
    private const string HiddenBodyTag = @"$\texttt{Hidden}$";
    private const string PackageJsonFilename = "package.json";
    
    private readonly HttpClient _http;
    private readonly bool _excessiveMode;

    internal PLGatherer(string githubToken, bool excessiveMode)
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("pristine-listing-action", "1.0.0"));
        _excessiveMode = excessiveMode;
    }

    internal async Task<PLOutputListing> DownloadAndAggregate(PLInput input)
    {
        var outputListing = NewOutputListing(input.listingData);

        var packages = await AsPackages(input.products);
        outputListing.packages = packages
            .Where(package => package.versions.Count > 0)
            // NOTE: This doesn't support multiple repositories adding to the same package name.
            // This is generally not a problem as we're aggregating from repositories that we have ownership of,
            // however if there is ever a case where there's a need to aggregate from multiple repositories
            // owned by different people, then this case might crop up.
            .ToDictionary(package => package.versions.First().Value.name);

        return outputListing;
    }

    private static PLOutputListing NewOutputListing(PLInputListingData listingData)
    {
        return new PLOutputListing
        {
            name = listingData.name,
            author = listingData.author,
            url = listingData.url,
            id = listingData.id,
            packages = new Dictionary<string, PLPackage>()
        };
    }

    private async Task<PLPackage[]> AsPackages(List<PLProduct> products)
    {
        var cts = new CancellationTokenSource();
        try
        {
            var results = await Task.WhenAll(products.Select(product => NavigatePackage(product.repository, cts)));
            return results;
        }
        catch (Exception)
        {
            await cts.CancelAsync();
            throw;
        }
        finally
        {
            cts.Dispose();
        }
    }

    private async Task<PLPackage> NavigatePackage(string repository, CancellationTokenSource source)
    {
        source.Token.ThrowIfCancellationRequested();

        try
        {
            ParseRepository(repository, out var owner, out var repo);

            var releasesUns = await FetchReleaseDataUnstructured(owner, repo, source);

            var relevantReleases = releasesUns
                .Where(ThereAreAssets)
                .Where(AtLeastOneOfTheAssetsIsPackageJson)
                .Where(IfThereIsABodyThenItDoesNotContainTheHiddenTag)
                .ToList();

            var package = new PLPackage
            {
                versions = new Dictionary<string, PLPackageVersion>()
            };

            // An assumption is made that relevantReleases (and by extension releasesUns) is already stably sorted
            // by version from more recent versions to older versions.
            // Unsure how much impact this may affect repository listing client applications (e.g. VPM Catalog, ALCOM, ...),
            // but we do make the assumption in the Outputter that the most recent package is the first in the versions list.
            var packageVersions = await Task.WhenAll(relevantReleases.Select(releaseUns => DownloadAndCompilePackage(source, releaseUns)));
            foreach (var packageVersion in packageVersions)
            {
                package.versions.Add(packageVersion.version, packageVersion);
            }

            return package;
        }
        catch (Exception)
        {
            await source.CancelAsync();
            throw;
        }
    }

    private static bool ThereAreAssets(JToken releaseUns)
    {
        return releaseUns[ReleaseAsset] != null;
    }

    private static bool AtLeastOneOfTheAssetsIsPackageJson(JToken releaseUns)
    {
        return releaseUns[ReleaseAsset].Any(asset => asset[AssetName].Value<string>() == PackageJsonFilename);
    }

    private static bool IfThereIsABodyThenItDoesNotContainTheHiddenTag(JToken releaseUns)
    {
        return releaseUns["body"] == null || !releaseUns["body"].Value<string>().Contains(HiddenBodyTag);
    }

    private async Task<PLPackageVersion> DownloadAndCompilePackage(CancellationTokenSource source, JToken releaseUns)
    {
        // This will download the ZIP file of the release, in order to calculate "zipSHA256". This isn't really used.
        if (_excessiveMode) return await ExcessiveMode(source, releaseUns);
        
        // This will download the package.json asset of the release. The "zipSHA256" value won't be able to be calculated.
        else return await LighterMode(source, releaseUns);
    }

    private async Task<PLPackageVersion> ExcessiveMode(CancellationTokenSource source, JToken releaseUns)
    {
        var zipAsset = releaseUns[ReleaseAsset].First(IsAssetThatZipFile);
        var downloadCount = zipAsset["download_count"].Value<int>();
        var downloadUrl = zipAsset["browser_download_url"].Value<string>();
        var intermediary = await DownloadZip(downloadUrl, source);

        return ToPackage(intermediary, downloadCount, downloadUrl);
    }

    private async Task<PLPackageVersion> LighterMode(CancellationTokenSource source, JToken releaseUns)
    {
        var zipAsset = releaseUns[ReleaseAsset].First(IsAssetThatZipFile);
        var downloadCount = zipAsset["download_count"].Value<int>();
        var downloadUrl = zipAsset["browser_download_url"].Value<string>();

        var packageJsonAsset = releaseUns[ReleaseAsset].First(IsAssetThatPackageJsonFile);
        var packageJsonUrl = packageJsonAsset["browser_download_url"].Value<string>();
        var intermediary = await DownloadPackageJson(packageJsonUrl, source);

        return ToPackage(intermediary, downloadCount, downloadUrl);
    }

    private PLPackageVersion ToPackage(PLIntermediary intermediary, int downloadCount, string downloadUrl)
    {
        var package = JObject.Parse(intermediary.packageJson);

        return new PLPackageVersion
        {
            name = package[PackageName].Value<string>(),
            displayName = package["displayName"].Value<string>(),
            version = package["version"].Value<string>(), // Was formerly: (string)releaseUns[ReleaseTagName]
            unity = package["unity"]?.Value<string>(),
            description = package["description"]?.Value<string>(),
            dependencies = AsDictionary(package["dependencies"]?.Value<JObject>()),
            vpmDependencies = AsDictionary(package["vpmDependencies"]?.Value<JObject>()),
            author = ExtractAuthorUnionField(package["author"]),
            url = downloadUrl,
            documentationUrl = package["documentationUrl"]?.Value<string>(),
            changelogUrl = package["changelogUrl"]?.Value<string>(),
            license = package["license"]?.Value<string>(),
            zipSHA256 = intermediary.hashHexNullableIfJson,
            
            vrchatVersion = package["vrchatVersion"]?.Value<string>(),
            legacyFolders = AsDictionary(package["legacyFolders"]?.Value<JObject>()),
            
            downloadCount = downloadCount
        };
    }

    private static PLAuthor ExtractAuthorUnionField(JToken authorToken)
    {
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        return authorToken.Type switch
        {
            JTokenType.Object => ExtractAuthorObject(authorToken.Value<JObject>()),
            JTokenType.String => new PLAuthor { name = authorToken.Value<string>() },
            _  => throw new DataException("Can't deserialize author from package.json")
        };
    }

    private static PLAuthor ExtractAuthorObject(JObject authorObject)
    {
        return new PLAuthor
        {
            name = authorObject[PackageJsonAuthorName].Value<string>(),
            email = authorObject["email"]?.Value<string>(),
            url = authorObject["url"]?.Value<string>(),
        };
    }

    private Dictionary<string, string> AsDictionary(JObject obj)
    {
        if (obj == null) return null;
        
        var legacyFolders = new Dictionary<string, string>();
        foreach (var keyValuePair in obj)
        {
            legacyFolders[keyValuePair.Key] = keyValuePair.Value.Value<string>();
        }

        return legacyFolders;
    }

    private static bool IsAssetThatZipFile(JToken assetUns)
    {
        return assetUns["content_type"].Value<string>() == "application/zip" && assetUns[AssetName].Value<string>().ToLowerInvariant().EndsWith(".zip");
    }

    private static bool IsAssetThatPackageJsonFile(JToken assetUns)
    {
        return assetUns["content_type"].Value<string>() == "application/json" && assetUns[AssetName].Value<string>().ToLowerInvariant() == PackageJsonFilename;
    }

    private async Task<PLIntermediary> DownloadZip(string zipUrl, CancellationTokenSource source)
    {
        Console.WriteLine($"Downloading zip {zipUrl}...");
        
        var request = new HttpRequestMessage(HttpMethod.Get, zipUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

        var response = await _http.SendAsync(request, source.Token);
        if (!response.IsSuccessStatusCode) throw new InvalidDataException($"Did not receive a valid response from GitHub: {response.StatusCode}");

        var data = await response.Content.ReadAsByteArrayAsync();
        
        var hashBytes = SHA256.HashData(data);
        var hashHex = Convert.ToHexStringLower(hashBytes);

        var packageJson = Unzip(data);
        return new PLIntermediary
        {
            hashHexNullableIfJson = hashHex,
            packageJson = packageJson
        };
    }

    private async Task<PLIntermediary> DownloadPackageJson(string packageJsonUrl, CancellationTokenSource source)
    {
        Console.WriteLine($"Downloading packageJson {packageJsonUrl}...");
        
        var request = new HttpRequestMessage(HttpMethod.Get, packageJsonUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _http.SendAsync(request, source.Token);
        if (!response.IsSuccessStatusCode) throw new InvalidDataException($"Did not receive a valid response from GitHub: {response.StatusCode}");

        var packageJson = await response.Content.ReadAsStringAsync();

        return new PLIntermediary
        {
            hashHexNullableIfJson = null,
            packageJson = packageJson
        };
    }

    private string Unzip(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var entry = archive.GetEntry(PackageJsonFilename);
        if (entry == null) throw new FileNotFoundException("Missing package.json from .zip. We should not unzip in the first place if it wasn't a package to begin with.");

        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private async Task<List<JToken>> FetchReleaseDataUnstructured(string owner, string repo, CancellationTokenSource source)
    {
        var releasesUns = new List<JToken>();
        
        int requested = 30; // Max is 100
        var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases?per_page={requested}";

        var iteration = 0;
        bool hasNext;
        do
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            Console.WriteLine($"Getting release data at {apiUrl}... (iteration #{iteration})");

            var response = await _http.SendAsync(request, source.Token);
            if (!response.IsSuccessStatusCode) throw new InvalidDataException($"Did not receive a valid response from GitHub: {response.StatusCode}");

            var responseStr = await response.Content.ReadAsStringAsync(source.Token);
            var releases = JArray.Parse(responseStr);

            foreach (var releaseUns in releases)
            {
                releasesUns.Add(releaseUns);
            }

            if (response.Headers.TryGetValues("Link", out var linkValues))
            {
                var linkHeader = linkValues.First();
                hasNext = TryParseNextLink(linkHeader, out apiUrl);
            }
            else
            {
                hasNext = false;
            }

            iteration++;

        } while (hasNext);
        
        return releasesUns;
    }

    private bool TryParseNextLink(string linkHeader, out string result)
    {
        if (string.IsNullOrEmpty(linkHeader))
        {
            result = null;
            return false;
        }

        var lines = linkHeader.Split(',');
        foreach (var line in lines)
        {
            var parts = line.Split(';');
            if (parts.Length < 2) continue;

            var url = parts[0].Trim().Trim('<', '>');
            var rel = parts[1].Trim();

            if (rel == "rel=\"next\"")
            {
                result = url;
                return true;
            }
        }

        result = null;
        return false;
    }

    private static void ParseRepository(string repository, out string owner, out string repo)
    {
        var split = repository.Split("/");
        if (split.Length != 2) throw new ArgumentException($"Invalid repository name {repository}");
    
        owner = split[0];
        repo = split[1];
    }
}