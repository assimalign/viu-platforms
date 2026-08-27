using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Assimalign.Viu.Cohesion.DevServer;

internal static class DevServerAddressResolver
{
    public static IReadOnlyList<DevServerAddress> Resolve(DevServerOptions options, string? aspNetCoreUrls)
    {
        ArgumentNullException.ThrowIfNull(options);

        string? configuredUrls = !string.IsNullOrWhiteSpace(aspNetCoreUrls)
            ? aspNetCoreUrls
            : options.Urls;

        if (string.IsNullOrWhiteSpace(configuredUrls))
        {
            int port = ReserveLoopbackPort();
            return [CreateAddress(new Uri($"http://127.0.0.1:{port}/", UriKind.Absolute))];
        }

        List<DevServerAddress> addresses = [];
        HashSet<string> uniqueUrls = new(StringComparer.OrdinalIgnoreCase);
        foreach (string candidate in configuredUrls.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? url))
            {
                throw new CommandLineException($"URL '{candidate}' is not an absolute URL.");
            }

            if (!url.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                throw new CommandLineException($"URL '{candidate}' is not supported. The development server currently supports HTTP only.");
            }

            if (url.AbsolutePath != "/" || !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment))
            {
                throw new CommandLineException($"URL '{candidate}' must not contain a path, query, or fragment.");
            }

            Uri normalizedUrl = url.Port == 0
                ? new UriBuilder(url) { Port = ReserveLoopbackPort() }.Uri
                : new UriBuilder(url) { Path = "/" }.Uri;

            if (uniqueUrls.Add(normalizedUrl.AbsoluteUri))
            {
                addresses.Add(CreateAddress(normalizedUrl));
            }
        }

        if (addresses.Count == 0)
        {
            throw new CommandLineException("The configured URL list did not contain any URLs.");
        }

        return addresses;
    }

    private static DevServerAddress CreateAddress(Uri url)
    {
        IPAddress address;
        if (url.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            address = IPAddress.Loopback;
        }
        else if (!IPAddress.TryParse(url.Host, out address!))
        {
            address = Dns.GetHostAddresses(url.DnsSafeHost)
                .FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork)
                ?? throw new CommandLineException($"Host '{url.Host}' did not resolve to an IPv4 address.");
        }

        return new DevServerAddress(url, new IPEndPoint(address, url.Port));
    }

    private static int ReserveLoopbackPort()
    {
        TcpListener reservation = new(IPAddress.Loopback, 0);
        reservation.Start();
        try
        {
            return ((IPEndPoint)reservation.LocalEndpoint).Port;
        }
        finally
        {
            reservation.Stop();
        }
    }
}
