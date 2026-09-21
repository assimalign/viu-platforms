using System;

namespace Assimalign.Cohesion.Viu.Hosting.Browser;

/// <summary>Configures the client document request made before the Viu runtime starts.</summary>
public sealed class ViuClientConfigurationOptions
{
    /// <summary>Gets or sets the document URL, resolved against the supplied HTTP client's base address.</summary>
    public string Path { get; set; } = "/_cohesion/client.json";

    /// <summary>Gets or sets the startup request timeout. The default is ten seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
