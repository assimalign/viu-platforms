using System.Threading.Tasks;
using Assimalign.Cohesion.Http;
using Assimalign.Viu.Cohesion.Web.Internal;
using Shouldly;
using Xunit;
using CohesionHttpMethod = Assimalign.Cohesion.Http.HttpMethod;

namespace Assimalign.Viu.Cohesion.Web.Tests;

public sealed class ViuApplicationMiddlewareTests
{
    [Fact(DisplayName = "Viu Cohesion Web - application middleware: serves the SPA fallback without caching")]
    public async Task InvokeAsync_UnknownRoute_ShouldServeIndexWithoutCaching()
    {
        // Arrange
        using TemporaryApplication application = new();
        application.WriteContent("index.html", "<html><body>Viu</body></html>");
        ViuApplicationMiddleware middleware = CreateMiddleware(application, refreshEndpoint: null);
        TestHttpContext context = new("/nested/route", CohesionHttpMethod.Get);
        bool passedThrough = false;

        // Act
        await middleware.InvokeAsync(context, _ => { passedThrough = true; return Task.CompletedTask; });

        // Assert
        passedThrough.ShouldBeFalse();
        context.ReadResponseBody().ShouldContain("Viu", Case.Sensitive);
        context.Response.Headers[new HttpHeaderKey("Cache-Control")].ToString().ShouldBe("no-store");
        context.Response.Headers[new HttpHeaderKey("Content-Type")].ToString().ShouldBe("text/html; charset=utf-8");
    }

    [Fact(DisplayName = "Viu Cohesion Web - application middleware: applies cross-origin isolation headers")]
    public async Task InvokeAsync_CrossOriginIsolationEnabled_ShouldWriteBothHeaders()
    {
        // Arrange
        using TemporaryApplication application = new();
        application.WriteContent("probe.wasm", "wasm");
        ViuApplicationMiddleware middleware = CreateMiddleware(application, refreshEndpoint: null, applyCrossOriginIsolation: true);
        TestHttpContext context = new("/probe.wasm", CohesionHttpMethod.Get);

        // Act
        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        // Assert
        context.Response.Headers[new HttpHeaderKey("Cross-Origin-Embedder-Policy")].ToString().ShouldBe("require-corp");
        context.Response.Headers[new HttpHeaderKey("Cross-Origin-Opener-Policy")].ToString().ShouldBe("same-origin");
        context.Response.Headers[new HttpHeaderKey("Content-Type")].ToString().ShouldBe("application/wasm");
    }

    [Fact(DisplayName = "Viu Cohesion Web - browser refresh: endpoint is gated by the watch environment contract")]
    public async Task InvokeAsync_RefreshDisabled_ShouldPassRefreshScriptRequestThrough()
    {
        // Arrange
        using TemporaryApplication application = new();
        application.WriteContent("index.html", "<html>fallback</html>");
        ViuApplicationMiddleware middleware = CreateMiddleware(application, refreshEndpoint: null);
        TestHttpContext context = new("/_framework/aspnetcore-browser-refresh.js", CohesionHttpMethod.Get);
        bool passedThrough = false;

        // Act
        await middleware.InvokeAsync(context, _ => { passedThrough = true; return Task.CompletedTask; });

        // Assert
        passedThrough.ShouldBeTrue();
        context.ReadResponseBody().ShouldBeEmpty();
    }

    [Theory(DisplayName = "Viu Cohesion Web - application middleware: delegates missing file and framework routes")]
    [InlineData("/missing.js")]
    [InlineData("/_framework/missing-resource")]
    public async Task InvokeAsync_MissingNonNavigationPath_ShouldInvokeNextMiddleware(string path)
    {
        // Arrange
        using TemporaryApplication application = new();
        application.WriteContent("index.html", "<html>fallback</html>");
        ViuApplicationMiddleware middleware = CreateMiddleware(application, refreshEndpoint: null);
        TestHttpContext context = new(path, CohesionHttpMethod.Get);
        bool passedThrough = false;

        // Act
        await middleware.InvokeAsync(context, _ =>
        {
            passedThrough = true;
            return Task.CompletedTask;
        });

        // Assert
        passedThrough.ShouldBeTrue();
        context.ReadResponseBody().ShouldBeEmpty();
    }

