using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Assimalign.Cohesion.Hosting.Resources;
using Assimalign.Cohesion.Web.Hosting;

using Shouldly;

using Xunit;

namespace Assimalign.Cohesion.Viu.Server.Tests;

public sealed class ClientOutputTests
{
    [Fact]
    public async Task AddViuClientInfersTheDocumentAndCapturesResourceIdentityBeforeWebBuild()
    {
        using TemporaryApplication content = new();
        content.WriteRootFile("viu-client.json", "{\"client\":\"Inferred.Client\",\"outputPath\":\"" +
            content.RootPath.Replace("\\", "\\\\") +
            "\",\"runtimeManifestPath\":\"runtime.json\",\"endpointsManifestPath\":\"endpoints.json\"}");
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        using (ResourceRuntime.CreateScope(new ResourceContext(
            applicationName: "captured-app", resourceName: "captured-web", environmentName: "Local",
            contentRootPath: content.RootPath)))
        {
            builder.Services.AddViuClient();
        }

        await using WebApplication application = builder.Build();

        ViuApplicationConfiguration configuration = application.Context.Features.OfType<ViuApplicationFeature>()
            .Single().Configuration;
        configuration.ContentRootPath.ShouldBe(content.ContentRootPath);
        configuration.RuntimeManifestPath.ShouldEndWith("runtime.json");
        configuration.ClientConfiguration!.Application.ShouldBe("captured-app");
        configuration.ClientConfiguration.Resource.ShouldBe("captured-web");
    }

    [Fact(DisplayName = "Viu Cohesion Web - client output: the document in the ambient content root selects the client build output")]
    public void UseClientOutput_WithDocumentInAmbientContentRoot_ShouldSelectClientBuildOutput()
    {
        // Arrange: an in-process member sees the gateway-staged content root, not its own bin.
        using TemporaryApplication contentRoot = new();
        using TemporaryApplication client = new();
        string output = client.RootPath + Path.DirectorySeparatorChar;
        contentRoot.WriteRootFile(
            "viu-client.json",
            "{\"client\":\"Acme.Docs.Client\",\"outputPath\":\"" + output.Replace("\\", "\\\\") +
            "\",\"runtimeManifestPath\":\"custom-runtime.json\",\"endpointsManifestPath\":\"custom-endpoints.json\"}");
        using IDisposable scope = ResourceRuntime.CreateScope(new ResourceContext(
            applicationName: "docs",
            resourceName: "docs-web",
            environmentName: "Local",
            gatewayName: "inprocess",
            contentRootPath: contentRoot.RootPath));
        ViuApplicationOptions options = new();

        // Act
        options.UseClientOutput("Acme.Docs.Client");

        // Assert
        options.ContentRootPath.ShouldBe(Path.Combine(output, "wwwroot"));
        options.RuntimeManifestPath.ShouldBe("custom-runtime.json");
        options.EndpointsManifestPath.ShouldBe("custom-endpoints.json");
        ViuApplicationOptions inferred = new();
        inferred.UseClientOutput();
        inferred.ContentRootPath.ShouldBe(options.ContentRootPath);
        inferred.RuntimeManifestPath.ShouldBe(options.RuntimeManifestPath);
    }

    [Fact(DisplayName = "Viu Cohesion Web - client output: a document for another client is not selected")]
    public void UseClientOutput_WithDocumentForAnotherClient_ShouldThrow()
    {
        // Arrange
        using TemporaryApplication contentRoot = new();
        contentRoot.WriteRootFile("viu-client.json", "{\"client\":\"Other.Client\",\"outputPath\":\"/clients/other/\"}");
        using IDisposable scope = ResourceRuntime.CreateScope(new ResourceContext(
            environmentName: "Local",
            contentRootPath: contentRoot.RootPath));
        ViuApplicationOptions options = new();

        // Act
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => options.UseClientOutput("Acme.Docs.Client"));

        // Assert
        exception.Message.ShouldContain("viu-client.json");
        exception.Message.ShouldContain("Acme.Docs.Client");
    }

    [Fact(DisplayName = "Viu Cohesion Web - client output: an invalid document names its path")]
    public void UseClientOutput_WithInvalidDocument_ShouldThrowWithPath()
    {
        // Arrange
        using TemporaryApplication contentRoot = new();
        string path = contentRoot.WriteRootFile("viu-client.json", "{not json");
        using IDisposable scope = ResourceRuntime.CreateScope(new ResourceContext(
            environmentName: "Local",
            contentRootPath: contentRoot.RootPath));
        ViuApplicationOptions options = new();

        // Act
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => options.UseClientOutput("Acme.Docs.Client"));

        // Assert
        exception.Message.ShouldContain(path);
    }
}
