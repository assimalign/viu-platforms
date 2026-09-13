using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Cohesion.Viu.Markdown;

/// <summary>Reads Markdown assets without tying consumers to a browser, file system, or hosting model.</summary>
public interface IMarkdownContentSource
{
    /// <summary>Reads one asset's complete Markdown text and honors cancellation.</summary>
    Task<string> ReadAsync(string assetPath, CancellationToken cancellationToken = default);

    /// <summary>Lists available asset paths for runtime catalog construction; sources may reject unsupported listing.</summary>
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromException<IReadOnlyList<string>>(new NotSupportedException("This content source does not provide an asset listing."));
}
