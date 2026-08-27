using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Web;
using Assimalign.Cohesion.Web.Hosting;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Cohesion.DevServer.Tests;

public sealed class DevServerApplicationTests
{
    [Fact(DisplayName = "Viu Cohesion DevServer - application: serves an index through Web.Hosting")]
    public async Task StartAsync_ViuApplication_ShouldServeAndStopCleanly()
    {
        // Arrange
        using TemporaryViuApplication content = new();
        DevServerOptions options = new(
            content.ContentRootPath,
            content.EndpointsManifestPath,
            content.RuntimeManifestPath,
            ApplyCrossOriginIsolationHeaders: true,
            Urls: null);
        IReadOnlyList<DevServerAddress> addresses = DevServerAddressResolver.Resolve(options, aspNetCoreUrls: null);
        await using WebApplication application = DevServerApplicationFactory.Create(options, addresses);
        IWebApplicationServer server = application.Context.ServiceProvider.GetRequiredService<IWebApplicationServer>();
        using CancellationTokenSource timeout = new(System.TimeSpan.FromSeconds(15));

        // Act
        await server.StartAsync(timeout.Token);
        await PortReadinessProbe.WaitAsync(addresses[0], timeout.Token);
        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(addresses[0].Url, timeout.Token);
        string body = await response.Content.ReadAsStringAsync(timeout.Token);
        using HttpResponseMessage wasmResponse = await client.GetAsync(
            new System.Uri(addresses[0].Url, "probe.wasm"),
            timeout.Token);
        await server.StopAsync(timeout.Token);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.ShouldContain("<title>Viu test</title>", Case.Sensitive);
        response.Headers.GetValues("Cross-Origin-Embedder-Policy").ShouldContain("require-corp");
        response.Headers.GetValues("Cross-Origin-Opener-Policy").ShouldContain("same-origin");
        wasmResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        wasmResponse.Content.Headers.ContentType.ShouldNotBeNull();
        wasmResponse.Content.Headers.ContentType.MediaType.ShouldBe("application/wasm");
    }
}