    [Fact(DisplayName = "Viu Cohesion Web - browser refresh: serves contract client and injects HTML")]
    public async Task InvokeAsync_RefreshEnabled_ShouldServeClientAndInjectHtml()
    {
        // Arrange
        using TemporaryApplication application = new();
        application.WriteContent("index.html", "<html><body>Viu</body></html>");
        ViuApplicationMiddleware middleware = CreateMiddleware(application, "ws://127.0.0.1:5001");
        TestHttpContext scriptContext = new("/_framework/aspnetcore-browser-refresh.js", CohesionHttpMethod.Get);
        TestHttpContext htmlContext = new("/", CohesionHttpMethod.Get);

        // Act
        await middleware.InvokeAsync(scriptContext, _ => Task.CompletedTask);
        await middleware.InvokeAsync(htmlContext, _ => Task.CompletedTask);

        // Assert
        string script = scriptContext.ReadResponseBody();
        script.ShouldContain("ws://127.0.0.1:5001", Case.Sensitive);
        script.ShouldContain("RSA-OAEP", Case.Sensitive);
        script.ShouldContain("ApplyManagedCodeUpdates", Case.Sensitive);
        htmlContext.ReadResponseBody().ShouldContain(
            "<script src=\"/_framework/aspnetcore-browser-refresh.js\"></script></body>",
            Case.Sensitive);
    }

    [Fact(DisplayName = "Viu Cohesion Web - hot reload cache: POST updates are returned by GET")]
    public async Task InvokeAsync_HotReloadPostThenGet_ShouldReturnCachedUpdateArray()
    {
        // Arrange
        using TemporaryApplication application = new();
        ViuApplicationMiddleware middleware = CreateMiddleware(application, "ws://127.0.0.1:5001");
        TestHttpContext post = new(
            "/_framework/blazor-hotreload",
            CohesionHttpMethod.Post,
            "{\"id\":1,\"deltas\":[]}");
        TestHttpContext get = new("/_framework/blazor-hotreload", CohesionHttpMethod.Get);

        // Act
        await middleware.InvokeAsync(post, _ => Task.CompletedTask);
        await middleware.InvokeAsync(get, _ => Task.CompletedTask);

        // Assert
        get.ReadResponseBody().ShouldBe("[{\"id\":1,\"deltas\":[]}]");
    }

    [Fact(DisplayName = "Viu Cohesion Web - hot reload cache: empty GET returns no content")]
    public async Task InvokeAsync_HotReloadEmptyGet_ShouldReturnNoContent()
    {
        // Arrange
        using TemporaryApplication application = new();
        ViuApplicationMiddleware middleware = CreateMiddleware(application, "ws://127.0.0.1:5001");
        TestHttpContext context = new("/_framework/blazor-hotreload", CohesionHttpMethod.Get);

        // Act
        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        // Assert
        context.Response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        context.ReadResponseBody().ShouldBeEmpty();
    }

    [Fact(DisplayName = "Viu Cohesion Web - hot reload cache: ignores duplicate and older update identifiers")]
    public async Task InvokeAsync_HotReloadOutOfOrderPosts_ShouldKeepOnlyIncreasingUpdates()
    {
        // Arrange
        using TemporaryApplication application = new();
        ViuApplicationMiddleware middleware = CreateMiddleware(application, "ws://127.0.0.1:5001");
        TestHttpContext first = new("/_framework/blazor-hotreload", CohesionHttpMethod.Post, "{\"id\":2,\"deltas\":[]}");
        TestHttpContext duplicate = new("/_framework/blazor-hotreload", CohesionHttpMethod.Post, "{\"id\":2,\"deltas\":[1]}");
        TestHttpContext older = new("/_framework/blazor-hotreload", CohesionHttpMethod.Post, "{\"id\":1,\"deltas\":[1]}");
        TestHttpContext get = new("/_framework/blazor-hotreload", CohesionHttpMethod.Get);

        // Act
        await middleware.InvokeAsync(first, _ => Task.CompletedTask);
        await middleware.InvokeAsync(duplicate, _ => Task.CompletedTask);
        await middleware.InvokeAsync(older, _ => Task.CompletedTask);
        await middleware.InvokeAsync(get, _ => Task.CompletedTask);

        // Assert
        get.ReadResponseBody().ShouldBe("[{\"id\":2,\"deltas\":[]}]");
    }

