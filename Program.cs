using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal class Program
{
    private const string InputListingData = "listingData";
    private const string InputGithubReleases = "githubReleases";
    private const string ListingName = "name";
    private const string ListingAuthor = "author";
    private const string ListingUrl = "url";
    private const string ListingId = "id";
    private const string ReleaseTagName = "tag_name";
    private const string ReleaseAsset = "assets";
    private const string AssetName = "name";

    private readonly string _inputJson;
    private readonly string _outputIndexJson;
    private readonly HttpClient _http;
    private readonly string _githubToken;

    public static async Task Main(string[] args)
    {
        var githubToken = Environment.GetEnvironmentVariable("IN__GITHUB_TOKEN");
        if (string.IsNullOrWhiteSpace(githubToken)) throw new ArgumentException("IN__GITHUB_TOKEN env var contains nothing");

        var inputJson = await File.ReadAllTextAsync("input.json", Encoding.UTF8);
        
        await new Program(githubToken, inputJson, "output/index.json").Run();
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
        Console.WriteLine(outputJson);

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

            var packageVersions = releasesUns
                .Where(releaseUns => releaseUns[ReleaseAsset] != null)
                .Where(releaseUns => releaseUns[ReleaseAsset].Any(asset => asset[AssetName].Value<string>() == "package.json"))
                .Select(releaseUns =>
                {
                    var zipAsset = releaseUns[ReleaseAsset].First(assetUns => assetUns["content_type"].Value<string>() == "application/zip");
                    var downloadCount = zipAsset["download_count"].Value<int>();
                    
                    return new PLPackageVersion
                    {
                        name = "test", // TODO (it must not be null)
                        displayName = null, // TODO
                        version = (string)releaseUns[ReleaseTagName], // FIXME: Check if this is correct
                        unity = null, // TODO
                        description = $"(Downloaded {downloadCount} times)", // FIXME: This is not correct
                        vpmDependencies = null, // TODO
                        author = null, // TODO
                        changelogUrl = null, // TODO
                        documentationUrl = null, // TODO
                        license = null, // TODO
                        vrchatVersion = null, // TODO
                        zipSHA256 = null, // TODO
                        url = null, // TODO
                        legacyFolders = null, // TODO
                    };
                })
                .ToList();
            
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

    private async Task<JArray> FetchReleaseDataUnstructured(string owner, string repo, CancellationTokenSource source)
    {
        var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases";

        using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _githubToken);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("pristine-listing-action", "1.0.0"));
        
        var response = await _http.SendAsync(request, source.Token);
        if (!response.IsSuccessStatusCode) throw new InvalidDataException($"Did not receive a valid response from GitHub: {response.StatusCode}");
            
        var responseStr = await response.Content.ReadAsStringAsync(source.Token);
        var releases = JArray.Parse(responseStr);
        return releases;
    }

    private static void ParseRepository(string repository, out string owner, out string repo)
    {
        var split = repository.Split("/");
        if (split.Length != 2) throw new ArgumentException($"Invalid repository name {repository}");
    
        owner = split[0];
        repo = split[1];
    }
}