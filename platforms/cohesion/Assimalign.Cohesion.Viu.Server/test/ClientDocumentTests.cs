using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Assimalign.Cohesion.Http;
using Assimalign.Cohesion.Viu.Hosting;
using Assimalign.Cohesion.Web;
using Assimalign.Cohesion.Web.Hosting;

using Shouldly;
using Xunit;

namespace Assimalign.Cohesion.Viu.Server.Tests;

public sealed class ClientDocumentTests
{
    private const string DisplayPrefix = "Viu Cohesion Web - client document: ";

    [Fact(DisplayName = DisplayPrefix + "a managed instance publishes only its identity and the references it added, uncached")]
    public async Task InvokeAsync_InPlatformEnvironment_ShouldPublishOnlyPublicFields()
    {
        // Arrange
        ViuApplicationOptions options = new();
        options.AddClientReference("api", "http", new Uri("https://api.example:443/v1"));
        ViuApplicationMiddleware middleware;
        using (TestEnvironment.Enter(
            (AppEnvironment.Variables.Application, "docs"),
            (AppEnvironment.Variables.Resource, "site"),
            (AppEnvironment.Variables.Gateway, "kubernetes"),
            (AppEnvironment.Variables.Environment, "Production"),
            (AppEnvironment.Variables.Configuration("Database:Password"), "must-not-leak"),
            (AppEnvironment.Variables.BootstrapTokenPath, "private-token-path")))
        {
            middleware = new ViuApplicationMiddleware(ViuApplicationConfiguration.Create(options));
        }
        TestHttpContext context = new("/_cohesion/client.json", HttpMethod.Get);

        // Act
        await middleware.InvokeAsync(context, _ => throw new InvalidOperationException("Document fell through."));

        // Assert
        string body = context.ReadResponseBody();
        ViuClientConfiguration document = JsonSerializer.Deserialize(body,
            ViuClientConfigurationJsonContext.Default.ViuClientConfiguration)!;
        document.Schema.ShouldBe(ViuClientConfiguration.SchemaName);
        document.Application.ShouldBe("docs");
        document.Resource.ShouldBe("site");
        document.Environment.ShouldBe("Production");
        document.Gateway.ShouldBe("kubernetes");
        document.References["api"]["http"].ShouldBe("https://api.example:443/v1");
        context.Response.Headers[new HttpHeaderKey("Cache-Control")].ToString().ShouldBe("no-store");
        using JsonDocument json = JsonDocument.Parse(body);
        json.RootElement.EnumerateObject().Count().ShouldBe(6);
        body.ShouldNotContain("must-not-leak");
        body.ShouldNotContain("private-token-path");
    }

    [Theory(DisplayName = DisplayPrefix + "an unmanaged application reports its Web host environment, and the route can be disabled")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InvokeAsync_Unmanaged_ShouldUseTheWebHostEnvironment(bool configureBuilder)
    {
        // Arrange: the scope names an environment but no resource, as an unmanaged run does.
        using IDisposable scope = TestEnvironment.Enter((AppEnvironment.Variables.Environment, "Ambient"));
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { Environment = "Staging" });
        if (configureBuilder) { builder.AddViuApplication(); }
        await using WebApplication application = builder.Build();
        application.UseViuApplication();
        TestHttpContext context = new("/_cohesion/client.json", HttpMethod.Get);

        // Act
        await ((IWebApplicationPipelineBuilder)application).Build().ExecuteAsync(context);

        // Assert
        ViuClientConfiguration document = JsonSerializer.Deserialize(context.ReadResponseBody(),
            ViuClientConfigurationJsonContext.Default.ViuClientConfiguration)!;
        document.Environment.ShouldBe("Staging");
        document.References.ShouldBeEmpty();
        document.Resource.ShouldBeNull();
        document.Application.ShouldBeNull();
        document.Gateway.ShouldBeNull();

        ViuApplicationMiddleware disabled = new(ViuApplicationConfiguration.Create(new ViuApplicationOptions { ClientConfigurationPath = null }));
        bool continued = false;
        await disabled.InvokeAsync(new TestHttpContext("/_cohesion/client.json", HttpMethod.Get), _ =>
        {
            continued = true;
            return Task.CompletedTask;
        });
        continued.ShouldBeTrue();
    }

    [Fact(DisplayName = DisplayPrefix + "an unmanaged application keeps the references it added")]
    public async Task AddViuClient_UnmanagedWithReference_ShouldKeepTheReference()
    {
        // Arrange
        using TemporaryApplication content = new();
        content.WriteContent("index.html", "<html>index</html>");
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { Environment = "Development" });
        builder.Services.AddViuClient(viu =>
        {
            viu.ContentRootPath = content.ContentRootPath;
            viu.AddClientReference("api", "http", new Uri("http://localhost:5100"));
        });

        // Act
        await using WebApplication application = builder.Build();

        // Assert
        ViuClientConfiguration document = application.Context.Features.OfType<ViuApplicationFeature>()
            .Single().Configuration.ClientConfiguration.ShouldNotBeNull();
        document.Environment.ShouldBe("Development");
        document.Resource.ShouldBeNull();
        document.References["api"]["http"].ShouldBe("http://localhost:5100");
    }

    [Theory(DisplayName = DisplayPrefix + "a reference that is not an endpoint URL is refused")]
    [InlineData("/relative")]
    [InlineData("https://api.example:443/v1?key=secret")]
    [InlineData("https://user@api.example:443/")]
    public void AddClientReference_WithNonEndpointUrl_ShouldThrow(string url)
    {
        // Arrange
        ViuApplicationOptions options = new();

        // Act / Assert
        Should.Throw<ArgumentException>(() => options.AddClientReference("api", "http", new Uri(url, UriKind.RelativeOrAbsolute)));
    }

    [Fact(DisplayName = DisplayPrefix + "a later reference for the same endpoint replaces the earlier one")]
    public void AddClientReference_Twice_ShouldKeepTheLastUrl()
    {
        // Arrange
        ViuApplicationOptions options = new();

        // Act
        options.AddClientReference("api", "http", new Uri("http://localhost:5100"));
        options.AddClientReference("api", "http", new Uri("http://localhost:5200"));

        // Assert
        options.ClientReferences["api"]["http"].ShouldBe("http://localhost:5200");
    }

    [Theory(DisplayName = DisplayPrefix + "a health-probe route cannot be the document route")]
    [InlineData("/healthz")]
    [InlineData("/READYZ")]
    public void Create_WithProbeDocumentRoute_ShouldThrow(string route)
    {
        // Act / Assert
        Should.Throw<ArgumentException>(() => ViuApplicationConfiguration.Create(new ViuApplicationOptions { ClientConfigurationPath = route }));
    }
}
