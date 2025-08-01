﻿using System.Diagnostics.CodeAnalysis;
using System.Text;
using Hai.PristineListing.Core;
using Markdig;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hai.PristineListing.Outputter;

public class PLOutputter
{
    private readonly string _outputIndexJson;

    public PLOutputter(string outputIndexJson)
    {
        _outputIndexJson = outputIndexJson;
    }

    public virtual async Task Write(PLCoreInputSettings settings, PLCoreOutputListing outputListing, PLCoreAggregation aggregation)
    {
        await Task.WhenAll(new[] { CreateListing(settings, outputListing, aggregation), CreateWebpage(outputListing) });
    }

    private async Task CreateListing(PLCoreInputSettings settings, PLCoreOutputListing outputListing, PLCoreAggregation aggregation)
    {
        var asOutput = PLOutputListing.FromCore(outputListing);
        if (settings.forceOutputAuthorAsObject)
        {
            foreach (var package in asOutput.packages.Values)
            {
                foreach (var version in package.versions.Values)
                {
                    if (version.author is string authorStr)
                    {
                        version.author = new PLOutputAuthorObject
                        {
                            name = authorStr
                        };
                    }
                }
            }
        }

        var intermediate = JObject.FromObject(asOutput, new JsonSerializer
        {
            NullValueHandling = NullValueHandling.Ignore
        });
        var packages = intermediate["packages"].Value<JObject>();
        foreach (var aggregate in aggregation.aggregated)
        {
            foreach (var aggregatePackage in aggregate.packages)
            {
                if (!packages.ContainsKey(aggregatePackage.Key))
                {
                    var versions = new JObject();
                    foreach (var aggregateVersion in aggregatePackage.Value.versions)
                    {
                        versions.Add(aggregateVersion.Key, aggregateVersion.Value.data);
                    }

                    var package = new JObject();
                    package.Add("versions", versions);
                    packages.Add(aggregatePackage.Key, package);
                }
            }
        }

        var outputJson = JsonConvert.SerializeObject(intermediate, Formatting.Indented, new JsonSerializerSettings
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

            {
                var firstVersion = versions.Values.First();
                var firstUpm = firstVersion.upmManifest;
                // FIXME: displayName, description, and some other fields like author may contain injections, which will be rendered in HTML.
                // This needs sanitizing.
                if (firstUpm.displayName != null) sw.WriteLine($"- displayName: {firstUpm.displayName}");
                if (firstUpm.description != null) sw.WriteLine($"- description: {firstUpm.description}");
                if (firstUpm.keywords != null && firstUpm.keywords.Count > 0)
                {
                    sw.WriteLine($"- keywords:");
                    foreach (var keyword in firstUpm.keywords)
                    {
                        sw.WriteLine($"  - {keyword}");
                    }
                }
                if (firstUpm.changelogUrl != null) sw.WriteLine($"- changelogUrl: [{firstUpm.changelogUrl}]({firstUpm.changelogUrl})");
                if (firstUpm.documentationUrl != null) sw.WriteLine($"- documentationUrl: [{firstUpm.documentationUrl}]({firstUpm.documentationUrl})");
                if (firstUpm.license != null) sw.WriteLine($"- license: {firstUpm.license}");
                if (firstUpm.licensesUrl != null) sw.WriteLine($"- licensesUrl: [{firstUpm.licensesUrl}]({firstUpm.licensesUrl})");
                if (firstUpm.unity != null) sw.WriteLine($"- unity: {firstUpm.unity}");
                if (firstUpm.unityRelease != null) sw.WriteLine($"- unityRelease: {firstUpm.unityRelease}");
                if (firstVersion.vrcConvention.vrchatVersion != null) sw.WriteLine($"- <s>vrchatVersion: {firstVersion.vrcConvention.vrchatVersion}</s>");
                if (firstUpm.dependencies != null && firstUpm.dependencies.Count > 0)
                {
                    sw.WriteLine("- dependencies:");
                    foreach (var dep in firstUpm.dependencies)
                    {
                        sw.WriteLine($"  - {dep.Key} : {dep.Value}");
                    }
                }
                if (firstVersion.vpmConvention.vpmDependencies != null && firstVersion.vpmConvention.vpmDependencies.Count > 0)
                {
                    sw.WriteLine("- vpmDependencies:");
                    foreach (var dep in firstVersion.vpmConvention.vpmDependencies)
                    {
                        sw.WriteLine($"  - {dep.Key} : {dep.Value}");
                    }
                }

                var author = firstUpm.author;
                if (author != null)
                {
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
                            if (authorObject.url != null) sw.WriteLine($"  - url: [{authorObject.url}]({authorObject.url})");
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                sw.WriteLine($"- metadata:");
                sw.WriteLine($"  - totalDownloadCount: {package.Value.totalDownloadCount}");
                sw.WriteLine($"  - repositoryUrl: [{package.Value.repositoryUrl}]({package.Value.repositoryUrl})");
                if (firstVersion.alcomConvention.vrcGetData != null)
                {
                    sw.WriteLine($"- vrc-get:");
                    if (firstVersion.alcomConvention.yanked is { } yanked)
                    {
                        switch (yanked.Kind)
                        {
                            case PLCoreOutputALCOMYankedKind.String:
                                sw.WriteLine($"  - yanked: {yanked.AsString()}");
                                break;
                            case PLCoreOutputALCOMYankedKind.Bool:
                                sw.WriteLine($"  - yanked: {(yanked.AsBool() ? "true" : "false")}");
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                    sw.WriteLine("```json");
                    sw.WriteLine(firstVersion.alcomConvention.vrcGetData.ToString(Formatting.Indented));
                    sw.WriteLine("```");
                }
            }
            
            sw.WriteLine($"- versions:");
            foreach (var version in versions.Values)
            {
                var appendUnitypackageDownload = version.unitypackageUrl != null ? $" \\[[.unitypackage]({version.unitypackageUrl})\\]" : "";
                var appendUnitypackageDownloadCount = version.unitypackageDownloadCount != null ? $" *({version.unitypackageDownloadCount})*" : "";
                var versionFormatted = version.semver.IsRelease ? $"**{version.upmManifest.version}**" : version.upmManifest.version;
                sw.WriteLine($"  - {versionFormatted} \\[[.zip]({version.listingConvention.url})\\]{appendUnitypackageDownload} -> *{version.downloadCount} downloads*{appendUnitypackageDownloadCount}");
            }
            sw.WriteLine("");
        }

        var markdown = sw.ToString();
        var html = Markdown.ToHtml(markdown);
        await File.WriteAllTextAsync("output/list.md", markdown, Encoding.UTF8);
        await File.WriteAllTextAsync("output/index.html", html, Encoding.UTF8);
    }
}