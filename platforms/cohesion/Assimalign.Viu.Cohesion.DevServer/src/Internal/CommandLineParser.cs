using System;
using System.Collections.Generic;
using System.IO;

namespace Assimalign.Viu.Cohesion.DevServer;

internal static class CommandLineParser
{
    public const string Usage = "Usage: Assimalign.Viu.Cohesion.DevServer --content-root <path> --endpoints-manifest <path> --runtime-manifest <path> [--apply-cop-headers] [--urls <semicolon list>]";

    public static DevServerOptions Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        string? contentRootPath = null;
        string? endpointsManifestPath = null;
        string? runtimeManifestPath = null;
        string? urls = null;
        bool applyCrossOriginIsolationHeaders = false;

        for (int index = 0; index < arguments.Count; index++)
        {
            string argument = arguments[index];
            switch (argument)
            {
                case "--content-root":
                    contentRootPath = ReadValue(arguments, ref index, argument, contentRootPath);
                    break;
                case "--endpoints-manifest":
                    endpointsManifestPath = ReadValue(arguments, ref index, argument, endpointsManifestPath);
                    break;
                case "--runtime-manifest":
                    runtimeManifestPath = ReadValue(arguments, ref index, argument, runtimeManifestPath);
                    break;
                case "--urls":
                    urls = ReadValue(arguments, ref index, argument, urls);
                    break;
                case "--apply-cop-headers":
                    if (applyCrossOriginIsolationHeaders)
                    {
                        throw new CommandLineException("Option '--apply-cop-headers' was specified more than once.");
                    }

                    applyCrossOriginIsolationHeaders = true;
                    break;
                case "--help" or "-h":
                    throw new CommandLineException(Usage);
                default:
                    throw new CommandLineException($"Unknown option '{argument}'.");
            }
        }

        contentRootPath = RequireValue(contentRootPath, "--content-root");
        endpointsManifestPath = RequireValue(endpointsManifestPath, "--endpoints-manifest");
        runtimeManifestPath = RequireValue(runtimeManifestPath, "--runtime-manifest");

        return new DevServerOptions(
            Path.GetFullPath(contentRootPath),
            Path.GetFullPath(endpointsManifestPath),
            Path.GetFullPath(runtimeManifestPath),
            applyCrossOriginIsolationHeaders,
            urls);
    }

    private static string ReadValue(
        IReadOnlyList<string> arguments,
        ref int index,
        string option,
        string? existingValue)
    {
        if (existingValue is not null)
        {
            throw new CommandLineException($"Option '{option}' was specified more than once.");
        }

        if (++index >= arguments.Count || arguments[index].StartsWith("--", StringComparison.Ordinal))
        {
            throw new CommandLineException($"Option '{option}' requires a value.");
        }

        string value = arguments[index];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CommandLineException($"Option '{option}' requires a non-empty value.");
        }

        return value;
    }

    private static string RequireValue(string? value, string option)
    {
        if (value is null)
        {
            throw new CommandLineException($"Required option '{option}' was not specified.");
        }

        return value;
    }
}
