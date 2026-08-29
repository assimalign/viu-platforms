using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.ServerRenderer;

namespace Assimalign.Cohesion.Viu.Server;

internal sealed class CohesionServerRenderOutput : IServerRenderOutput, IAsyncDisposable
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly Stream _responseBody;
    private readonly CancellationToken _requestAborted;
    private readonly Encoder _encoder = Utf8.GetEncoder();
    private bool _disposed;

    public CohesionServerRenderOutput(
        Stream responseBody,
        CancellationToken requestAborted)
    {
        ArgumentNullException.ThrowIfNull(responseBody);
        _responseBody = responseBody;
        _requestAborted = requestAborted;
    }

    // The current Cohesion response body is a replaceable memory buffer. A future streaming
    // implementation must instead expose its monotonic, transport-confirmed HasStarted state.
    public bool ResponseCommitted => false;

    public async ValueTask WriteAsync(
        ReadOnlyMemory<char> content,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        byte[] bytes = ArrayPool<byte>.Shared.Rent(Utf8.GetMaxByteCount(content.Length));
        try
        {
            _encoder.Convert(
                content.Span,
                bytes,
                flush: false,
                out int charactersUsed,
                out int bytesUsed,
                out bool completed);
            if (!completed || charactersUsed != content.Length)
            {
                throw new InvalidOperationException("The UTF-8 encoder buffer was unexpectedly too small.");
            }

            await _responseBody
                .WriteAsync(bytes.AsMemory(0, bytesUsed), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken = default) =>
        new(_responseBody.FlushAsync(cancellationToken));

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        byte[] bytes = ArrayPool<byte>.Shared.Rent(Utf8.GetMaxByteCount(1));
        try
        {
            _encoder.Convert(
                ReadOnlySpan<char>.Empty,
                bytes,
                flush: true,
                out _,
                out int bytesUsed,
                out _);
            if (bytesUsed > 0)
            {
                await _responseBody
                    .WriteAsync(bytes.AsMemory(0, bytesUsed), _requestAborted)
                    .ConfigureAwait(false);
            }
            await _responseBody.FlushAsync(_requestAborted).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }
}
