using System.Data;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Markdig;
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
    private const string PackageJsonFilename = "package.json";

    private readonly string _inputJson;
    private readonly string _outputIndexJson;
    private readonly HttpClient _http;

    public static async Task Main(string[] args)
    {
        try
        {
            var githubToken = Environment.GetEnvironmentVariable("IN__GITHUB_TOKEN");
            if (string.IsNullOrWhiteSpace(githubToken)) throw new ArgumentException("IN__GITHUB_TOKEN env var contains nothing");

            var inputFile = "input.json";
            var outputFile = "index.json";
            
            var inputJson = await File.ReadAllTextAsync(inputFile, Encoding.UTF8);
            
            Directory.CreateDirectory("output");
            await new Program(githubToken, inputJson, $"output/{outputFile}").Run();
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
        _inputJson = inputJson;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("pristine-listing-action", "1.0.0"));
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

        await Task.WhenAll(new[] { CreateListing(outputListing), CreateWebpage(outputListing) });
    }

    private async Task CreateListing(PLOutputListing outputListing)
    {
        var outputJson = JsonConvert.SerializeObject(outputListing, Formatting.Indented, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });

        await File.WriteAllTextAsync(_outputIndexJson, outputJson, Encoding.UTF8);
    }

    private async Task CreateWebpage(PLOutputListing outputListing)
    {
        var sw = new StringWriter();
        sw.WriteLine($"# {outputListing.id}");
        sw.WriteLine("");
        foreach (var package in outputListing.packages)
        {
            var versions = package.Value.versions;
            sw.WriteLine($"## {package.Key}");
            sw.WriteLine("");

            var firstVersion = versions.Values.First();
            sw.WriteLine($"- displayName: {firstVersion.displayName}");
            sw.WriteLine($"- description: {firstVersion.description}");
            sw.WriteLine($"- totalDownloadCount: {versions.Values.Select(version => version.downloadCount).Sum()}");
            if (firstVersion.changelogUrl != null) sw.WriteLine($"- changelogUrl: {firstVersion.changelogUrl}");
            if (firstVersion.documentationUrl != null) sw.WriteLine($"- documentationUrl: {firstVersion.documentationUrl}");
            if (firstVersion.unity != null) sw.WriteLine($"- unity: {firstVersion.unity}");
            if (firstVersion.vrchatVersion != null) sw.WriteLine($"- vrchatVersion: {firstVersion.vrchatVersion}");
            if (firstVersion.dependencies != null && firstVersion.dependencies.Count > 0)
            {
                sw.WriteLine("- dependencies:");
                foreach (var dep in firstVersion.dependencies)
                {
                    sw.WriteLine($"  - {dep.Key} : {dep.Value}");
                }
            }
            if (firstVersion.vpmDependencies != null && firstVersion.vpmDependencies.Count > 0)
            {
                sw.WriteLine("- vpmDependencies:");
                foreach (var dep in firstVersion.vpmDependencies)
                {
                    sw.WriteLine($"  - {dep.Key} : {dep.Value}");
                }
            }
            sw.WriteLine($"- versions:");
            foreach (var version in versions.Values)
            {
                sw.WriteLine($"  - {version.version} -> {version.downloadCount}");
            }
            sw.WriteLine("");
        }

        var markdown = sw.ToString();
        var html = Markdown.ToHtml(markdown);
        await File.WriteAllTextAsync("output/list.md", markdown, Encoding.UTF8);
        await File.WriteAllTextAsync("output/index.html", html, Encoding.UTF8);
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

            var package = new PLPackage
            {
                versions = new Dictionary<string, PLPackageVersion>()
            };

            var relevantPackages = releasesUns
                .Where(ThereAreAssets)
                .Where(AtLeastOneOfTheAssetsIsPackageJson)
                .Where(IfThereIsABodyThenItDoesNotContainTheHiddenTag)
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

    private async Task<PLPackageVersion> CompilePackage(CancellationTokenSource source, JToken releaseUns)
    {
        // This will download the ZIP file of the release, in order to calculate "zipSHA256". This isn't really used.
        if (UseExcessiveMode) return await ExcessiveMode(source, releaseUns);
        
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
                name = ExtractAuthorName(package)
            },
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

    private static string ExtractAuthorName(JObject package)
    {
        var authorToken = package["author"];
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        var authorName = authorToken.Type switch
        {
            JTokenType.Object => authorToken.Value<JObject>()[PackageJsonAuthorName].Value<string>(),
            JTokenType.String => authorToken.Value<string>(),
            _  => throw new DataException("Can't deserialize author from package.json")
        };
        return authorName;
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