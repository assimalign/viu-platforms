using System;
using Assimalign.Cohesion.Http;
using Assimalign.Cohesion.Web;
using Assimalign.Viu.Cohesion.Web.Internal;
using Assimalign.Viu.Components;
using Assimalign.Viu.ServerRenderer;

namespace Assimalign.Viu.Cohesion.Web;

/// <summary>
/// Adds Viu hosting features to a Cohesion Web application pipeline.
/// </summary>
public static class WebApplicationViuExtensions
{
    extension(IWebApplicationPipelineBuilder builder)
    {
        /// <summary>
        /// Serves a manifest-described Viu browser application, including its SPA fallback and
        /// development browser-refresh HTTP surface.
        /// </summary>
        /// <param name="configure">An optional callback that supplies content and manifest paths.</param>
        /// <returns>The same pipeline builder for continued composition.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the pipeline builder is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when a configured path is invalid.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a supplied manifest cannot be read.</exception>
        public IWebApplicationPipelineBuilder UseViuApplication(Action<ViuApplicationOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);

            ViuApplicationOptions options = new();
            configure?.Invoke(options);

            return builder.Use(new ViuApplicationMiddleware(ViuApplicationConfiguration.Create(options)));
        }

        /// <summary>
        /// Terminates matching GET requests by streaming one Viu server-render operation to the
        /// Cohesion response.
        /// </summary>
        /// <param name="adaptor">
        /// The Viu hosting adaptor configured with the application's request-scope factory and,
        /// when generated render bodies are used, its explicitly registered and frozen
        /// server-render catalog.
        /// </param>
        /// <param name="rootComponentFactory">
        /// Creates the immutable root render description for the current Cohesion request.
        /// </param>
        /// <param name="shouldRender">
        /// An optional request predicate. A GET request continues when the predicate returns
        /// <see langword="false"/>; omitted means every GET request is rendered.
        /// </param>
        /// <returns>The same pipeline builder for continued composition.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the builder, adaptor, or root component factory is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// Non-GET requests continue through the pipeline. Rendering failures are rethrown for the
        /// host's exception boundary; a replaceable buffered response is cleared and changed to 500.
        /// </remarks>
        public IWebApplicationPipelineBuilder UseViuServerRenderer(
            ServerRenderAdaptor<IHttpContext> adaptor,
            Func<IHttpContext, VirtualNode> rootComponentFactory,
            Func<IHttpContext, bool>? shouldRender = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(adaptor);
            ArgumentNullException.ThrowIfNull(rootComponentFactory);

            return builder.Use(new ViuServerRendererMiddleware(
                adaptor,
                rootComponentFactory,
                shouldRender ?? (static _ => true)));
        }

        /// <summary>
        /// Terminates matching GET requests by streaming a host-owned document prefix, one Viu
        /// server-render operation, and a teleport-aware document suffix to the Cohesion response.
        /// </summary>
        /// <param name="adaptor">
        /// The Viu hosting adaptor configured with the application's request-scope factory and,
        /// when generated render bodies are used, its frozen server-render catalog.
        /// </param>
        /// <param name="rootComponentFactory">
        /// Creates the immutable root render description for the current Cohesion request.
        /// </param>
        /// <param name="documentShellFactory">
        /// Creates a fresh document shell for the current request. The shell must place every
        /// teleport destination in its suffix and write each Viu-provided payload verbatim because
        /// the payload already contains its hydration anchors.
        /// </param>
        /// <param name="shouldRender">
        /// An optional request predicate. A GET request continues when the predicate returns
        /// <see langword="false"/>; omitted means every GET request is rendered.
        /// </param>
        /// <returns>The same pipeline builder for continued composition.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the builder, adaptor, root component factory, or document-shell factory is
        /// <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// The shell is borrowed for one request. Its prefix is flushed before component execution;
        /// its suffix runs only after a successful render and receives the completed teleport map.
        /// Non-GET requests continue through the pipeline. Rendering failures are rethrown for the
        /// host's exception boundary; a replaceable buffered response is cleared and changed to 500.
        /// </remarks>
        public IWebApplicationPipelineBuilder UseViuServerRenderer(
            ServerRenderAdaptor<IHttpContext> adaptor,
            Func<IHttpContext, VirtualNode> rootComponentFactory,
            Func<IHttpContext, IServerRenderDocumentShell> documentShellFactory,
            Func<IHttpContext, bool>? shouldRender = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(adaptor);
            ArgumentNullException.ThrowIfNull(rootComponentFactory);
            ArgumentNullException.ThrowIfNull(documentShellFactory);

            return builder.Use(new ViuServerRendererMiddleware(
                adaptor,
                rootComponentFactory,
                documentShellFactory,
                shouldRender ?? (static _ => true)));
        }
    }
}
