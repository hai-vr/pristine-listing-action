using System.Text;
using Newtonsoft.Json;

namespace Hai.PristineListing;

internal class Program
{
    private readonly string _inputFile;
    private readonly bool _includeDownloadCount;
    private readonly bool _devOnly;

    private readonly PLGatherer _gatherer;
    private readonly PLOutputter _outputter;

    public static async Task Main(string[] args)
    {
        string EnvVar(string var) => Environment.GetEnvironmentVariable(var);

        try
        {
            var githubToken = EnvVar("IN__GITHUB_TOKEN");
            if (string.IsNullOrWhiteSpace(githubToken)) throw new ArgumentException("IN__GITHUB_TOKEN env var contains nothing");

            var includeDownloadCount = false;
            var devOnly = false;

            if (bool.TryParse(EnvVar("IN__INCLUDE_DOWNLOAD_COUNT"), out var doIncludeDownloadCount)) includeDownloadCount = doIncludeDownloadCount;
            if (bool.TryParse(EnvVar("IN__DEVONLY"), out var doDevOnly)) devOnly = doDevOnly;
            
            if (devOnly) Console.WriteLine("We're in DEVELOPERONLY mode.");

            var inputFile = "input.json";
            var outputFile = "index.json";

            await new Program(githubToken, inputFile, $"output/{outputFile}", includeDownloadCount, devOnly).Run();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error occurred: {e.Message}");
            Console.WriteLine(e);
            throw;
        }
    }

    private Program(string githubToken, string inputFile, string outputFile, bool includeDownloadCount, bool devOnly)
    {
        _inputFile = inputFile;
        _includeDownloadCount = includeDownloadCount;
        _devOnly = devOnly;

        _gatherer = new PLGatherer(githubToken);
        _outputter = new PLOutputter(outputFile);
    }

    private async Task Run()
    {
        Directory.CreateDirectory("output");
        
        var inputJson = await File.ReadAllTextAsync(_inputFile, Encoding.UTF8);
        var input = JsonConvert.DeserializeObject<PLInput>(inputJson, new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.Populate
        });
        foreach (var product in input.products)
        {
            product.includePrereleases ??= input.settings.defaultIncludePrereleases;
            product.onlyPackageNames ??= new List<string>();
            if (product.mode is null or PLMode.Undefined) product.mode = input.settings.defaultMode;
        }
        
        var outputListing = await _gatherer.DownloadAndAggregate(input);

        foreach (var outputListingPackage in outputListing.packages.Values)
        {
            var totalDownloadCount = outputListingPackage.versions.Values
                .Select(version => version.downloadCount)
                .Sum();
            outputListingPackage.totalDownloadCount = totalDownloadCount;
            
            foreach (var version in outputListingPackage.versions.Values)
            {
                var description = version.description ?? version.displayName;
                if (_devOnly)
                {
                    if (_includeDownloadCount) version.displayName = $"{version.displayName} 🔽{version.downloadCount}/{totalDownloadCount}";
                }
                version.description = _includeDownloadCount ? $"{description} (Downloaded {version.downloadCount} times)" : description;
            }
        }

        await _outputter.Write(outputListing);
    }
}