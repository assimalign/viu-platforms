using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Assimalign.Cohesion.Viu.Hosting;

/// <summary>Immutable, public resource identity and endpoint configuration for a browser application.</summary>
public sealed class ViuClientConfiguration
{
    /// <summary>The supported client document schema.</summary>
    public const string SchemaName = "cohesion/viu-client/v1";

    /// <summary>Gets the configuration used when no document is available.</summary>
    public static ViuClientConfiguration Empty { get; } = new();

    /// <summary>Creates a document, taking a defensive copy of every reference map.</summary>
    /// <param name="application">The containing application identity.</param>
    /// <param name="resource">The Web resource identity.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="gateway">The gateway identity.</param>
    /// <param name="references">Endpoint URLs grouped by resource and endpoint names.</param>
    /// <param name="schema">The document schema identifier.</param>
    [JsonConstructor]
    public ViuClientConfiguration(
        string? application = null,
        string? resource = null,
        string? environment = null,
        string? gateway = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? references = null,
        string schema = SchemaName)
    {
        Application = application;
        Resource = resource;
        Environment = environment;
        Gateway = gateway;
        Schema = schema;
        Dictionary<string, IReadOnlyDictionary<string, string>> snapshot = new(StringComparer.Ordinal);
        if (references is not null)
        {
            foreach ((string name, IReadOnlyDictionary<string, string> endpoints) in references)
            {
                ArgumentNullException.ThrowIfNull(endpoints);
                snapshot.Add(name, new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(endpoints, StringComparer.Ordinal)));
            }
        }
        References = new ReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>(snapshot);
    }

    /// <summary>Gets the document schema identifier.</summary>
    public string Schema { get; }

    /// <summary>Gets the containing application identity, when orchestrated.</summary>
    public string? Application { get; }

    /// <summary>Gets the Web resource identity, when orchestrated.</summary>
    public string? Resource { get; }

    /// <summary>Gets the host environment.</summary>
    public string? Environment { get; }

    /// <summary>Gets the gateway identity, when orchestrated.</summary>
    public string? Gateway { get; }

    /// <summary>Gets immutable endpoint URLs grouped by resource and endpoint names.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> References { get; }
}
