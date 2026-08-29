using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

using Assimalign.Cohesion.Http;
using Assimalign.Cohesion.Web;
using Assimalign.Viu.Components;
using Assimalign.Viu.ServerRenderer;

namespace Assimalign.Cohesion.Viu.Server;

internal sealed class ViuServerRendererMiddleware : IWebApplicationMiddleware
{
    private static readonly HttpHeaderKey CacheControlHeader = new("Cache-Control");
    private static readonly HttpHeaderKey ContentTypeHeader = new("Content-Type");

    private readonly ServerRenderAdaptor<IHttpContext> _adaptor;
    private readonly Func<IHttpContext, VirtualNode> _rootComponentFactory;
    private readonly Func<IHttpContext, IServerRenderDocumentShell>? _documentShellFactory;
    private readonly Func<IHttpContext, bool> _shouldRender;

    public ViuServerRendererMiddleware(
        ServerRenderAdaptor<IHttpContext> adaptor,
        Func<IHttpContext, VirtualNode> rootComponentFactory,
        Func<IHttpContext, bool> shouldRender)
    {
        ArgumentNullException.ThrowIfNull(adaptor);
        ArgumentNullException.ThrowIfNull(rootComponentFactory);
        ArgumentNullException.ThrowIfNull(shouldRender);
        _adaptor = adaptor;
        _rootComponentFactory = rootComponentFactory;
        _shouldRender = shouldRender;
    }

    public ViuServerRendererMiddleware(
        ServerRenderAdaptor<IHttpContext> adaptor,
        Func<IHttpContext, VirtualNode> rootComponentFactory,
        Func<IHttpContext, IServerRenderDocumentShell> documentShellFactory,
        Func<IHttpContext, bool> shouldRender)
    {
        ArgumentNullException.ThrowIfNull(adaptor);
        ArgumentNullException.ThrowIfNull(rootComponentFactory);
        ArgumentNullException.ThrowIfNull(documentShellFactory);
        ArgumentNullException.ThrowIfNull(shouldRender);
        _adaptor = adaptor;
        _rootComponentFactory = rootComponentFactory;
        _documentShellFactory = documentShellFactory;
        _shouldRender = shouldRender;
    }

    public async Task InvokeAsync(IHttpContext context, WebApplicationMiddleware next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (context.Request.Method != HttpMethod.Get)
        {
            await next.Invoke(context).ConfigureAwait(false);
            return;
        }

        bool shouldRender;
        try
        {
            shouldRender = _shouldRender(context);
        }
        catch
        {
            SetFailureResponse(context);
            throw;
        }

        if (!shouldRender)
        {
            await next.Invoke(context).ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = HttpStatusCode.Ok;
        context.Response.Headers[ContentTypeHeader] = "text/html; charset=utf-8";
        context.Response.Headers[CacheControlHeader] = "no-store";

        VirtualNode rootComponent;
        IServerRenderDocumentShell? documentShell;
        try
        {
            rootComponent = _rootComponentFactory(context)
                ?? throw new InvalidOperationException("The Viu server-render root factory returned null.");
            documentShell = null;
            if (_documentShellFactory is not null)
            {
                documentShell = _documentShellFactory(context)
                    ?? throw new InvalidOperationException(
                        "The Viu server-render document-shell factory returned null.");
            }
        }
        catch
        {
            SetFailureResponse(context);
            throw;
        }

        ServerRenderResult result;
        await using (CohesionServerRenderOutput output = new(
            context.Response.Body,
            context.RequestCancelled))
        {
            ServerRenderRequest<IHttpContext> request = new(rootComponent, context);
            result = documentShell is null
                ? await _adaptor.RenderAsync(
                    request,
                    output,
                    context.RequestCancelled).ConfigureAwait(false)
                : await _adaptor.RenderDocumentAsync(
                    request,
                    output,
                    documentShell,
                    context.RequestCancelled).ConfigureAwait(false);
        }

        if (result.Succeeded)
        {
            return;
        }

        if (!result.ResponseCommitted)
        {
            SetFailureResponse(context);
        }

        ExceptionDispatchInfo.Capture(result.Failure!).Throw();
    }

    private static void SetFailureResponse(IHttpContext context)
    {
        context.Response.StatusCode = HttpStatusCode.InternalServerError;
        ClearResponseBody(context.Response.Body);
    }

    private static void ClearResponseBody(Stream responseBody)
    {
        if (!responseBody.CanSeek || !responseBody.CanWrite)
        {
            return;
        }

        responseBody.Position = 0;
        responseBody.SetLength(0);
    }
}
