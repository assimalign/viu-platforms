using System;
using System.IO;
using Shouldly;
using Xunit;

namespace Assimalign.Cohesion.Viu.Server.Tests;

public sealed class ManifestTests
{
    [Fact(DisplayName = "Viu Cohesion Web - runtime manifest: resolves explicit assets and wildcard patterns")]
    public void Load_RuntimeManifest_ShouldResolveExplicitAssetsAndPatterns()
    {
        // Arrange
        using TemporaryApplication application = new();
        application.WriteContent("index.html", "index");
        application.WriteContent("fallback.txt", "pattern");
        string escapedRoot = EscapeJson(application.ContentRootPath + Path.DirectorySeparatorChar);
        string runtimePath = application.WriteRootFile(
            "Test.staticwebassets.runtime.json",
            $$"""
              {
                "ContentRoots": ["{{escapedRoot}}"],
                "Root": {
                  "Children": {
                    "index.html": {
                      "Children": null,
                      "Asset": { "ContentRootIndex": 0, "SubPath": "index.html" },
                      "Patterns": null
                    }
                  },
                  "Asset": null,
                  "Patterns": [{ "ContentRootIndex": 0, "Pattern": "**", "Depth": 0 }]
                }
              }
              """);
        ViuApplicationConfiguration configuration = new(
            application.ContentRootPath,
            runtimePath,
            EndpointsManifestPath: null,
            ApplyCrossOriginIsolationHeaders: false,
            new BrowserRefreshConfiguration(string.Empty, string.Empty, string.Empty));

        // Act
        StaticWebAssetCatalog catalog = StaticWebAssetCatalog.Load(configuration);
        StaticWebAssetDescriptor? index = catalog.Resolve("/index.html", acceptEncoding: null);
        StaticWebAssetDescriptor? pattern = catalog.Resolve("/fallback.txt", acceptEncoding: null);

        // Assert
        index.ShouldNotBeNull();
        index.PhysicalPath.ShouldBe(Path.Combine(application.ContentRootPath, "index.html"));
        pattern.ShouldNotBeNull();
        pattern.PhysicalPath.ShouldBe(Path.Combine(application.ContentRootPath, "fallback.txt"));
    }

