using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.Hosting;

namespace Assimalign.Cohesion.Viu.Hosting.Browser;

internal sealed class ViuClientConfigurationHostService(
    HttpClient httpClient,
    ViuClientConfigurationProvider provider,
    string path,
    TimeSpan timeout,
    Action<string>? warnHandler) : IHostService
{
    public ServiceId Id { get; } = ServiceId.New();

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        provider.Current = ViuClientConfiguration.Empty;
        try
        {
            using CancellationTokenSource request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            request.CancelAfter(timeout);
            using HttpResponseMessage response = await httpClient.GetAsync(path, request.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using Stream body = await response.Content.ReadAsStreamAsync(request.Token).ConfigureAwait(false);
            ViuClientConfiguration? document = await JsonSerializer.DeserializeAsync(
                body, ViuClientConfigurationJsonContext.Default.ViuClientConfiguration, request.Token).ConfigureAwait(false);
            if (document is null || document.Schema != ViuClientConfiguration.SchemaName)
            {
                throw new JsonException("The client configuration document has an unsupported schema.");
            }
            provider.Current = document;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException
            or OperationCanceledException or InvalidOperationException or ArgumentException)
        {
            warnHandler?.Invoke($"Viu client configuration could not be loaded from '{path}': {exception.Message}");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
