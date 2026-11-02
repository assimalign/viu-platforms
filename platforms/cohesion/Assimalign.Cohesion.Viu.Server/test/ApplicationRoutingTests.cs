using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

using Assimalign.Cohesion.Http;
using Assimalign.Cohesion.Web;
using Assimalign.Cohesion.Web.Hosting;

using Shouldly;
using Xunit;

namespace Assimalign.Cohesion.Viu.Server.Tests;

public sealed class ApplicationRoutingTests
{
    private const string DisplayPrefix = "Viu Cohesion Web - application routing: ";

    [Theory(DisplayName = DisplayPrefix + "a health probe continues to the next middleware")]
    [InlineData("/readyz")]
    [InlineData("/livez")]
    [InlineData("/healthz")]
    [InlineData("/HEALTHZ")]
    public async Task Fallback_HealthProbe_ShouldPassToNextMiddleware(string path)
    {
        // Arrange
        using TemporaryApplication content = new();
        IWebApplicationPipeline pipeline = CreatePipeline(content);
        TestHttpContext context = new(path, HttpMethod.Get);

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        context.ReadResponseBody().ShouldBeEmpty();
    }

    [Theory(DisplayName = DisplayPrefix + "every other route, under /cohesion included, belongs to the browser application")]
    [InlineData("/cohesion")]
    [InlineData("/cohesion/v1")]
    [InlineData("/cohesion/v1/health")]
    [InlineData("/cohesion/database/sql")]
    [InlineData("/healthz/details")]
    public async Task Fallback_ApplicationRoute_ShouldServeIndex(string path)
    {
        // Arrange
        using TemporaryApplication content = new();
        IWebApplicationPipeline pipeline = CreatePipeline(content);
        TestHttpContext context = new(path, HttpMethod.Get);

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(HttpStatusCode.Ok);
        context.ReadResponseBody().ShouldBe("<html>index</html>");
    }

    [Theory(DisplayName = DisplayPrefix + "a published layout serves fingerprinted assets with compression and their media type")]
    [InlineData("br")]
    [InlineData("gzip")]
    [InlineData("identity")]
    public async Task InvokeAsync_PhysicalPublishLayout_ShouldServeCompressedAsset(string encoding)
    {
        // Arrange
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

        // Act
        await middleware.InvokeAsync(context, _ => throw new InvalidOperationException("Asset fell through."));

        // Assert
        context.Response.StatusCode.ShouldBe(HttpStatusCode.Ok);
        context.Response.Headers[new HttpHeaderKey("Content-Type")].ToString().ShouldBe("text/javascript");
        context.Response.Headers[new HttpHeaderKey("Vary")].ToString().ShouldBe("Accept-Encoding");
        context.Response.Body.Position = 0;
        using Stream decoded = encoding == "br" ? new BrotliStream(context.Response.Body, CompressionMode.Decompress)
            : encoding == "gzip" ? new GZipStream(context.Response.Body, CompressionMode.Decompress) : context.Response.Body;
        using StreamReader reader = new(decoded);
        (await reader.ReadToEndAsync()).ShouldBe(payload);
    }

    // The Viu middleware first, then a terminal that answers 503, so a request the middleware passes
    // on is told apart from one it serves.
    private static IWebApplicationPipeline CreatePipeline(TemporaryApplication content)
    {
        content.WriteContent("index.html", "<html>index</html>");
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.AddViuApplication(options => options.ContentRootPath = content.ContentRootPath);
        WebApplication application = builder.Build();
        application.UseViuApplication();
        application.Use((context, next) =>
        {
            context.Response.StatusCode = HttpStatusCode.ServiceUnavailable;
            return Task.CompletedTask;
        });
        return ((IWebApplicationPipelineBuilder)application).Build();
    }
}
