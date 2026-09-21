using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Assimalign.Cohesion.Hosting.Resources;
using Assimalign.Cohesion.Http;
using Assimalign.Cohesion.Viu.Hosting;
using Assimalign.Cohesion.Web;
using Assimalign.Cohesion.Web.Hosting;

using Shouldly;
using Xunit;

namespace Assimalign.Cohesion.Viu.Server.Tests;

public sealed class ResourceHostingTests
{
    [Theory]
    [InlineData("/readyz")]
    [InlineData("/livez")]
    [InlineData("/healthz")]
    [InlineData("/cohesion/v1/health")]
    [InlineData("/cohesion/v1")]
    [InlineData("/COHESION/V1/commands")]
    public async Task FallbackPassesResourceRoutesToNextMiddleware(string path)
    {
        using TemporaryApplication content = new();
        content.WriteContent("index.html", "<html>index</html>");
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.AddViuApplication(options => options.ContentRootPath = content.ContentRootPath);
        await using WebApplication application = builder.Build();
        application.UseViuApplication();
        application.Use((context, next) =>
        {
            context.Response.StatusCode = HttpStatusCode.ServiceUnavailable;
            return Task.CompletedTask;
        });
        TestHttpContext context = new(path, HttpMethod.Get);
        await ((IWebApplicationPipelineBuilder)application).Build().ExecuteAsync(context);
        context.Response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        context.ReadResponseBody().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("/cohesion")]
    [InlineData("/cohesion/database/sql")]
    [InlineData("/cohesion/v1x")]
    [InlineData("/cohesionx")]
    public async Task FallbackServesApplicationRoutesBesideTheControlPlane(string path)
    {
        using TemporaryApplication content = new();
        content.WriteContent("index.html", "<html>index</html>");
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.AddViuApplication(options => options.ContentRootPath = content.ContentRootPath);
        await using WebApplication application = builder.Build();
        application.UseViuApplication();
        application.Use((context, next) =>
        {
            context.Response.StatusCode = HttpStatusCode.ServiceUnavailable;
            return Task.CompletedTask;
        });
        TestHttpContext context = new(path, HttpMethod.Get);
        await ((IWebApplicationPipelineBuilder)application).Build().ExecuteAsync(context);
        context.Response.StatusCode.ShouldBe(HttpStatusCode.Ok);
        context.ReadResponseBody().ShouldBe("<html>index</html>");
    }

    [Fact]
    public async Task ConfiguredControlPlanePathReplacesTheDefaultReservation()
    {
        using TemporaryApplication content = new();
        content.WriteContent("index.html", "<html>index</html>");
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.AddViuApplication(options =>
        {
            options.ContentRootPath = content.ContentRootPath;
            options.ControlPlanePath = "/_control/";
        });
        await using WebApplication application = builder.Build();
        application.UseViuApplication();
        application.Use((context, next) =>
        {
            context.Response.StatusCode = HttpStatusCode.ServiceUnavailable;
            return Task.CompletedTask;
        });
        IWebApplicationPipeline pipeline = ((IWebApplicationPipelineBuilder)application).Build();

        TestHttpContext reserved = new("/_control/readyz", HttpMethod.Get);
        await pipeline.ExecuteAsync(reserved);
        reserved.Response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

        TestHttpContext application1 = new("/cohesion/v1/anything", HttpMethod.Get);
        await pipeline.ExecuteAsync(application1);
        application1.Response.StatusCode.ShouldBe(HttpStatusCode.Ok);
        application1.ReadResponseBody().ShouldBe("<html>index</html>");
    }

