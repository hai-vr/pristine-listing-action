using System.Data;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hai.PristineListing;

internal class Program
{
    private const bool UseExcessiveMode = false;
    
    private const string ReleaseAsset = "assets";
    private const string AssetName = "name";
    private const string PackageJsonAuthorName = "name";
    private const string PackageName = "name";
    private const string HiddenBodyTag = @"$\texttt{Hidden}$";

    private readonly string _inputJson;
    private readonly string _outputIndexJson;
    private readonly HttpClient _http;
    private readonly string _githubToken;

    public static async Task Main(string[] args)
    {
        try
        {
            var githubToken = Environment.GetEnvironmentVariable("IN__GITHUB_TOKEN");
            if (string.IsNullOrWhiteSpace(githubToken)) throw new ArgumentException("IN__GITHUB_TOKEN env var contains nothing");

            var inputJson = await File.ReadAllTextAsync("input.json", Encoding.UTF8);
            
            Directory.CreateDirectory("output");
            await new Program(githubToken, inputJson, "output/index.json").Run();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error occurred: {e.Message}");
            Console.WriteLine(e);
            throw;
        }
    }

    private Program(string githubToken, string inputJson, string outputFile)
    {
        _githubToken = githubToken;
        _inputJson = inputJson;
        _http = new HttpClient();
        _outputIndexJson = outputFile;
    }

    private async Task Run()
    {
        var input = JsonConvert.DeserializeObject<PLInput>(_inputJson);
        var listingData = input.listingData;

        var outputListing = NewOutputListing(listingData);

        var packages = await AsPackages(input.products);
        outputListing.packages = packages
            .Where(package => package.versions.Count > 0)
            .ToDictionary(package => package.versions.First().Value.name);

        var outputJson = JsonConvert.SerializeObject(outputListing, Formatting.Indented, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });

        await File.WriteAllTextAsync(_outputIndexJson, outputJson, Encoding.UTF8);
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

    private async Task<PLPackage[]> AsPackages(List<PLProducts> products)
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

            var package = new PLPackage
            {
                versions = new Dictionary<string, PLPackageVersion>()
            };

            var relevantPackages = releasesUns
                .Where(releaseUns => releaseUns[ReleaseAsset] != null)
                .Where(releaseUns => releaseUns[ReleaseAsset].Any(asset => asset[AssetName].Value<string>() == "package.json"))
                .Where(releaseUns => releaseUns["body"] == null || !releaseUns["body"].Value<string>().Contains(HiddenBodyTag))
                .ToList();

            var packageVersions = await Task.WhenAll(relevantPackages.Select(releaseUns => CompilePackage(source, releaseUns)));
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

    private async Task<PLPackageVersion> CompilePackage(CancellationTokenSource source, JToken releaseUns)
    {
        if (UseExcessiveMode) return await ExcessiveMode(source, releaseUns);
        else return await LighterMode(source, releaseUns);
    }

    private async Task<PLPackageVersion> ExcessiveMode(CancellationTokenSource source, JToken releaseUns)
    {
        var zipAsset = releaseUns[ReleaseAsset].First(IsAssetThatZipFile);
        var downloadCount = zipAsset["download_count"].Value<int>();
        var downloadUrl = zipAsset["browser_download_url"].Value<string>();
        var downloadZip = await DownloadZip(downloadUrl, source);

        var package = JObject.Parse(downloadZip.packageJson);
        
        var authorToken = package["author"];
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        var authorName = authorToken.Type switch
        {
            JTokenType.Object => authorToken.Value<JObject>()[PackageJsonAuthorName].Value<string>(),
            JTokenType.String => authorToken.Value<string>(),
            _  => throw new DataException("Can't deserialize author from package.json")
        };

        var displayName = package["displayName"].Value<string>();
        var description = package["description"]?.Value<string>() ?? displayName;

        return new PLPackageVersion
        {
            name = package[PackageName].Value<string>(),
            displayName = displayName,
            version = package["version"].Value<string>(), // Was formerly: (string)releaseUns[ReleaseTagName]
            unity = package["unity"]?.Value<string>(),
            description = $"{description} (Downloaded {downloadCount} times)",
            dependencies = AsDictionary(package["dependencies"]?.Value<JObject>()),
            vpmDependencies = AsDictionary(package["vpmDependencies"]?.Value<JObject>()),
            author = new PLAuthor
            {
                name = authorName
            },
            url = downloadUrl,
            documentationUrl = package["documentationUrl"]?.Value<string>(),
            changelogUrl = package["changelogUrl"]?.Value<string>(),
            license = package["license"]?.Value<string>(),
            zipSHA256 = downloadZip.hashHexNullableIfJson,
            
            vrchatVersion = package["vrchatVersion"]?.Value<string>(),
            legacyFolders = AsDictionary(package["legacyFolders"]?.Value<JObject>()),
        };
    }

