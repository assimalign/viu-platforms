using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Cohesion.Viu.Server;

internal sealed class BrowserRefreshDeltaCache
{
    private readonly Lock _lock = new();
    private readonly List<byte[]> _updates = [];
    private int _lastUpdateIdentifier = -1;

    public async Task<bool> AddAsync(Stream source, CancellationToken cancellationToken)
    {
        using MemoryStream buffer = new();
        await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        byte[] payload = buffer.ToArray();
        using JsonDocument document = JsonDocument.Parse(payload);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("id", out JsonElement identifierElement)
            || !identifierElement.TryGetInt32(out int identifier)
            || !document.RootElement.TryGetProperty("deltas", out JsonElement deltasElement)
            || deltasElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("A browser hot-reload update must contain an integer id and a deltas array.");
        }

        lock (_lock)
        {
            if (_updates.Count > 0 && identifier <= _lastUpdateIdentifier)
            {
                return false;
            }

            _updates.Add(payload);
            _lastUpdateIdentifier = identifier;
            return true;
        }
    }

    public byte[]? Snapshot()
    {
        lock (_lock)
        {
            if (_updates.Count == 0)
            {
                return null;
            }

            using MemoryStream output = new();
            output.WriteByte((byte)'[');
            for (int index = 0; index < _updates.Count; index++)
            {
                if (index > 0)
                {
                    output.WriteByte((byte)',');
                }
                output.Write(_updates[index]);
            }
            output.WriteByte((byte)']');
            return output.ToArray();
        }
    }
}
