using System;
using System.Collections.Generic;

using Assimalign.Cohesion.Hosting.Resources;
using Assimalign.Cohesion.Viu.Hosting;

namespace Assimalign.Cohesion.Viu.Server;

internal static class ClientConfigurationDocument
{
    internal static ViuClientConfiguration Create(string environment)
    {
        ResourceContext resource = ResourceRuntime.Current;
        if (string.IsNullOrEmpty(resource.ResourceName))
        {
            return new ViuClientConfiguration(environment: environment);
        }

        Dictionary<string, Dictionary<string, string>> grouped = new(StringComparer.Ordinal);
        foreach ((string key, Uri url) in resource.References)
        {
            int separator = key.IndexOf(':');
            if (separator <= 0 || separator == key.Length - 1)
            {
                continue;
            }
            string name = key[..separator];
            if (!grouped.TryGetValue(name, out Dictionary<string, string>? endpoints))
            {
                endpoints = new(StringComparer.Ordinal);
                grouped.Add(name, endpoints);
            }
            endpoints.Add(key[(separator + 1)..], url.ToEndpointString());
        }
        Dictionary<string, IReadOnlyDictionary<string, string>> references = new(StringComparer.Ordinal);
        foreach ((string name, Dictionary<string, string> endpoints) in grouped)
        {
            references.Add(name, endpoints);
        }
        return new ViuClientConfiguration(resource.ApplicationName, resource.ResourceName,
            resource.EnvironmentName, resource.GatewayName, references);
    }
}
