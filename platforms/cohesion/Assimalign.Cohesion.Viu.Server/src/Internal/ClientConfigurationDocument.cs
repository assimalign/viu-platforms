using System.Collections.Generic;

using Assimalign.Cohesion.Viu.Hosting;

namespace Assimalign.Cohesion.Viu.Server;

internal static class ClientConfigurationDocument
{
    /// <summary>
    /// Creates the public client document. The identity comes from the runtime contract the
    /// instance's platform sets (<see cref="AppEnvironment.Variables.Application"/>,
    /// <see cref="AppEnvironment.Variables.Resource"/> and <see cref="AppEnvironment.Variables.Gateway"/>),
    /// read through <see cref="AppEnvironment"/>, so a program a gateway runs in its own process
    /// reads its own environment scope. Only those names and the references the application added
    /// are projected; no other variable or setting reaches the document.
    /// </summary>
    internal static ViuClientConfiguration Create(
        string environment,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? references)
    {
        string? resource = Normalize(AppEnvironment.GetValue(AppEnvironment.Variables.Resource));
        if (resource is null)
        {
            return new ViuClientConfiguration(environment: environment, references: references);
        }

        return new ViuClientConfiguration(
            Normalize(AppEnvironment.GetValue(AppEnvironment.Variables.Application)),
            resource,
            environment,
            Normalize(AppEnvironment.GetValue(AppEnvironment.Variables.Gateway)),
            references);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
