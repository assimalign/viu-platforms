using System;

namespace Assimalign.Cohesion.Viu.Server;

/// <summary>
/// Identifies the request paths a Cohesion Web resource maps ahead of every user middleware: the
/// bare health probes and the resource control plane. Only those paths are passed through by the
/// Viu middleware; every other path, including application routes that merely start with
/// <c>/cohesion</c>, belongs to the browser application.
/// </summary>
internal static class ResourceRoutes
{
    /// <summary>The control-plane path <c>Assimalign.Cohesion.Sdk.Web</c> declares by default.</summary>
    internal const string DefaultControlPlanePath = "/cohesion/v1";

    internal static bool IsReserved(string path) => IsReserved(path, DefaultControlPlanePath);

    internal static bool IsReserved(string path, string controlPlanePath)
    {
        if (path.Equals("/healthz", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/readyz", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/livez", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string controlPlane = NormalizeControlPlanePath(controlPlanePath);
        return path.Equals(controlPlane, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(controlPlane + "/", StringComparison.OrdinalIgnoreCase);
    }

    internal static string NormalizeControlPlanePath(string controlPlanePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(controlPlanePath);
        if (!controlPlanePath.StartsWith('/') || controlPlanePath.StartsWith("//", StringComparison.Ordinal)
            || controlPlanePath.Contains('?') || controlPlanePath.Contains('#'))
        {
            throw new ArgumentException("ControlPlanePath must be a local absolute route such as /cohesion/v1.", nameof(controlPlanePath));
        }

        string trimmed = controlPlanePath.TrimEnd('/');
        return trimmed.Length == 0 ? "/" : trimmed;
    }
}
