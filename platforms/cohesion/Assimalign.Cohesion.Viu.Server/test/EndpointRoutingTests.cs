using System.Threading.Tasks;

using Assimalign.Cohesion.Http;
using Assimalign.Cohesion.Web;
using Assimalign.Cohesion.Web.Hosting;
using Assimalign.Cohesion.Web.Routing;

using Shouldly;
using Xunit;

namespace Assimalign.Cohesion.Viu.Server.Tests;

public sealed class EndpointRoutingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UseViuApplication_SelectedGetOrHeadEndpoint_ShouldReachTerminal(bool isHead)
    {
        using TemporaryApplication content = new();
        content.WriteContent("index.html", "<html>client</html>");
        WebApplicationBuilder builder = CreateBuilder(content);
        await using WebApplication application = builder.Build();
        int handlerRuns = 0;
        int downstreamRuns = 0;
        application.UseRouting();
        application.MapGet("/api/hello/{name}", (string name) =>
        {
            handlerRuns++;
            return $"Hello, {name}!";
        });
        application.UseViuApplication();
        application.Use((context, next) =>
        {
            downstreamRuns++;
            return next(context);
        });
        IWebApplicationPipeline pipeline = ((IWebApplicationPipelineBuilder)application).Build();
        TestHttpContext context = new("/api/hello/Ada", isHead ? HttpMethod.Head : HttpMethod.Get);

        await pipeline.ExecuteAsync(context);

        handlerRuns.ShouldBe(1);
        downstreamRuns.ShouldBe(1);
        context.Response.StatusCode.ShouldBe(HttpStatusCode.Ok);
        if (!isHead)
        {
            context.ReadResponseBody().ShouldBe("Hello, Ada!");
        }
        context.Response.Headers.ContainsKey(new HttpHeaderKey("Cache-Control")).ShouldBeFalse();
        context.Response.Headers.ContainsKey(new HttpHeaderKey("Cross-Origin-Opener-Policy")).ShouldBeFalse();
        context.Response.Headers.ContainsKey(new HttpHeaderKey("Cross-Origin-Embedder-Policy")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/asset.js")]
    [InlineData("/_cohesion/client.json")]
    public async Task UseViuApplication_SelectedEndpoint_ShouldTakePrecedenceOverViuContent(string path)
    {
        using TemporaryApplication content = new();
        content.WriteContent("index.html", "<html>client</html>");
        content.WriteContent("asset.js", "client asset");
        WebApplicationBuilder builder = CreateBuilder(content);
        await using WebApplication application = builder.Build();
        application.UseRouting();
        application.MapGet(path, () => "mapped endpoint");
        application.UseViuApplication();
        IWebApplicationPipeline pipeline = ((IWebApplicationPipelineBuilder)application).Build();
        TestHttpContext context = new(path, HttpMethod.Get);

        await pipeline.ExecuteAsync(context);

        context.Response.StatusCode.ShouldBe(HttpStatusCode.Ok);
        context.ReadResponseBody().ShouldBe("mapped endpoint");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UseViuApplication_SelectedMethodNotAllowedEndpoint_ShouldPreserveRoutingResponse(bool isHead)
    {
        using TemporaryApplication content = new();
        content.WriteContent("index.html", "<html>client</html>");
        WebApplicationBuilder builder = CreateBuilder(content);
        await using WebApplication application = builder.Build();
        int handlerRuns = 0;
        application.UseRouting();
        application.MapPost("/api/only-post", () =>
        {
            handlerRuns++;
            return "posted";
        });
        application.UseViuApplication();
        IWebApplicationPipeline pipeline = ((IWebApplicationPipelineBuilder)application).Build();
        TestHttpContext context = new("/api/only-post", isHead ? HttpMethod.Head : HttpMethod.Get);

        await pipeline.ExecuteAsync(context);

        handlerRuns.ShouldBe(0);
        context.Response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        context.Response.Headers[new HttpHeaderKey("Allow")].ToString().ShouldBe("POST");
        context.ReadResponseBody().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("/docs/guide", "<html>client</html>", 200)]
    [InlineData("/api/unmapped", "<html>client</html>", 200)]
    [InlineData("/asset.js", "client asset", 200)]
    [InlineData("/missing.js", "", 404)]
    public async Task UseViuApplication_UnselectedRequest_ShouldRetainAssetAndFallbackBehavior(
        string path, string expectedBody, int expectedStatus)
    {
        using TemporaryApplication content = new();
        content.WriteContent("index.html", "<html>client</html>");
        content.WriteContent("asset.js", "client asset");
        WebApplicationBuilder builder = CreateBuilder(content);
        await using WebApplication application = builder.Build();
        application.UseRouting();
        application.MapGet("/api/hello", () => "Hello!");
        application.UseViuApplication();
        IWebApplicationPipeline pipeline = ((IWebApplicationPipelineBuilder)application).Build();
        TestHttpContext context = new(path, HttpMethod.Get);

        await pipeline.ExecuteAsync(context);

        context.Response.StatusCode.Value.ShouldBe(expectedStatus);
        context.ReadResponseBody().ShouldBe(expectedBody);
    }

    private static WebApplicationBuilder CreateBuilder(TemporaryApplication content)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        ((IWebApplicationBuilder)builder).AddFeature(new RoutingBuilder().Build());
        builder.AddViuApplication(options =>
        {
            options.ContentRootPath = content.ContentRootPath;
            options.ApplyCrossOriginIsolationHeaders = true;
        });
        return builder;
    }
}