    [Fact]
    public async Task ClientDocumentContainsOnlyPublicRuntimeFieldsAndIsNotCached()
    {
        using IDisposable scope = ResourceRuntime.CreateScope(new ResourceContext(
            applicationName: "docs", resourceName: "site", environmentName: "Production", gatewayName: "kubernetes",
            references: new Dictionary<string, Uri> { ["api:http"] = new("https://api.example:443/v1") },
            settings: new Dictionary<string, string> { ["password"] = "must-not-leak" },
            bootstrapCredential: Encoding.UTF8.GetBytes("private-token")));
        ViuApplicationMiddleware middleware = new(ViuApplicationConfiguration.Create(new ViuApplicationOptions()));
        TestHttpContext context = new("/_cohesion/client.json", HttpMethod.Get);
        await middleware.InvokeAsync(context, _ => throw new InvalidOperationException("Document fell through."));
        ViuClientConfiguration document = JsonSerializer.Deserialize(context.ReadResponseBody(),
            ViuClientConfigurationJsonContext.Default.ViuClientConfiguration)!;
        document.Schema.ShouldBe(ViuClientConfiguration.SchemaName);
        document.Application.ShouldBe("docs");
        document.Resource.ShouldBe("site");
        document.Environment.ShouldBe("Production");
        document.Gateway.ShouldBe("kubernetes");
        document.References["api"]["http"].ShouldBe("https://api.example:443/v1");
        context.Response.Headers[new HttpHeaderKey("Cache-Control")].ToString().ShouldBe("no-store");
        using JsonDocument json = JsonDocument.Parse(context.ReadResponseBody());
        int count = 0;
        foreach (JsonProperty property in json.RootElement.EnumerateObject()) { count++; }
        count.ShouldBe(6);
        context.ReadResponseBody().ShouldNotContain("must-not-leak");
        context.ReadResponseBody().ShouldNotContain("private-token");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StandaloneDocumentUsesWebHostEnvironmentAndCanBeDisabled(bool configureBuilder)
    {
        using IDisposable scope = ResourceRuntime.CreateScope(new ResourceContext(environmentName: "Ambient"));
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { Environment = "Staging" });
        if (configureBuilder) { builder.AddViuApplication(); }
        await using WebApplication application = builder.Build();
        application.UseViuApplication();
        TestHttpContext context = new("/_cohesion/client.json", HttpMethod.Get);
        await ((IWebApplicationPipelineBuilder)application).Build().ExecuteAsync(context);
        ViuClientConfiguration document = JsonSerializer.Deserialize(context.ReadResponseBody(),
            ViuClientConfigurationJsonContext.Default.ViuClientConfiguration)!;
        document.Environment.ShouldBe("Staging");
        document.References.ShouldBeEmpty();
        document.Resource.ShouldBeNull();

        ViuApplicationMiddleware disabled = new(ViuApplicationConfiguration.Create(new ViuApplicationOptions { ClientConfigurationPath = null }));
        bool continued = false;
        await disabled.InvokeAsync(new TestHttpContext("/_cohesion/client.json", HttpMethod.Get), _ =>
        {
            continued = true;
            return Task.CompletedTask;
        });
        continued.ShouldBeTrue();
    }

    [Theory]
    [InlineData("br")]
    [InlineData("gzip")]
    [InlineData("identity")]
    public async Task PhysicalPublishLayoutServesFingerprintedAssetsWithCompressionAndMimeType(string encoding)
    {
        using TemporaryApplication content = new();
        const string payload = "export const value = 42;";
        string path = content.WriteContent("_framework/app.abc123.js", payload);
        await using (FileStream file = File.Create(path + ".br"))
        await using (BrotliStream compressor = new(file, CompressionLevel.Fastest))
        {
            await compressor.WriteAsync(Encoding.UTF8.GetBytes(payload));
        }
        await using (FileStream file = File.Create(path + ".gz"))
        await using (GZipStream compressor = new(file, CompressionLevel.Fastest))
        {
            await compressor.WriteAsync(Encoding.UTF8.GetBytes(payload));
        }
        ViuApplicationMiddleware middleware = new(ViuApplicationConfiguration.Create(new ViuApplicationOptions { ContentRootPath = content.ContentRootPath }));
        TestHttpContext context = new("/_framework/app.abc123.js", HttpMethod.Get);
        context.Request.Headers[new HttpHeaderKey("Accept-Encoding")] = encoding;
        await middleware.InvokeAsync(context, _ => throw new InvalidOperationException("Asset fell through."));
        context.Response.StatusCode.ShouldBe(HttpStatusCode.Ok);
        context.Response.Headers[new HttpHeaderKey("Content-Type")].ToString().ShouldBe("text/javascript");
        context.Response.Headers[new HttpHeaderKey("Vary")].ToString().ShouldBe("Accept-Encoding");
        context.Response.Body.Position = 0;
        using Stream decoded = encoding == "br" ? new BrotliStream(context.Response.Body, CompressionMode.Decompress)
            : encoding == "gzip" ? new GZipStream(context.Response.Body, CompressionMode.Decompress) : context.Response.Body;
        using StreamReader reader = new(decoded);
        (await reader.ReadToEndAsync()).ShouldBe(payload);
    }
}
