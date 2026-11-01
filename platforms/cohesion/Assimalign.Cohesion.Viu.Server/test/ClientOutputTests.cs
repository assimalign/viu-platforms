using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Assimalign.Cohesion.Web.Hosting;

using Shouldly;

using Xunit;

namespace Assimalign.Cohesion.Viu.Server.Tests;

public sealed class ClientOutputTests
{
    [Fact(DisplayName = "Viu Cohesion Web - client output: AddViuClient infers the document and captures the instance identity before the Web build")]
    public async Task AddViuClient_InPlatformEnvironment_ShouldInferTheDocumentAndCaptureIdentity()
    {
        // Arrange
        using TemporaryApplication content = new();
        content.WriteRootFile("Inferred.Client.viu-client.json", "{\"client\":\"Inferred.Client\",\"outputPath\":\"" +
            content.RootPath.Replace("\\", "\\\\") +
            "\",\"runtimeManifestPath\":\"runtime.json\",\"endpointsManifestPath\":\"endpoints.json\"}");
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        using (TestEnvironment.Enter(
            (AppEnvironment.Variables.Application, "captured-app"),
            (AppEnvironment.Variables.Resource, "captured-web"),
            (AppEnvironment.Variables.Environment, "Local"),
            (AppEnvironment.Variables.ContentRoot, content.RootPath)))
        {
            // Act
            builder.Services.AddViuClient();
        }

        await using WebApplication application = builder.Build();

        // Assert
        ViuApplicationConfiguration configuration = application.Context.Features.OfType<ViuApplicationFeature>()
            .Single().Configuration;
        configuration.ContentRootPath.ShouldBe(content.ContentRootPath);
        configuration.RuntimeManifestPath.ShouldNotBeNull().ShouldEndWith("runtime.json");
        configuration.ClientConfiguration!.Application.ShouldBe("captured-app");
        configuration.ClientConfiguration.Resource.ShouldBe("captured-web");
    }

    [Fact(DisplayName = "Viu Cohesion Web - client output: the document in the content root the platform names selects the client build output")]
    public void UseClientOutput_WithDocumentInPlatformContentRoot_ShouldSelectClientBuildOutput()
    {
        // Arrange: the platform names a content root of its own, not the executable's directory.
        using TemporaryApplication contentRoot = new();
        using TemporaryApplication client = new();
        string output = client.RootPath + Path.DirectorySeparatorChar;
        contentRoot.WriteRootFile(
            "Acme.Docs.Client.viu-client.json",
            "{\"client\":\"Acme.Docs.Client\",\"outputPath\":\"" + output.Replace("\\", "\\\\") +
            "\",\"runtimeManifestPath\":\"custom-runtime.json\",\"endpointsManifestPath\":\"custom-endpoints.json\"}");
        using IDisposable scope = TestEnvironment.Enter(
            (AppEnvironment.Variables.Application, "docs"),
            (AppEnvironment.Variables.Resource, "docs-web"),
            (AppEnvironment.Variables.Gateway, "inprocess"),
            (AppEnvironment.Variables.ContentRoot, contentRoot.RootPath));
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

    [Fact(DisplayName = "Viu Cohesion Web - client output: beside a gateway that runs two Viu sites, each named client selects its own document and inference refuses to guess")]
    public void UseClientOutput_WithDocumentsForTwoClients_ShouldSelectByNameAndRefuseInference()
    {
        // Arrange: a gateway referencing two Web projects receives both clients' documents.
        using TemporaryApplication contentRoot = new();
        using TemporaryApplication docs = new();
        using TemporaryApplication shop = new();
        contentRoot.WriteRootFile("Acme.Docs.Client.viu-client.json", Document("Acme.Docs.Client", docs.RootPath));
        contentRoot.WriteRootFile("Acme.Shop.Client.viu-client.json", Document("Acme.Shop.Client", shop.RootPath));
        using IDisposable scope = TestEnvironment.Enter((AppEnvironment.Variables.ContentRoot, contentRoot.RootPath));
        ViuApplicationOptions docsOptions = new();
        ViuApplicationOptions shopOptions = new();

        // Act
        docsOptions.UseClientOutput("Acme.Docs.Client");
        shopOptions.UseClientOutput("Acme.Shop.Client");
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => new ViuApplicationOptions().UseClientOutput());

        // Assert
        docsOptions.ContentRootPath.ShouldBe(Path.Combine(docs.RootPath, "wwwroot"));
        shopOptions.ContentRootPath.ShouldBe(Path.Combine(shop.RootPath, "wwwroot"));
        exception.Message.ShouldContain("Acme.Docs.Client.viu-client.json");
        exception.Message.ShouldContain("Acme.Shop.Client.viu-client.json");
        exception.Message.ShouldContain("UseClientOutput");

        static string Document(string client, string output) =>
            "{\"client\":\"" + client + "\",\"outputPath\":\"" + output.Replace("\\", "\\\\") +
            "\",\"runtimeManifestPath\":\"runtime.json\",\"endpointsManifestPath\":\"endpoints.json\"}";
    }

    [Fact(DisplayName = "Viu Cohesion Web - client output: a document for another client is not selected")]
    public void UseClientOutput_WithDocumentForAnotherClient_ShouldThrow()
    {
        // Arrange
        using TemporaryApplication contentRoot = new();
        contentRoot.WriteRootFile("Other.Client.viu-client.json", "{\"client\":\"Other.Client\",\"outputPath\":\"/clients/other/\"}");
        using IDisposable scope = TestEnvironment.Enter((AppEnvironment.Variables.ContentRoot, contentRoot.RootPath));
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
        string path = contentRoot.WriteRootFile("Acme.Docs.Client.viu-client.json", "{not json");
        using IDisposable scope = TestEnvironment.Enter((AppEnvironment.Variables.ContentRoot, contentRoot.RootPath));
        ViuApplicationOptions options = new();

        // Act
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => options.UseClientOutput("Acme.Docs.Client"));

        // Assert
        exception.Message.ShouldContain(path);
    }
}