    private async Task<PLPackageVersion> LighterMode(CancellationTokenSource source, JToken releaseUns)
    {
        var zipAsset = releaseUns[ReleaseAsset].First(IsAssetThatZipFile);
        var downloadCount = zipAsset["download_count"].Value<int>();
        var downloadUrl = zipAsset["browser_download_url"].Value<string>();

        var packageJsonAsset = releaseUns[ReleaseAsset].First(IsAssetThatPackageJsonFile);
        var packageJsonUrl = packageJsonAsset["browser_download_url"].Value<string>();
        var packageJson = (await DownloadPackageJson(packageJsonUrl, source)).packageJson;

        var package = JObject.Parse(packageJson);
        
        var authorToken = package["author"];
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        var authorName = authorToken.Type switch
        {
            JTokenType.Object => authorToken.Value<JObject>()[PackageJsonAuthorName].Value<string>(),
            JTokenType.String => authorToken.Value<string>(),
            _  => throw new DataException("Can't deserialize author from package.json")
        };

        var displayName = package["displayName"].Value<string>();
        var description = package["description"]?.Value<string>() ?? displayName;

        return new PLPackageVersion
        {
            name = package[PackageName].Value<string>(),
            displayName = displayName,
            version = package["version"].Value<string>(), // Was formerly: (string)releaseUns[ReleaseTagName]
            unity = package["unity"]?.Value<string>(),
            description = $"{description} (Downloaded {downloadCount} times)",
            dependencies = AsDictionary(package["dependencies"]?.Value<JObject>()),
            vpmDependencies = AsDictionary(package["vpmDependencies"]?.Value<JObject>()),
            author = new PLAuthor
            {
                name = authorName
            },
            url = downloadUrl,
            documentationUrl = package["documentationUrl"]?.Value<string>(),
            changelogUrl = package["changelogUrl"]?.Value<string>(),
            license = package["license"]?.Value<string>(),
            zipSHA256 = null,
            
            vrchatVersion = package["vrchatVersion"]?.Value<string>(),
            legacyFolders = AsDictionary(package["legacyFolders"]?.Value<JObject>()),
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
        return assetUns["content_type"].Value<string>() == "application/json" && assetUns[AssetName].Value<string>().ToLowerInvariant() == "package.json";
    }

    private async Task<PLIntermediary> DownloadZip(string zipUrl, CancellationTokenSource source)
    {
        Console.WriteLine($"Downloading zip {zipUrl}...");
        
        var request = NewRequest(zipUrl);
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
        
        var request = NewRequest(packageJsonUrl);
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

        var entry = archive.GetEntry("package.json");
        if (entry == null) throw new FileNotFoundException("Missing package.json from .zip. We should not unzip in the first place if it wasn't a package to begin with.");

        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private async Task<JArray> FetchReleaseDataUnstructured(string owner, string repo, CancellationTokenSource source)
    {
        // FIXME: Implement proper pagination
        int requested = 100; // Max is 100
        var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases?per_page={requested}";

        using var request = NewRequest(apiUrl);
        
        var response = await _http.SendAsync(request, source.Token);
        if (!response.IsSuccessStatusCode) throw new InvalidDataException($"Did not receive a valid response from GitHub: {response.StatusCode}");
            
        var responseStr = await response.Content.ReadAsStringAsync(source.Token);
        var releases = JArray.Parse(responseStr);

        if (releases.Count == requested) throw new DataException("Pagination not implemented");
        
        return releases;
    }

    private HttpRequestMessage NewRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _githubToken);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("pristine-listing-action", "1.0.0"));
        return request;
    }

    private static void ParseRepository(string repository, out string owner, out string repo)
    {
        var split = repository.Split("/");
        if (split.Length != 2) throw new ArgumentException($"Invalid repository name {repository}");
    
        owner = split[0];
        repo = split[1];
    }
}