    [Fact(DisplayName = "Viu Cohesion Web - endpoints manifest: selects Brotli and fingerprinted assets")]
    public void Load_EndpointsManifest_ShouldSelectBrotliAndFingerprintedAssets()
    {
        // Arrange
        using TemporaryApplication application = new();
        application.WriteContent("app.wasm", "identity");
        application.WriteContent("app.wasm.br", "brotli");
        application.WriteContent("app.wasm.gz", "gzip");
        string escapedRoot = EscapeJson(application.ContentRootPath + Path.DirectorySeparatorChar);
        string runtimePath = application.WriteRootFile(
            "Test.staticwebassets.runtime.json",
            $$"""
              { "ContentRoots": ["{{escapedRoot}}"], "Root": {
                "Children": {
                  "app.wasm": { "Children": null, "Asset": { "ContentRootIndex": 0, "SubPath": "app.wasm" }, "Patterns": null },
                  "app.wasm.br": { "Children": null, "Asset": { "ContentRootIndex": 0, "SubPath": "app.wasm.br" }, "Patterns": null },
                  "app.wasm.gz": { "Children": null, "Asset": { "ContentRootIndex": 0, "SubPath": "app.wasm.gz" }, "Patterns": null }
                }, "Asset": null, "Patterns": null } }
              """);
        string endpointsPath = application.WriteRootFile(
            "Test.staticwebassets.endpoints.json",
            """
              { "Version": 1, "ManifestType": "Build", "Endpoints": [
                { "Route": "app.wasm", "AssetFile": "app.wasm.br",
                  "Selectors": [{ "Name": "Content-Encoding", "Value": "br", "Quality": "0.5" }],
                  "ResponseHeaders": [{ "Name": "Content-Encoding", "Value": "br" }, { "Name": "Content-Type", "Value": "application/wasm" }],
                  "EndpointProperties": [] },
                { "Route": "app.wasm", "AssetFile": "app.wasm.gz",
                  "Selectors": [{ "Name": "Content-Encoding", "Value": "gzip", "Quality": "1" }],
                  "ResponseHeaders": [{ "Name": "Content-Encoding", "Value": "gzip" }, { "Name": "Content-Type", "Value": "application/wasm" }],
                  "EndpointProperties": [] },
                { "Route": "app.wasm", "AssetFile": "app.wasm", "Selectors": [],
                  "ResponseHeaders": [{ "Name": "Content-Type", "Value": "application/wasm" }], "EndpointProperties": [] },
                { "Route": "app.abc123.wasm", "AssetFile": "app.wasm", "Selectors": [],
                  "ResponseHeaders": [{ "Name": "Cache-Control", "Value": "max-age=31536000, immutable" }],
                  "EndpointProperties": [{ "Name": "fingerprint", "Value": "abc123" }] }
              ] }
              """);
        ViuApplicationConfiguration configuration = new(
            application.ContentRootPath,
            runtimePath,
            endpointsPath,
            ApplyCrossOriginIsolationHeaders: false,
            new BrowserRefreshConfiguration(string.Empty, string.Empty, string.Empty));

        // Act
        StaticWebAssetCatalog catalog = StaticWebAssetCatalog.Load(configuration);
        StaticWebAssetDescriptor? compressed = catalog.Resolve("/app.wasm", "gzip;q=0.8, br;q=1");
        StaticWebAssetDescriptor? gzipPreferred = catalog.Resolve("/app.wasm", "gzip;q=1, br;q=0.8");
        StaticWebAssetDescriptor? selectorPreferred = catalog.Resolve("/app.wasm", "gzip;q=1, br;q=1");
        StaticWebAssetDescriptor? identityPreferred = catalog.Resolve(
            "/app.wasm",
            "gzip;q=0.4, br;q=0.5, identity;q=0.9");
        StaticWebAssetDescriptor? identity = catalog.Resolve("/app.wasm", "br;q=0");
        StaticWebAssetDescriptor? fingerprinted = catalog.Resolve("/app.abc123.wasm", acceptEncoding: null);

        // Assert
        compressed.ShouldNotBeNull();
        compressed.ContentEncoding.ShouldBe("br");
        compressed.PhysicalPath.ShouldEndWith("app.wasm.br");
        gzipPreferred.ShouldNotBeNull();
        gzipPreferred.ContentEncoding.ShouldBe("gzip");
        selectorPreferred.ShouldNotBeNull();
        selectorPreferred.ContentEncoding.ShouldBe("gzip");
        identityPreferred.ShouldNotBeNull();
        identityPreferred.ContentEncoding.ShouldBeNull();
        identity.ShouldNotBeNull();
        identity.ContentEncoding.ShouldBeNull();
        fingerprinted.ShouldNotBeNull();
        fingerprinted.ResponseHeaders["Cache-Control"].ShouldBe("max-age=31536000, immutable");
    }

    [Fact(DisplayName = "Viu Cohesion Web - manifest reader: rejects malformed JSON")]
    public void ReadRuntime_MalformedJson_ShouldThrowInvalidOperationException()
    {
        // Arrange
        using TemporaryApplication application = new();
        string path = application.WriteRootFile("broken.staticwebassets.runtime.json", "{");

        // Act
        Action action = () => StaticWebAssetsManifestReader.ReadRuntime(path);

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact(DisplayName = "Viu Cohesion Web - runtime manifest: applies pattern depth to the complete request path")]
    public void Load_NestedPattern_ShouldStripManifestDepthBeforeMatchingContentRoot()
    {
        // Arrange
        using TemporaryApplication application = new();
        application.WriteContent("scripts/app.js", "application");
        string escapedRoot = EscapeJson(application.ContentRootPath + Path.DirectorySeparatorChar);
        string runtimePath = application.WriteRootFile(
            "Test.staticwebassets.runtime.json",
            $$"""
              { "ContentRoots": ["{{escapedRoot}}"], "Root": {
                "Children": { "_content": { "Children": { "Library": {
                  "Children": null,
                  "Asset": null,
                  "Patterns": [{ "ContentRootIndex": 0, "Pattern": "**/*.js", "Depth": 2 }]
                } }, "Asset": null, "Patterns": null } },
                "Asset": null,
                "Patterns": null
              } }
              """);
        ViuApplicationConfiguration configuration = new(
            application.ContentRootPath,
            runtimePath,
            EndpointsManifestPath: null,
            ApplyCrossOriginIsolationHeaders: false,
            new BrowserRefreshConfiguration(string.Empty, string.Empty, string.Empty));

        // Act
        StaticWebAssetDescriptor? asset = StaticWebAssetCatalog
            .Load(configuration)
            .Resolve("/_content/Library/scripts/app.js", acceptEncoding: null);

        // Assert
        asset.ShouldNotBeNull();
        asset.PhysicalPath.ShouldBe(Path.Combine(application.ContentRootPath, "scripts", "app.js"));
    }

    private static string EscapeJson(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal);
    }
}
