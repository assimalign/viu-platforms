using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Assimalign.Cohesion.ApplicationModel;
using Assimalign.Cohesion.ApplicationModel.Gateway;
using Assimalign.Cohesion.ApplicationModel.Gateway.InProcess;
using Assimalign.Cohesion.Viu.Hosting;

using Shouldly;
using Xunit;

namespace Assimalign.Cohesion.Viu.Server.Tests;

/// <summary>
/// A Viu site under a gateway that runs it in its own process: the site is an ordinary Web
/// application, the gateway hands it its port and settings, and its client document reports the
/// identity its environment scope carries.
/// </summary>
public sealed class InProcessGatewayTests
{
    private const string DisplayPrefix = "Viu Cohesion Web - in-process gateway: ";

    [Fact(DisplayName = DisplayPrefix + "a managed site serves its client and reports its identity and the setting it published")]
    public async Task RunAsync_WithViuSite_ShouldServeTheClientAndItsIdentity()
    {
        // Arrange
        using TemporaryApplication content = new();
        content.WriteContent("index.html", "<html>site</html>");
        SiteProgram.ContentRootPath = content.ContentRootPath;
        await using var infrastructure = new InProcessInfrastructure();
        infrastructure.AddProgram("Web", SiteProgram.ResourceName, SiteProgram.MainAsync);

        ApplicationGatewayBuilder builder = ApplicationGateway.CreateBuilder("docs").UseInProcess(infrastructure);
        builder.StartupTimeout = TimeSpan.FromSeconds(30);
        builder.AddResource("Web", SiteProgram.ResourceName).WithSetting("Api:Url", "http://127.0.0.1:5999");
        ApplicationGateway gateway = builder.Build();

        try
        {
            // Act
            await gateway.StartAsync().WaitAsync(TimeSpan.FromSeconds(30));
            IApplicationGatewayDeployment site = gateway.Resources.Single();
            using HttpClient client = new() { BaseAddress = new Uri(site.Outputs[OutputName.Endpoint]) };
            string index = await GetAsync(client, "/");
            ViuClientConfiguration document = JsonSerializer.Deserialize(await GetAsync(client, "/_cohesion/client.json"),
                ViuClientConfigurationJsonContext.Default.ViuClientConfiguration)!;

            // Assert
            index.ShouldBe("<html>site</html>");
            document.Application.ShouldBe("docs");
            document.Resource.ShouldBe(SiteProgram.ResourceName);
            document.Gateway.ShouldBe("inprocess");
            document.References["api"]["http"].ShouldBe("http://127.0.0.1:5999");
            site.Instances.ShouldHaveSingleItem().Deployment.ShouldNotBeNull();
        }
        finally
        {
            await gateway.StopAsync();
            await gateway.Completion;
        }
    }

    // An instance registers before its servers start, so the first request can arrive before the
    // application listens.
    private static async Task<string> GetAsync(HttpClient client, string path)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return await client.GetStringAsync(path);
            }
            catch (HttpRequestException) when (attempt < 100)
            {
                await Task.Delay(50);
            }
        }
    }
}
