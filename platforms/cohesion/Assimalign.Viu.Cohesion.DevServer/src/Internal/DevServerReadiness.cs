using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Cohesion.DevServer;

internal static class DevServerReadiness
{
    public static async Task WriteAsync(TextWriter output, Uri url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(url);

        await output.WriteLineAsync($"Now listening on: {url.AbsoluteUri}").ConfigureAwait(false);
        await output.WriteLineAsync($"App url: {url.AbsoluteUri}").ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
