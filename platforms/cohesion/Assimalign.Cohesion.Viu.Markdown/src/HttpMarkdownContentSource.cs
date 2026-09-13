using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Cohesion.Viu.Markdown;

/// <summary>Fetches Markdown using a borrowed HttpClient and its configured base address.</summary>
public sealed class HttpMarkdownContentSource : IMarkdownContentSource
{
    private readonly HttpClient _httpClient;
    private readonly IReadOnlyList<string>? _assetPaths;

    /// <summary>Borrows a client without disposing it and optionally snapshots a known asset listing.</summary>
    public HttpMarkdownContentSource(HttpClient httpClient, IEnumerable<string>? assetPaths = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _assetPaths = assetPaths is null ? null : Array.AsReadOnly(assetPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray());
    }

    /// <summary>Fetches the asset relative to the client's base address and propagates HTTP failures and cancellation.</summary>
    public Task<string> ReadAsync(string assetPath, CancellationToken cancellationToken = default)
        => _httpClient.GetStringAsync(assetPath, cancellationToken);

    /// <summary>Returns the configured immutable listing, or rejects listing when none was supplied.</summary>
    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _assetPaths is null
            ? Task.FromException<IReadOnlyList<string>>(new NotSupportedException("Configure asset paths to list HTTP Markdown content."))
            : Task.FromResult(_assetPaths);
    }
}
