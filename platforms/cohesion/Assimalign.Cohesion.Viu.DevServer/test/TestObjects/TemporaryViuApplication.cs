using System;
using System.IO;

namespace Assimalign.Cohesion.Viu.DevServer.Tests;

internal sealed class TemporaryViuApplication : IDisposable
{
    public TemporaryViuApplication()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "viu-cohesion-devserver-tests", Guid.NewGuid().ToString("N"));
        ContentRootPath = Path.Combine(RootPath, "wwwroot");
        Directory.CreateDirectory(ContentRootPath);

        File.WriteAllText(Path.Combine(ContentRootPath, "index.html"), "<!doctype html><title>Viu test</title>");
        File.WriteAllText(Path.Combine(ContentRootPath, "probe.wasm"), "synthetic-wasm-probe");
        string escapedContentRoot = (ContentRootPath + Path.DirectorySeparatorChar)
            .Replace("\\", "\\\\", StringComparison.Ordinal);
        RuntimeManifestPath = WriteRootFile(
            "Test.staticwebassets.runtime.json",
            $$"""
              { "ContentRoots": ["{{escapedContentRoot}}"], "Root": {
                "Children": {
                  "index.html": { "Children": null, "Asset": { "ContentRootIndex": 0, "SubPath": "index.html" }, "Patterns": null }
                }, "Asset": null, "Patterns": [{ "ContentRootIndex": 0, "Pattern": "**", "Depth": 0 }] } }
              """);
        EndpointsManifestPath = WriteRootFile(
            "Test.staticwebassets.endpoints.json",
            """
              { "Version": 1, "ManifestType": "Build", "Endpoints": [] }
              """);
    }

    public string RootPath { get; }

    public string ContentRootPath { get; }

    public string EndpointsManifestPath { get; }

    public string RuntimeManifestPath { get; }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }

    private string WriteRootFile(string name, string content)
    {
        string path = Path.Combine(RootPath, name);
        File.WriteAllText(path, content);
        return path;
    }
}
