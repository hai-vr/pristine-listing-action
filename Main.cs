using System.Text;
using Newtonsoft.Json;

namespace Hai.PristineListing;

internal class Program
{
    private readonly string _inputFile;
    private readonly bool _includeDownloadCount;
    
    private readonly PLGatherer _gatherer;
    private readonly PLOutputter _outputter;

    public static async Task Main(string[] args)
    {
        string EnvVar(string var) => Environment.GetEnvironmentVariable(var);

        try
        {
            var githubToken = EnvVar("IN__GITHUB_TOKEN");
            if (string.IsNullOrWhiteSpace(githubToken)) throw new ArgumentException("IN__GITHUB_TOKEN env var contains nothing");

            var excessiveMode = false;
            var includeDownloadCount = false;

            if (bool.TryParse(EnvVar("IN__EXCESSIVE_MODE"), out var doExcessiveMode)) excessiveMode = doExcessiveMode;
            if (bool.TryParse(EnvVar("IN__INCLUDE_DOWNLOAD_COUNT"), out var doIncludeDownloadCount)) includeDownloadCount = doIncludeDownloadCount;

            var inputFile = "input.json";
            var outputFile = "index.json";

            await new Program(githubToken, inputFile, $"output/{outputFile}", excessiveMode, includeDownloadCount).Run();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error occurred: {e.Message}");
            Console.WriteLine(e);
            throw;
        }
    }

    private Program(string githubToken, string inputFile, string outputFile, bool excessiveMode, bool includeDownloadCount)
    {
        _inputFile = inputFile;
        _includeDownloadCount = includeDownloadCount;

        _gatherer = new PLGatherer(githubToken, excessiveMode);
        _outputter = new PLOutputter(outputFile);
    }

    private async Task Run()
    {
        Directory.CreateDirectory("output");
        
        var inputJson = await File.ReadAllTextAsync(_inputFile, Encoding.UTF8);
        var input = JsonConvert.DeserializeObject<PLInput>(inputJson);
        
        var outputListing = await _gatherer.DownloadAndAggregate(input);
        
        foreach (var outputListingPackage in outputListing.packages.Values)
        {
            foreach (var version in outputListingPackage.versions.Values)
            {
                var description = version.description ?? version.displayName;
                version.description = _includeDownloadCount ? $"{description} (Downloaded {version.downloadCount} times)" : description;
            }
        }

        await _outputter.Write(outputListing);
    }
}