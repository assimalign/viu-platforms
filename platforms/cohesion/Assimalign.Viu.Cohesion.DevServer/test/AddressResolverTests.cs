using System;
using System.Collections.Generic;
using System.Net;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Cohesion.DevServer.Tests;

public sealed class AddressResolverTests
{
    [Fact(DisplayName = "Viu Cohesion DevServer - URLs: ASPNETCORE_URLS wins over the command line")]
    public void Resolve_AspNetCoreUrlsPresent_ShouldOverrideCommandLineUrls()
    {
        DevServerOptions options = CreateOptions("http://127.0.0.1:5100");

        IReadOnlyList<DevServerAddress> addresses = DevServerAddressResolver.Resolve(
            options,
            "http://127.0.0.1:5200");

        addresses.Count.ShouldBe(1);
        addresses[0].Url.Port.ShouldBe(5200);
    }

    [Fact(DisplayName = "Viu Cohesion DevServer - URLs: resolves every semicolon-separated URL")]
    public void Resolve_MultipleUrls_ShouldReturnEveryDistinctAddress()
    {
        DevServerOptions options = CreateOptions("http://127.0.0.1:5100;http://localhost:5101");

        IReadOnlyList<DevServerAddress> addresses = DevServerAddressResolver.Resolve(options, aspNetCoreUrls: null);

        addresses.Count.ShouldBe(2);
        addresses[0].EndPoint.ShouldBe(new IPEndPoint(IPAddress.Loopback, 5100));
        addresses[1].EndPoint.ShouldBe(new IPEndPoint(IPAddress.Loopback, 5101));
    }

    [Fact(DisplayName = "Viu Cohesion DevServer - URLs: defaults to a reserved loopback port")]
    public void Resolve_NoConfiguredUrls_ShouldReturnExplicitLoopbackAddress()
    {
        DevServerOptions options = CreateOptions(urls: null);

        IReadOnlyList<DevServerAddress> addresses = DevServerAddressResolver.Resolve(options, aspNetCoreUrls: null);

        addresses.Count.ShouldBe(1);
        addresses[0].EndPoint.Address.ShouldBe(IPAddress.Loopback);
        addresses[0].EndPoint.Port.ShouldBeGreaterThan(0);
        addresses[0].Url.Port.ShouldBe(addresses[0].EndPoint.Port);
    }

    [Fact(DisplayName = "Viu Cohesion DevServer - URLs: rejects HTTPS without a TLS contract")]
    public void Resolve_HttpsUrl_ShouldRejectConfiguration()
    {
        DevServerOptions options = CreateOptions("https://127.0.0.1:5100");

        Action action = () => DevServerAddressResolver.Resolve(options, aspNetCoreUrls: null);

        action.ShouldThrow<CommandLineException>().Message.ShouldContain("HTTP only");
    }

    private static DevServerOptions CreateOptions(string? urls)
    {
        return new DevServerOptions("content", "endpoints", "runtime", false, urls);
    }
}
