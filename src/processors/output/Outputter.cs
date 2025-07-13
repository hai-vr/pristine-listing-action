using System.Diagnostics.CodeAnalysis;
using System.Text;
using Hai.PristineListing.Core;
using Markdig;
using Newtonsoft.Json;

namespace Hai.PristineListing.Outputter;

public class PLOutputter
{
    private readonly string _outputIndexJson;

    public PLOutputter(string outputIndexJson)
    {
        _outputIndexJson = outputIndexJson;
    }

    public async Task Write(PLCoreOutputListing outputListing)
    {
        await Task.WhenAll(new[] { CreateListing(outputListing), CreateWebpage(outputListing) });
    }

    private async Task CreateListing(PLCoreOutputListing outputListing)
    {
        var asOutput = PLOutputListing.FromCore(outputListing);
        var outputJson = JsonConvert.SerializeObject(asOutput, Formatting.Indented, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });

        await File.WriteAllTextAsync(_outputIndexJson, outputJson, Encoding.UTF8);
    }

    [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
    private async Task CreateWebpage(PLCoreOutputListing outputListing)
    {
        var sw = new StringWriter();
        // FIXME: Yes, this needs to be replaced.
        sw.WriteLine(@"<style>
    body {
      font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Helvetica, Arial, sans-serif;
      margin: 2em;
      line-height: 1.6;
    }
  </style>");
        sw.WriteLine($"# {outputListing.id}");
        sw.WriteLine("");
        sw.WriteLine("This repository listing was generated using [hai-vr/pristine-listing-action](https://github.com/hai-vr/pristine-listing-action/).");
        sw.WriteLine("");
        sw.WriteLine($"- id: {outputListing.id}");
        sw.WriteLine($"- name: {outputListing.name}");
        sw.WriteLine($"- author: {outputListing.author}");
        sw.WriteLine($"- url: [{outputListing.url}]({outputListing.url})");
        sw.WriteLine("");
        foreach (var package in outputListing.packages)
        {
            var versions = package.Value.versions;
            sw.WriteLine($"## {package.Key}");
            sw.WriteLine("");

            var firstVersion = versions.Values.First();
            // FIXME: displayName, description, and some other fields like author may contain injections, which will be rendered in HTML.
            // This needs sanitizing.
            sw.WriteLine($"- displayName: {firstVersion.displayName}");
            sw.WriteLine($"- description: {firstVersion.description}");
            if (firstVersion.changelogUrl != null) sw.WriteLine($"- changelogUrl: [{firstVersion.changelogUrl}]({firstVersion.changelogUrl})");
            if (firstVersion.documentationUrl != null) sw.WriteLine($"- documentationUrl: [{firstVersion.documentationUrl}]({firstVersion.documentationUrl})");
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

            var author = firstVersion.author;
            switch (author.Kind)
            {
                case PLCoreOutputAuthorKind.String:
                    sw.WriteLine($"- author: {author.AsString()}");
                    break;
                case PLCoreOutputAuthorKind.Object:
                    var authorObject = author.AsObject();
                    sw.WriteLine($"- author:");
                    sw.WriteLine($"  - name: {authorObject.name}");
                    if (authorObject.email != null) sw.WriteLine($"  - email: {authorObject.email}");
                    if (authorObject.url != null) sw.WriteLine($"  - url: {authorObject.url}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            sw.WriteLine($"- metadata:");
            sw.WriteLine($"  - totalDownloadCount: {package.Value.totalDownloadCount}");
            sw.WriteLine($"  - repositoryUrl: [{package.Value.repositoryUrl}]({package.Value.repositoryUrl})");
            sw.WriteLine($"- versions:");
            foreach (var version in versions.Values)
            {
                var appendUnitypackageDownload = version.unitypackageUrl != null ? $" \\[[.unitypackage]({version.unitypackageUrl})\\]" : "";
                var appendUnitypackageDownloadCount = version.unitypackageDownloadCount != null ? $" *({version.unitypackageDownloadCount})*" : "";
                var versionFormatted = version.semver.IsRelease ? $"**{version.version}**" : version.version;
                sw.WriteLine($"  - {versionFormatted} \\[[.zip]({version.url})\\]{appendUnitypackageDownload} -> *{version.downloadCount} downloads*{appendUnitypackageDownloadCount}");
            }
            sw.WriteLine("");
        }

        var markdown = sw.ToString();
        var html = Markdown.ToHtml(markdown);
        await File.WriteAllTextAsync("output/list.md", markdown, Encoding.UTF8);
        await File.WriteAllTextAsync("output/index.html", html, Encoding.UTF8);
    }
}