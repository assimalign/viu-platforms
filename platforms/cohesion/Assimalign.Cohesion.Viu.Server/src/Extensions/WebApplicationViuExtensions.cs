using System;
using System.Linq;

using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Hosting;
using Assimalign.Cohesion.Http;
using Assimalign.Cohesion.Web;
using Assimalign.Cohesion.Web.Hosting;
using Assimalign.Cohesion.Viu.Hosting;
using Assimalign.Viu.Components;
using Assimalign.Viu.ServerRenderer;

namespace Assimalign.Cohesion.Viu.Server;

/// <summary>
/// Adds Viu hosting features to a Cohesion Web application builder and pipeline.
/// </summary>
public static class WebApplicationViuExtensions
{
    extension(ViuApplicationBuilder builder)
    {
        /// <summary>
        /// Selects the server composition runtime for a Cohesion-hosted Viu application.
        /// </summary>
        /// <returns>The same Viu application builder for continued composition.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the Viu application builder is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// The runtime owns a real single-use Viu application lifetime and its middleware, but it
        /// does not mount or retain a <see cref="ServerRenderApplication"/>. Each HTTP render
        /// creates a fresh server application and <see cref="SsrContext"/> through the pipeline
        /// overloads below.
        /// </remarks>
        public ViuApplicationBuilder ConfigureServerRendering()
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder.ConfigureHost(ServerViuApplicationHostFactory.Instance);
        }
    }

    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// Registers a built Viu server application with the Cohesion Web host lifecycle.
        /// </summary>
        /// <param name="application">The borrowed Viu application to start and stop.</param>
        /// <returns>The same Web application builder for continued composition.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the Web builder or Viu application is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// Call this before <see cref="WebApplicationBuilder.Build"/>. The adapter starts and
        /// stops the Viu application in the Web host's service order but does not dispose it; the
        /// caller retains ownership. Request rendering is installed separately through
        /// <c>UseViuServerRenderer</c>.
        /// </remarks>
        public WebApplicationBuilder AddViuServerApplication(ViuApplication application)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(application);

            builder.Services.AddSingleton<IHostService>(
                new ViuApplicationHostService(application));
            return builder;
        }
    }

    extension(IWebApplicationBuilder builder)
    {
        /// <summary>
        /// Registers a Viu browser application with a Cohesion Web application builder.
        /// </summary>
        /// <param name="configure">
        /// An optional callback that configures content, manifest, fallback, and cross-origin
        /// isolation behavior.
        /// </param>
        /// <returns>The same application builder for continued composition.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the application builder is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when a configured path is invalid.</exception>
        /// <remarks>
        /// Registration captures an immutable configuration feature and the development-refresh
        /// environment at builder time. It performs no service-container registration or
        /// request-time service lookup. Call
        /// <c>UseViuApplication()</c> on the built Cohesion application to install it.
        /// </remarks>
        public IWebApplicationBuilder AddViuApplication(Action<ViuApplicationOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);

            ViuApplicationOptions options = new();
            configure?.Invoke(options);

            ViuApplicationConfiguration configuration = ViuApplicationConfiguration.Create(
                options, (builder as WebApplicationBuilder)?.Environment.Name);
            return builder.AddFeature(new ViuApplicationFeature(configuration));
        }
    }

    extension(IWebApplicationPipelineBuilder builder)
    {
        /// <summary>
        /// Renders the root composed by a Cohesion-hosted Viu application for matching requests.
        /// </summary>
        /// <param name="application">
        /// The built Viu application whose frozen composition and service provider are borrowed.
        /// </param>
        /// <param name="serverRenders">
        /// The explicit generated server-render registry. This call freezes it before publishing
        /// the middleware to concurrent requests.
        /// </param>
        /// <param name="shouldRender">
        /// An optional request predicate. A GET request continues when it returns
        /// <see langword="false"/>; omitted means every GET request is rendered.
        /// </param>
        /// <returns>The same pipeline builder for continued composition.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the builder, application, or server-render registry is
        /// <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// A fresh <see cref="ServerRenderApplication"/> and <see cref="SsrContext"/> are created
        /// for every request. The built service provider and all other frozen composition
        /// references are borrowed and are not disposed per request. No child DI scope is created,
        /// and the Cohesion <see cref="IHttpContext"/> is not injected into application services.
        /// Use the existing adaptor overload when request-specific services or roots are required.
        /// </remarks>
        public IWebApplicationPipelineBuilder UseViuServerRenderer(
            ViuApplication application,
            ServerRenderRegistry serverRenders,
            Func<IHttpContext, bool>? shouldRender = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(application);
            ArgumentNullException.ThrowIfNull(serverRenders);

            ViuApplicationServerRenderRequestScopeFactory requestScopes = new(application);
            ServerRenderAdaptor<IHttpContext> adaptor = new(
                requestScopes,
                serverRenders.Freeze());
            return builder.Use(new ViuServerRendererMiddleware(
                adaptor,
                _ => requestScopes.RootComponent,
                shouldRender ?? (static _ => true)));
        }

        /// <summary>
        /// Renders the root composed by a Cohesion-hosted Viu application inside a request-owned
        /// document shell.
        /// </summary>
        /// <param name="application">
        /// The built Viu application whose frozen composition and service provider are borrowed.
        /// </param>
        /// <param name="serverRenders">
        /// The explicit generated server-render registry. This call freezes it before publishing
        /// the middleware to concurrent requests.
        /// </param>
        /// <param name="documentShellFactory">Creates one document shell per request.</param>
        /// <param name="shouldRender">
        /// An optional request predicate. A GET request continues when it returns
        /// <see langword="false"/>; omitted means every GET request is rendered.
        /// </param>
        /// <returns>The same pipeline builder for continued composition.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the builder, application, registry, or document-shell factory is
        /// <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// Request scope, borrowing, and request-specific-service limits are identical to the
        /// non-document hosting-model overload. Prefix/main/teleport-suffix ordering remains
        /// delegated to Viu's public <see cref="ServerRenderAdaptor{TContext}"/> seam.
        /// </remarks>
        public IWebApplicationPipelineBuilder UseViuServerRenderer(
            ViuApplication application,
            ServerRenderRegistry serverRenders,
            Func<IHttpContext, IServerRenderDocumentShell> documentShellFactory,
            Func<IHttpContext, bool>? shouldRender = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(application);
            ArgumentNullException.ThrowIfNull(serverRenders);
            ArgumentNullException.ThrowIfNull(documentShellFactory);

            ViuApplicationServerRenderRequestScopeFactory requestScopes = new(application);
            ServerRenderAdaptor<IHttpContext> adaptor = new(
                requestScopes,
                serverRenders.Freeze());
            return builder.Use(new ViuServerRendererMiddleware(
                adaptor,
                _ => requestScopes.RootComponent,
                documentShellFactory,
                shouldRender ?? (static _ => true)));
        }

        /// <summary>
        /// Serves a manifest-described Viu browser application, including its SPA fallback and
        /// development browser-refresh HTTP surface.
        /// </summary>
        /// <param name="configure">
        /// An optional callback that supplies content and manifest paths. When omitted on a built
        /// Cohesion application, the registration from <c>AddViuApplication()</c> is used when
        /// present; otherwise the existing default options are preserved.
        /// </param>
        /// <returns>The same pipeline builder for continued composition.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the pipeline builder is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when a configured path is invalid.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a supplied manifest cannot be read.</exception>
        public IWebApplicationPipelineBuilder UseViuApplication(Action<ViuApplicationOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (configure is null && builder is IWebApplication application)
            {
                ViuApplicationFeature? feature = application.Context.Features
                    .OfType<ViuApplicationFeature>()
                    .LastOrDefault();
                if (feature is not null)
                {
                    return builder.Use(new ViuApplicationMiddleware(feature.Configuration));
                }
            }

            ViuApplicationOptions options = new();
            configure?.Invoke(options);

            return builder.Use(new ViuApplicationMiddleware(ViuApplicationConfiguration.Create(
                options, (builder as WebApplication)?.Context.Environment.Name)));
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
