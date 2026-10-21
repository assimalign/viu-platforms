using System;
using System.Collections.Generic;

using Assimalign.Cohesion.Http;
using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.ServerRenderer;
using Assimalign.Viu.State;

namespace Assimalign.Cohesion.Viu.Server;

/// <summary>Composes server rendering in the Web host's service provider.</summary>
/// <remarks>Options are captured at Web host build. Unset catalogs use compile-time aggregation.</remarks>
public sealed class ViuServerRenderingOptions
{
    /// <summary>Gets or sets the required immutable root render description.</summary>
    public VirtualNode? RootComponent { get; set; }

    /// <summary>Gets or sets an explicit component resolver, replacing the generated default.</summary>
    public IComponentFactory? Components { get; set; }

    /// <summary>Gets or sets an explicit render catalog, replacing the generated default; frozen at build.</summary>
    public ServerRenderRegistry? ServerRenders { get; set; }

    /// <summary>Gets or sets the optional document-shell factory invoked once per rendered request.</summary>
    public Func<IHttpContext, IServerRenderDocumentShell>? DocumentShell { get; set; }

    /// <summary>Gets or sets shared state; when unset, state resolves from the render's DI scope.</summary>
    public IStateStoreRegistry? State { get; set; }

    /// <summary>Gets or sets shared directives; when unset, directives resolve from the render's DI scope.</summary>
    public IDirectiveResolver? Directives { get; set; }

    /// <summary>Gets or sets the warning handler; unset warnings write to standard error.</summary>
    public Action<string>? WarnHandler { get; set; }

    /// <summary>Gets or sets the error handler; unset errors write to standard error.</summary>
    public Action<Exception, ComponentContext?, string>? ErrorHandler { get; set; }

    /// <summary>Gets or sets the component event observer.</summary>
    public Action<ComponentContext, string, IReadOnlyList<object?>>? EventObserver { get; set; }

    /// <summary>Selects a generated component definition as the server root without activating it.</summary>
    /// <typeparam name="TComponent">The generated component definition.</typeparam>
    /// <returns>These options.</returns>
    public ViuServerRenderingOptions UseRootComponent<TComponent>()
        where TComponent : IComponentDefinition
    {
        RootComponent = ComponentNode.For<TComponent>();
        return this;
    }
}
