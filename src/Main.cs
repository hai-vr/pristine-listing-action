using System.Text;
using Hai.PristineListing.Gatherer;
using Hai.PristineListing.Input;
using Hai.PristineListing.Outputter;

namespace Hai.PristineListing;

internal class Program
{
    private readonly string _inputFile;
    private readonly bool _devOnly;

    private readonly PLGatherer _gatherer;
    private readonly PLOutputter _outputter;
    private readonly InputParser _inputParser;

    public static async Task Main(string[] args)
    {
        string EnvVar(string var) => Environment.GetEnvironmentVariable(var);

        try
        {
            var githubToken = EnvVar("IN__GITHUB_TOKEN");
            if (string.IsNullOrWhiteSpace(githubToken)) throw new ArgumentException("IN__GITHUB_TOKEN env var contains nothing");

            var devOnly = false;

            if (bool.TryParse(EnvVar("IN__DEVONLY"), out var doDevOnly)) devOnly = doDevOnly;
            
            if (devOnly) Console.WriteLine("We're in DEVELOPERONLY mode.");

            var inputFile = "input.json";
            var outputFile = "index.json";

            await new Program(githubToken, inputFile, $"output/{outputFile}", devOnly).Run();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error occurred: {e.Message}");
            Console.WriteLine(e);
            throw;
        }
    }

    private Program(string githubToken, string inputFile, string outputFile, bool devOnly)
    {
        _inputFile = inputFile;
        _devOnly = devOnly;

        _inputParser = new InputParser();
        _gatherer = new PLGatherer(githubToken);
        _outputter = new PLOutputter(outputFile);
    }

    private async Task Run()
    {
        Directory.CreateDirectory("output");
        
        var inputJson = await File.ReadAllTextAsync(_inputFile, Encoding.UTF8);
        var input = _inputParser.Parse(inputJson);

        var outputListing = await _gatherer.DownloadAndAggregate(input);

        var includeDownloadCount = input.settings.includeDownloadCount;
        foreach (var outputListingPackage in outputListing.packages.Values)
        {
            var totalDownloadCount = outputListingPackage.totalDownloadCount;

            if (includeDownloadCount)
            {
                foreach (var version in outputListingPackage.versions.Values)
                {
                    var description = version.description ?? "";
                    if (_devOnly)
                    {
                        version.displayName = $"{version.displayName} 🔽{version.downloadCount}/{totalDownloadCount}";
                    }
                    version.description = $"{description} (Downloaded {version.downloadCount} times)";
                }
            }
        }

        await _outputter.Write(outputListing);
    }
}