using System;

namespace Assimalign.Cohesion.Viu.Server;

internal sealed record BrowserRefreshConfiguration(
    string WebSocketEndpoints,
    string ServerKey,
    string VirtualDirectory)
{
    public bool IsEnabled => !string.IsNullOrWhiteSpace(WebSocketEndpoints);

    public string GetApplicationPath(string path)
    {
        string applicationPath = "/" + path.TrimStart('/');
        string virtualDirectory = VirtualDirectory.Trim().Trim('/');
        return virtualDirectory.Length == 0
            ? applicationPath
            : "/" + virtualDirectory + applicationPath;
    }

    public static BrowserRefreshConfiguration FromEnvironment()
    {
        return new BrowserRefreshConfiguration(
            Environment.GetEnvironmentVariable("ASPNETCORE_AUTO_RELOAD_WS_ENDPOINT") ?? string.Empty,
            Environment.GetEnvironmentVariable("ASPNETCORE_AUTO_RELOAD_WS_KEY") ?? string.Empty,
            Environment.GetEnvironmentVariable("ASPNETCORE_AUTO_RELOAD_VDIR") ?? string.Empty);
    }
}
