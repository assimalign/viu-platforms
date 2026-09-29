using System;

namespace Assimalign.Cohesion.Viu.Markdown;

/// <summary>Collects explicit Markdown dependencies before hosting registers instances or factories.</summary>
/// <remarks>Not thread-safe; configure before building the application.</remarks>
public sealed class MarkdownContentOptions
{
    /// <summary>Creates options requiring an explicit catalog and content source.</summary>
    public MarkdownContentOptions()
    {
    }

    /// <summary>Gets or sets the generated or runtime-built immutable catalog.</summary>
    public MarkdownContentCatalog? Catalog { get; set; }

    /// <summary>Gets or sets a caller-owned source instance; use either this or SourceFactory.</summary>
    public IMarkdownContentSource? Source { get; set; }

    /// <summary>Gets or sets an explicit source factory resolved by the host's provider without type activation.</summary>
    public Func<IServiceProvider, IMarkdownContentSource>? SourceFactory { get; set; }

    /// <summary>Gets or sets rendering policies copied when the renderer is constructed.</summary>
    public MarkdownRenderOptions RenderOptions { get; set; } = new();
}
