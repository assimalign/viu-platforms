using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.ApplicationModel;

using Shouldly;
using Xunit;

namespace Assimalign.Cohesion.Viu.ApplicationModel.Tests;

public sealed class ViuWebResourceTests
{
    [Theory]
    [InlineData(ViuWebManifestProperties.Client, "")]
    [InlineData(ViuWebManifestProperties.SpaFallback, "perhaps")]
    [InlineData(ViuWebManifestProperties.BasePath, "relative")]
    [InlineData(ViuWebManifestProperties.BasePath, "//another-host")]
    [InlineData(ViuWebManifestProperties.Client, null)]
    [InlineData(ViuWebManifestProperties.BasePath, null)]
    public void InvalidPropertyNamesTheBuildProperty(string key, string? value)
    {
        ResourceManifest source = CreateManifest();
        Dictionary<string, string> properties = new(source.Properties) { [key] = value! };
        ArgumentException exception = Should.Throw<ArgumentException>(() => Application.CreateBuilder()
            .AddViuWeb(source with { Properties = properties }));
        exception.Message.ShouldContain(key);
        exception.Message.ShouldContain("ViuClient=true");
    }

    [Theory]
    [InlineData(ViuWebManifestProperties.Client)]
    [InlineData(ViuWebManifestProperties.SpaFallback)]
    [InlineData(ViuWebManifestProperties.BasePath)]
    public void MissingPropertyIsActionable(string key)
    {
        ResourceManifest source = CreateManifest();
        Dictionary<string, string> properties = new(source.Properties);
        properties.Remove(key);
        Should.Throw<ArgumentException>(() => Application.CreateBuilder()
            .AddViuWeb(source with { Properties = properties })).Message.ShouldContain(key);
    }

    [Fact]
    public void ValidationCanBeDisabledButWebKindIsAlwaysRequired()
    {
        ViuWebResourceOptions options = new() { ValidateClient = false };
        ResourceManifest manifest = CreateManifest() with { Properties = new Dictionary<string, string>() };
        Application.CreateBuilder().AddViuWeb(manifest, options).ClientAssembly.ShouldBeEmpty();
        Should.Throw<ArgumentException>(() => Application.CreateBuilder().AddViuWeb(manifest with { Kind = "Database" }, options));
    }

    [Fact]
    public void DescriptorWrapsWebAndPreservesDependenciesAndPlans()
    {
        IApplicationBuilder builder = Application.CreateBuilder("docs", ["--mode", "describe"]);
        builder.UseGateway(new TestGateway());
        IViuWebResourceDescriptor descriptor = builder.AddViuWeb(CreateManifest(), new() { Replicas = 3 });
        IWebResourceDescriptor dependency = builder.AddWeb(CreateManifest() with { Name = "api" });
        descriptor.DependsOn(dependency).ShouldBeSameAs(descriptor);
        descriptor.Dependencies.ShouldContain(dependency);
        descriptor.Web.Resource.ShouldBeSameAs(descriptor.Resource);
        descriptor.ClientAssembly.ShouldBe("Acme.Docs.Client");
        descriptor.SpaFallback.ShouldBeTrue();
        descriptor.BasePath.ShouldBe("/");
        descriptor.Resource.ShouldBeOfType<WebResource>();

        IApplication actual = builder.Build();
        IApplicationBuilder webBuilder = Application.CreateBuilder("docs", ["--mode", "describe"]);
        webBuilder.UseGateway(new TestGateway());
        IWebResourceDescriptor web = webBuilder.AddWeb(CreateManifest(), new() { Replicas = 3 });
        web.DependsOn(webBuilder.AddWeb(CreateManifest() with { Name = "api" }));
        IApplication expected = webBuilder.Build();
        actual.Model.Plans.Count.ShouldBe(expected.Model.Plans.Count);
        for (int index = 0; index < actual.Model.Plans.Count; index++)
        {
            JsonSerializer.Serialize(actual.Model.Plans[index], ResourcePlanJsonContext.Default.ResourcePlan)
                .ShouldBe(JsonSerializer.Serialize(expected.Model.Plans[index], ResourcePlanJsonContext.Default.ResourcePlan));
        }
    }

    [Fact]
    public void TryReadRejectsInvalidMetadataAndPreservesFalseFallback()
    {
        ResourceManifest manifest = CreateManifest();
        Dictionary<string, string> properties = new(manifest.Properties) { [ViuWebManifestProperties.SpaFallback] = "false" };
        ViuWebManifestProperties.TryRead(manifest with { Properties = properties }, out string client, out bool fallback, out string path).ShouldBeTrue();
        client.ShouldBe("Acme.Docs.Client");
        fallback.ShouldBeFalse();
        path.ShouldBe("/");
    }

    private sealed class TestGateway : IApplicationGateway
    {
        public ResourceName Name => "test";
        public void Validate(IApplicationModel model) { }
        public Task StartAsync(IApplicationModel model, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReconcileAsync(IApplicationModel model, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UninstallAsync(IApplicationModel model, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static ResourceManifest CreateManifest() => JsonSerializer.Deserialize(
        """
        {
          "schema":"cohesion/resource/v1", "name":"docs-web", "kind":"Web", "application":"docs",
          "applicationModel":"Assimalign.Cohesion.Web.ApplicationModel",
          "artifact":{"assembly":"Acme.Docs.Web","composable":true},
          "endpoints":[{"name":"http","scheme":"http","protocol":"tcp","containerPort":8080,"public":true}],
          "mounts":[], "settings":[], "references":[], "commands":[], "environment":{},
          "probes":{"readiness":{"endpoint":"http","http":"/readyz"},"liveness":{"endpoint":"http","http":"/livez"}},
          "controlPlane":{"endpoint":"http","path":"/cohesion/v1"},
          "lifecycle":{"workload":"Deployment","replicas":1,"stopGraceSeconds":30,"restartPolicy":"OnFailure","exitCodes":"cohesion/sysexits/v1"},
          "properties":{"web.viu.client":"Acme.Docs.Client","web.viu.spa-fallback":"true","web.viu.base-path":"/"}
        }
        """, ResourceManifestJsonContext.Default.ResourceManifest)!;
}
