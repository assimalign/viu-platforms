using System;

using Assimalign.Viu.Components;

namespace Assimalign.Cohesion.Viu.Hosting;

/// <summary>Writes Viu diagnostics to standard error when the application supplies no handler.</summary>
internal static class ViuApplicationDiagnostics
{
    public static void WriteWarning(string message) =>
        Console.Error.WriteLine($"Viu warning: {message}");

    public static void WriteError(Exception exception, ComponentContext? context, string source) =>
        Console.Error.WriteLine($"Viu error ({source}): {exception}");
}
