using System;

namespace Assimalign.Cohesion.Viu.Server;

/// <summary>
/// Identifies the health-probe paths the Viu middleware never answers: the bare <c>/healthz</c>,
/// <c>/readyz</c> and <c>/livez</c> that Cohesion's Web.Health maps by default. They continue to
/// the next middleware, so a probe placed after <c>UseViuApplication()</c> is still reached and the
/// SPA fallback never answers a probe with <c>index.html</c>. Every other path, including
/// application routes that start with <c>/cohesion</c>, belongs to the browser application.
/// </summary>
internal static class HealthProbeRoutes
{
    internal static bool IsReserved(string path)
    {
        return path.Equals("/healthz", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/readyz", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/livez", StringComparison.OrdinalIgnoreCase);
    }
}
