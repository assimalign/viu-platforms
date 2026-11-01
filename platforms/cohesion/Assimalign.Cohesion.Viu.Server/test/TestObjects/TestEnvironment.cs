using System;
using System.Collections.Generic;
using System.Threading;

namespace Assimalign.Cohesion.Viu.Server.Tests;

/// <summary>
/// Enters an environment scope the way a gateway that runs a program in its own process does: the
/// runtime-contract variables the scope sets are what the program reads, and every other contract
/// variable reads as unset.
/// </summary>
internal static class TestEnvironment
{
    public static IDisposable Enter(params (string Variable, string Value)[] variables)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string variable, string value) in variables)
        {
            values[variable] = value;
        }

        return AppEnvironment.Enter(AppEnvironmentScope.Create(values, CancellationToken.None));
    }
}