    [Fact(DisplayName = "Viu Cohesion Web - browser refresh: exposes the functional CSS cache-clear endpoint")]
    public async Task InvokeAsync_ClearBrowserCache_ShouldReturnClearSiteDataHeader()
    {
        // Arrange
        using TemporaryApplication application = new();
        ViuApplicationMiddleware middleware = CreateMiddleware(application, "ws://127.0.0.1:5001");
        TestHttpContext context = new("/_framework/clear-browser-cache", CohesionHttpMethod.Get);

        // Act
        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        // Assert
        context.Response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        context.Response.Headers[new HttpHeaderKey("Clear-Site-Data")].ToString().ShouldBe("\"cache\"");
    }

    [Fact(DisplayName = "Viu Cohesion Web - browser refresh: honors the watch virtual directory")]
    public async Task InvokeAsync_VirtualDirectory_ShouldPrefixRefreshRoutesAndInjectedSource()
    {
        // Arrange
        using TemporaryApplication application = new();
        application.WriteContent("index.html", "<html><body>Viu</body></html>");
        ViuApplicationMiddleware middleware = CreateMiddleware(
            application,
            "ws://127.0.0.1:5001",
            virtualDirectory: "/nested/");
        TestHttpContext script = new(
            "/nested/_framework/aspnetcore-browser-refresh.js",
            CohesionHttpMethod.Get);
        TestHttpContext html = new("/", CohesionHttpMethod.Get);

        // Act
        await middleware.InvokeAsync(script, _ => Task.CompletedTask);
        await middleware.InvokeAsync(html, _ => Task.CompletedTask);

        // Assert
        script.Response.StatusCode.ShouldBe(HttpStatusCode.Ok);
        script.ReadResponseBody().ShouldContain("const virtualDirectory = '/nested/'", Case.Sensitive);
        html.ReadResponseBody().ShouldContain(
            "<script src=\"/nested/_framework/aspnetcore-browser-refresh.js\"></script>",
            Case.Sensitive);
    }

    [Fact(DisplayName = "Viu Cohesion Web - application middleware: can delegate missing routes without SPA fallback")]
    public async Task InvokeAsync_SpaFallbackDisabled_ShouldDelegateMissingRoute()
    {
        // Arrange
        using TemporaryApplication application = new();
        application.WriteContent("index.html", "<html>fallback</html>");
        ViuApplicationMiddleware middleware = CreateMiddleware(
            application,
            refreshEndpoint: null,
            enableSpaFallback: false);
        TestHttpContext context = new("/server-rendered", CohesionHttpMethod.Get);
        bool passedThrough = false;

        // Act
        await middleware.InvokeAsync(context, _ =>
        {
            passedThrough = true;
            return Task.CompletedTask;
        });

        // Assert
        passedThrough.ShouldBeTrue();
        context.ReadResponseBody().ShouldBeEmpty();
    }

    [Theory(DisplayName = "Viu Cohesion Web - application middleware: rejects unsafe request paths")]
    [InlineData("/../secret")]
    [InlineData("/safe/./secret")]
    [InlineData("/file.txt:stream")]
    [InlineData("/safe\\secret")]
    [InlineData("/bad|path")]
    public async Task InvokeAsync_UnsafePath_ShouldReturnNotFound(string path)
    {
        // Arrange
        using TemporaryApplication application = new();
        application.WriteContent("index.html", "<html>fallback</html>");
        ViuApplicationMiddleware middleware = CreateMiddleware(application, refreshEndpoint: null);
        TestHttpContext context = new(path, CohesionHttpMethod.Get);
        bool passedThrough = false;

        // Act
        await middleware.InvokeAsync(context, _ =>
        {
            passedThrough = true;
            return Task.CompletedTask;
        });

        // Assert
        context.Response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        context.ReadResponseBody().ShouldBeEmpty();
        passedThrough.ShouldBeFalse();
    }

    private static ViuApplicationMiddleware CreateMiddleware(
        TemporaryApplication application,
        string? refreshEndpoint,
        bool applyCrossOriginIsolation = false,
        string virtualDirectory = "",
        bool enableSpaFallback = true)
    {
        ViuApplicationConfiguration configuration = new(
            application.ContentRootPath,
            RuntimeManifestPath: null,
            EndpointsManifestPath: null,
            applyCrossOriginIsolation,
            new BrowserRefreshConfiguration(refreshEndpoint ?? string.Empty, string.Empty, virtualDirectory),
            enableSpaFallback);
        return new ViuApplicationMiddleware(configuration);
    }
}
