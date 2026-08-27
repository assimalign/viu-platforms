using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.Web;
using Shouldly;
using Xunit;

namespace Assimalign.Viu.Cohesion.DevServer.Tests;

public sealed class TrackedHttpServerTests
{
    [Fact(DisplayName = "Viu Cohesion DevServer - host: serves an index over the packaged transport")]
    public async Task StartAsync_ViuApplication_ShouldServeAndStopCleanly()
    {
        using TemporaryViuApplication application = new();
        DevServerOptions options = new(
            application.ContentRootPath,
            application.EndpointsManifestPath,
            application.RuntimeManifestPath,
            ApplyCrossOriginIsolationHeaders: true,
            Urls: null);
        IReadOnlyList<DevServerAddress> addresses = DevServerAddressResolver.Resolve(options, aspNetCoreUrls: null);
        IWebApplicationPipeline pipeline = DevServerPipelineFactory.Create(options);
        await using TrackedHttpServer server = new(pipeline, addresses);
        using CancellationTokenSource timeout = new(System.TimeSpan.FromSeconds(15));

        await server.StartAsync(timeout.Token);
        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(addresses[0].Url, timeout.Token);
        string body = await response.Content.ReadAsStringAsync(timeout.Token);
        using HttpResponseMessage wasmResponse = await client.GetAsync(
            new System.Uri(addresses[0].Url, "probe.wasm"),
            timeout.Token);
        await server.StopAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.ShouldContain("<title>Viu test</title>");
        response.Headers.GetValues("Cross-Origin-Embedder-Policy").ShouldContain("require-corp");
        response.Headers.GetValues("Cross-Origin-Opener-Policy").ShouldContain("same-origin");
        wasmResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        wasmResponse.Content.Headers.ContentType.ShouldNotBeNull();
        wasmResponse.Content.Headers.ContentType.MediaType.ShouldBe("application/wasm");
        server.Completion.IsCompleted.ShouldBeTrue();
    }
}
