using System;

using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Viu.Hosting;
using Assimalign.Viu.Components;

namespace Assimalign.Cohesion.Viu.Markdown;

/// <summary>Adds Markdown composition to the optional Cohesion Viu hosting model.</summary>
public static class ViuApplicationBuilderExtensions
{
    /// <summary>Registers catalog, source, renderers, and named and typed components with explicit activation.</summary>
    /// <param name="builder">The mutable application composition.</param>
    /// <param name="configure">Supplies the catalog, source or source factory, and optional rendering settings.</param>
    /// <returns>The same builder after registration; building remains the caller's responsibility.</returns>
    public static ViuApplicationBuilder AddMarkdownContent(this ViuApplicationBuilder builder,
        Action<MarkdownContentOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        MarkdownContentOptions options = new();
        configure(options);
        MarkdownContentCatalog catalog = options.Catalog
            ?? throw new InvalidOperationException("Supply a Markdown content catalog.");
        if ((options.Source is null) == (options.SourceFactory is null))
        {
            throw new InvalidOperationException("Supply exactly one Markdown source instance or source factory.");
        }
        ArgumentNullException.ThrowIfNull(options.RenderOptions);
        ComponentFactory components = options.Components
            ?? builder.ApplicationOptions.Components as ComponentFactory
            ?? throw new InvalidOperationException("Supply the application's concrete ComponentFactory for Markdown registration.");
        MarkdownVirtualNodeRenderer renderer = new(catalog, options.RenderOptions);
        MarkdownHtmlRenderer htmlRenderer = new(catalog, options.RenderOptions);
        builder.ConfigureApplication(application =>
        {
            MarkdownComponents.Register(components);
            application.Components = components;
        });
        builder.Services.Add(ServiceDescriptor.Singleton(catalog));
        builder.Services.Add(ServiceDescriptor.Singleton(renderer));
        builder.Services.Add(ServiceDescriptor.Singleton(htmlRenderer));
        if (options.Source is IMarkdownContentSource source)
        {
            builder.Services.Add(ServiceDescriptor.Singleton(source));
        }
        else
        {
            Func<IServiceProvider, IMarkdownContentSource> factory = options.SourceFactory!;
            builder.Services.AddSingleton<IMarkdownContentSource>(services => factory(services));
        }
        return builder;
    }
}
