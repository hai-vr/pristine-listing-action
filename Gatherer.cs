﻿using System.Data;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using Semver;

namespace Hai.PristineListing;

internal class PLGatherer
{
    private const string ReleaseAsset = "assets";
    private const string AssetName = "name";
    private const string PackageJsonAuthorName = "name";
    private const string PackageName = "name";
    private const string HiddenBodyTag = @"$\texttt{Hidden}$";
    private const string PackageJsonFilename = "package.json";
    private const string AssetBrowserDownloadUrl = "browser_download_url";
    private const string AssetDownloadCount = "download_count";

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

        var packages = await ResolvePackages(input.products, input.settings);
        outputListing.packages = packages
            // A package may have 0 versions if they're all prereleases and prereleases are not included in the
            // product configuration. In this case, remove the package altogether.
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

    private async Task<List<PLPackage>> ResolvePackages(List<PLProduct> products, PLSettings settings)
    {
        var cts = new CancellationTokenSource();
        try
        {
            var results = await Task.WhenAll(products.Select(product => ResolvePackage(product, settings, cts)));
            return results.SelectMany(it => it).ToList();
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

    private async Task<List<PLPackage>> ResolvePackage(PLProduct product, PLSettings settings, CancellationTokenSource source)
    {
        source.Token.ThrowIfCancellationRequested();

        try
        {
            ParseRepository(product.repository, out var owner, out var repo);

            // TODO: This fetches all the pages before fetching the package.json assets.
            // On some large repos, this can take a while.
            // It would be nice to get the package.json assets as soon as we fetch a page.
            var releasesUns = await FetchReleaseDataUnstructured(owner, repo, source);

            var filtered = releasesUns
                .Where(ThereAreAssets)
                .Where(AtLeastOneOfTheAssetsIsZipFile);
            
            if (!_excessiveMode || !settings.excessiveModeToleratesPackageJsonAssetMissing)
                filtered = filtered.Where(AtLeastOneOfTheAssetsIsPackageJson);
            
            var relevantReleases = filtered
                .Where(IfThereIsABodyThenItDoesNotContainTheHiddenTag)
                .ToList();
            
            var packageNameToPackage = new Dictionary<string, PLPackage>();

            // FIXME: Some repositories have multiple different packages within the same release.
            // When this may happen, the assets of that release has no package.json asset.
            // This means we need to unravel each release into separate ones.
            var itemsToFetch = SplitIntoWork(relevantReleases);
            
            var packageVersionFetchResults = await Task.WhenAll(itemsToFetch.Select(itemToFetch => DownloadAndCompilePackage(source, itemToFetch)));
            foreach (var packageVersionFetchResult in packageVersionFetchResults)
            {
                if (packageVersionFetchResult.success)
                {
                    var packageVersion = packageVersionFetchResult.version!;
                    if (product.includePrereleases == true || !IsPrerelease(packageVersion.version))
                    {
                        if (!packageNameToPackage.TryGetValue(packageVersion.name, out var ourPackage))
                        {
                            ourPackage = new PLPackage
                            {
                                versions = new Dictionary<string, PLPackageVersion>(),
                                repositoryUrl = $"https://github.com/{product.repository}"
                            };
                            packageNameToPackage[packageVersion.name] = ourPackage;
                        }

                        ourPackage.versions.Add(packageVersion.version, packageVersion);
                    }
                }
            }

            foreach (var package in packageNameToPackage.Values)
            {
                SortPackage(package);
            }

            return packageNameToPackage.Values.ToList();
        }
        catch (Exception)
        {
            await source.CancelAsync();
            throw;
        }
    }

    private List<PLPackageToFetch> SplitIntoWork(List<JToken> relevantReleasesUns)
    {
        return relevantReleasesUns
            .SelectMany(relevantReleaseUns =>
            {
                var assets = relevantReleaseUns[ReleaseAsset];
                if (_excessiveMode)
                {
                    var containsPackageJsonAsset = AtLeastOneOfTheAssetsIsPackageJson(relevantReleaseUns);
                    if (containsPackageJsonAsset)
                    {
                        // If it contains package.json, then it must only have just one zip in it.
                        var onlyZipAsset = assets.First(IsAssetThatZipFile);
                        return new List<PLPackageToFetch> { new()
                        {
                            urlOfDataToFetch = onlyZipAsset[AssetBrowserDownloadUrl].Value<string>(),
                            downloadCountOfActualPayload = onlyZipAsset[AssetDownloadCount].Value<int>(),
                            useExcessiveMode = true,
                            
                            urlOfPackageDownloadToStore = onlyZipAsset[AssetBrowserDownloadUrl].Value<string>(),
                            unityPackageNullable = FindUnitypackageAssetOrNull(relevantReleaseUns)
                        } };
                    }
                    else
                    {
                        // If it doesn't, then it *could* be a release that contains different packages (not different versions of the same package).
                        return assets
                            .Where(IsAssetThatZipFile)
                            .Select(zipFileAsset => new PLPackageToFetch
                            {
                                urlOfDataToFetch = zipFileAsset[AssetBrowserDownloadUrl].Value<string>(),
                                downloadCountOfActualPayload = zipFileAsset[AssetDownloadCount].Value<int>(),
                                useExcessiveMode = true,
                                
                                urlOfPackageDownloadToStore = zipFileAsset[AssetBrowserDownloadUrl].Value<string>(),
                                unityPackageNullable = null
                            })
                            .ToList();
                    }
                }

                var packageJsonAsset = assets.First(IsAssetThatPackageJsonFile);
                var zipAsset = assets.First(IsAssetThatZipFile);
                return new List<PLPackageToFetch> { new()
                {
                    urlOfDataToFetch = packageJsonAsset[AssetBrowserDownloadUrl].Value<string>(),
                    downloadCountOfActualPayload = zipAsset[AssetDownloadCount].Value<int>(),
                    useExcessiveMode = false,
                    
                    urlOfPackageDownloadToStore = zipAsset[AssetBrowserDownloadUrl].Value<string>(),
                    unityPackageNullable = FindUnitypackageAssetOrNull(relevantReleaseUns)
                } };
            })
            .ToList();
    }

    private static void SortPackage(PLPackage package)
    {
        // GitHub releases are usually sorted already in the desired order, but to be extra sure,
        // sort the releases by semver precedence in descending order.
        // Unsure how existing repository listing client applications cope with unordered items in the JSON object (e.g. VPM Catalog, ALCOM, ...),
        // however, we do make the assumption in our Outputter that the most recent package is the first in the versions list.
        var reorderedKeys = package.versions.Values
            .OrderByDescending(packageVersion => packageVersion.semver, SemVersion.PrecedenceComparer)
            .Select(packageVersion => packageVersion.version)
            .ToList();

        // TODO: Dictionary aren't supposed to have a given iteration order, it may be relevant to switch this to a JObject or something.
        package.versions = NewOrderedDict(reorderedKeys, package);
    }

    private static Dictionary<string, PLPackageVersion> NewOrderedDict(List<string> keys, PLPackage package)
    {
        var reorderedDict = new Dictionary<string, PLPackageVersion>();
        foreach (var key in keys)
        {
            reorderedDict[key] = package.versions[key];
        }
        return reorderedDict;
    }

    private static bool IsPrerelease(string version)
    {
        return version.Contains('-');
    }

    private static bool ThereAreAssets(JToken releaseUns)
    {
        return releaseUns[ReleaseAsset] != null;
    }

    private static bool AtLeastOneOfTheAssetsIsPackageJson(JToken releaseUns)
    {
        return releaseUns[ReleaseAsset].Any(asset => asset[AssetName].Value<string>() == PackageJsonFilename);
    }

    private static bool AtLeastOneOfTheAssetsIsZipFile(JToken releaseUns)
    {
        return releaseUns[ReleaseAsset].Any(IsAssetThatZipFile);
    }

    private static bool IfThereIsABodyThenItDoesNotContainTheHiddenTag(JToken releaseUns)
    {
        return releaseUns["body"] == null || !releaseUns["body"].Value<string>().Contains(HiddenBodyTag);
    }

    private async Task<PLPackageVersionFetchResult> DownloadAndCompilePackage(CancellationTokenSource source, PLPackageToFetch packageToFetch)
    {
        // This will download the ZIP file of the release, in order to calculate "zipSHA256". This isn't really used.
        if (packageToFetch.useExcessiveMode) return await ExcessiveMode(source, packageToFetch);
        
        // This will download the package.json asset of the release. The "zipSHA256" value won't be able to be calculated.
        else return await LighterMode(source, packageToFetch);
    }

    private async Task<PLPackageVersionFetchResult> ExcessiveMode(CancellationTokenSource source, PLPackageToFetch packageToFetch)
    {
        var downloadCount = packageToFetch.downloadCountOfActualPayload;
        var downloadUrl = packageToFetch.urlOfDataToFetch;
        var intermediary = await DownloadZip(downloadUrl, source);
        if (!intermediary.success)
        {
            return new PLPackageVersionFetchResult
            {
                success = false
            };
        }

        return new PLPackageVersionFetchResult
        {
            success = true,
            version = ToPackage(intermediary, downloadCount, downloadUrl, packageToFetch.unityPackageNullable)
        };
    }

    private async Task<PLPackageVersionFetchResult> LighterMode(CancellationTokenSource source, PLPackageToFetch packageToFetch)
    {
        var downloadCount = packageToFetch.downloadCountOfActualPayload;
        var downloadUrl = packageToFetch.urlOfPackageDownloadToStore;

        var packageJsonUrl = packageToFetch.urlOfDataToFetch;
        var intermediary = await DownloadPackageJson(packageJsonUrl, source);

        return new PLPackageVersionFetchResult
        {
            success = true,
            version = ToPackage(intermediary, downloadCount, downloadUrl, packageToFetch.unityPackageNullable)
        };
    }

    private static PLUnitypackageIntermediary FindUnitypackageAssetOrNull(JToken releaseUns)
    {
        var unitypackageAssetNullable = releaseUns[ReleaseAsset].FirstOrDefault(assetUns => assetUns[AssetName].Value<string>().ToLowerInvariant().EndsWith(".unitypackage"));
        if (unitypackageAssetNullable == null) return null;
        return new PLUnitypackageIntermediary
        {
            downloadUrl = unitypackageAssetNullable[AssetBrowserDownloadUrl].Value<string>(),
        };
    }

    private PLPackageVersion ToPackage(PLIntermediary intermediary, int downloadCount, string downloadUrl, PLUnitypackageIntermediary unityPackageNullable)
    {
        var package = JObject.Parse(intermediary.packageJson);

        var version = package["version"].Value<string>();
        return new PLPackageVersion
        {
            name = package[PackageName].Value<string>(),
            displayName = package["displayName"].Value<string>(),
            version = version, // Was formerly: (string)releaseUns[ReleaseTagName]
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
            
            downloadCount = downloadCount,
            semver = SemVersion.Parse(version, SemVersionStyles.Any),
            unitypackageUrl = unityPackageNullable?.downloadUrl
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
        var contentType = assetUns["content_type"].Value<string>();
        return (contentType == "application/zip" || contentType == "application/x-zip-compressed")
               && assetUns[AssetName].Value<string>().ToLowerInvariant().EndsWith(".zip");
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

        if (TryUnzip(data, out var packageJson))
        {
            return new PLIntermediary
            {
                success = true,
                hashHexNullableIfJson = hashHex,
                packageJson = packageJson
            };
        }
        else
        {
            Console.WriteLine($"Zip file at {zipUrl} didn't have a package.json at the root of it.");
            return new PLIntermediary
            {
                success = false
            };
        }
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
            success = true,
            hashHexNullableIfJson = null,
            packageJson = packageJson
        };
    }

    private bool TryUnzip(byte[] data, out string packageJson)
    {
        using var stream = new MemoryStream(data);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var entry = archive.GetEntry(PackageJsonFilename);
        if (entry == null)
        {
            // On some repos, there may not be a package.json if there were older releases.
            // This can happen in excessiveMode, where not only some of the earliest repos don't have a package.json asset exposed
            // therefore we can't know if it's usable, the repo contains irrelevant zips in it.
            packageJson = null;
            return false;
        }

        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        packageJson = reader.ReadToEnd();
        return true;
    }

    private async Task<List<JToken>> FetchReleaseDataUnstructured(string owner, string repo, CancellationTokenSource source)
    {
        var releasesUns = new List<JToken>();
        
        int requested = 100; // Max is 100
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