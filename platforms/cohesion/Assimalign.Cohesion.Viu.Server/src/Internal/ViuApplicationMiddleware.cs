using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Assimalign.Cohesion.Http;
using Assimalign.Cohesion.Web;
using Assimalign.Cohesion.Web.Routing;
using Assimalign.Cohesion.Viu.Hosting;

using WebUtility = System.Net.WebUtility;

namespace Assimalign.Cohesion.Viu.Server;

internal sealed class ViuApplicationMiddleware : IWebApplicationMiddleware
{
    private const string BrowserRefreshPath = "/_framework/aspnetcore-browser-refresh.js";
    private const string BlazorHotReloadPath = "/_framework/blazor-hotreload";
    private const string ClearBrowserCachePath = "/_framework/clear-browser-cache";

    private static readonly HttpHeaderKey AcceptEncodingHeader = new("Accept-Encoding");
    private static readonly HttpHeaderKey CacheControlHeader = new("Cache-Control");
    private static readonly HttpHeaderKey ClearSiteDataHeader = new("Clear-Site-Data");
    private static readonly HttpHeaderKey ContentEncodingHeader = new("Content-Encoding");
    private static readonly HttpHeaderKey ContentLengthHeader = new("Content-Length");
    private static readonly HttpHeaderKey ContentTypeHeader = new("Content-Type");
    private static readonly HttpHeaderKey ETagHeader = new("ETag");
    private static readonly HttpHeaderKey LastModifiedHeader = new("Last-Modified");
    private static readonly HttpHeaderKey CrossOriginEmbedderPolicyHeader = new("Cross-Origin-Embedder-Policy");
    private static readonly HttpHeaderKey CrossOriginOpenerPolicyHeader = new("Cross-Origin-Opener-Policy");

    private readonly ViuApplicationConfiguration _configuration;
    private readonly StaticWebAssetCatalog _catalog;
    private readonly BrowserRefreshDeltaCache _deltaCache = new();

    public ViuApplicationMiddleware(ViuApplicationConfiguration configuration)
    {
        _configuration = configuration;
        _catalog = StaticWebAssetCatalog.Load(configuration);
    }

    public async Task InvokeAsync(IHttpContext context, WebApplicationMiddleware next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        // Cohesion dispatches selected endpoints at the pipeline terminal. Keep their handlers
        // and routing-generated responses ahead of Viu content and the SPA fallback.
        if (context.Features.Get<IRouterEndpointFeature>() is not null)
        {
            await next.Invoke(context).ConfigureAwait(false);
            return;
        }

        string requestPath = context.Request.Path.Value;
        if (ResourceRoutes.IsReserved(requestPath, _configuration.ControlPlanePath))
        {
            await next.Invoke(context).ConfigureAwait(false);
            return;
        }

        ApplyCrossOriginIsolationHeaders(context.Response.Headers);
        if (_configuration.ClientConfigurationPath is string configurationPath
            && requestPath.Equals(configurationPath, StringComparison.Ordinal))
        {
            if (context.Request.Method != HttpMethod.Get && context.Request.Method != HttpMethod.Head)
            {
                context.Response.StatusCode = HttpStatusCode.MethodNotAllowed;
                return;
            }
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
                _configuration.ClientConfiguration ?? ViuClientConfiguration.Empty,
                ViuClientConfigurationJsonContext.Default.ViuClientConfiguration);
            await WriteBytesAsync(context, payload, "application/json; charset=utf-8", "no-store").ConfigureAwait(false);
            return;
        }

        bool isReservedRefreshPath = IsReservedRefreshPath(requestPath);
        if (!_configuration.BrowserRefresh.IsEnabled && isReservedRefreshPath)
        {
            await next.Invoke(context).ConfigureAwait(false);
            return;
        }

        if (_configuration.BrowserRefresh.IsEnabled)
        {
            if (requestPath.Equals(
                _configuration.BrowserRefresh.GetApplicationPath(BrowserRefreshPath),
                StringComparison.Ordinal))
            {
                if (context.Request.Method == HttpMethod.Get)
                {
                    await WriteBrowserRefreshScriptAsync(context).ConfigureAwait(false);
                }
                else
                {
                    context.Response.StatusCode = HttpStatusCode.MethodNotAllowed;
                }
                return;
            }

            if (requestPath.Equals(
                _configuration.BrowserRefresh.GetApplicationPath(BlazorHotReloadPath),
                StringComparison.Ordinal))
            {
                await HandleHotReloadAsync(context).ConfigureAwait(false);
                return;
            }

            if (requestPath.Equals(
                _configuration.BrowserRefresh.GetApplicationPath(ClearBrowserCachePath),
                StringComparison.Ordinal))
            {
                HandleClearBrowserCache(context);
                return;
            }
        }

        if (context.Request.Method != HttpMethod.Get && context.Request.Method != HttpMethod.Head)
        {
            await next.Invoke(context).ConfigureAwait(false);
            return;
        }

        if (!StaticWebAssetCatalog.IsSafeRequestPath(requestPath))
        {
            context.Response.StatusCode = HttpStatusCode.NotFound;
            return;
        }

        string? acceptEncoding = context.Request.Headers.TryGetValue(AcceptEncodingHeader, out HttpHeaderValue value)
            ? (string?)value
            : null;
        bool isFallback = requestPath is "/" or "";
        string assetPath = isFallback ? "index.html" : requestPath;
        StaticWebAssetDescriptor? asset = _catalog.Resolve(assetPath, acceptEncoding);
        if (asset is null && ShouldUseSpaFallback(requestPath))
        {
            asset = _catalog.Resolve("index.html", acceptEncoding, requireIdentity: _configuration.BrowserRefresh.IsEnabled);
            isFallback = asset is not null;
        }

        if (asset is null)
        {
            await next.Invoke(context).ConfigureAwait(false);
            return;
        }

        if (_configuration.BrowserRefresh.IsEnabled
            && ViuContentTypes.IsHtml(asset.LogicalPath)
            && asset.ContentEncoding is not null)
        {
            asset = _catalog.Resolve(assetPath, acceptEncoding, requireIdentity: true)
                ?? (isFallback ? _catalog.Resolve("index.html", acceptEncoding, requireIdentity: true) : null);
        }

        if (asset is null)
        {
            await next.Invoke(context).ConfigureAwait(false);
            return;
        }

        await WriteAssetAsync(context, asset, isFallback).ConfigureAwait(false);
    }

    private bool ShouldUseSpaFallback(string requestPath)
    {
        if (!_configuration.EnableSpaFallback || IsFrameworkPath(requestPath))
        {
            return false;
        }

        int lastSeparator = requestPath.LastIndexOf('/');
        ReadOnlySpan<char> finalSegment = requestPath.AsSpan(lastSeparator + 1);
        return !finalSegment.Contains('.');
    }

    private bool IsReservedRefreshPath(string requestPath) =>
        requestPath.Equals(
            _configuration.BrowserRefresh.GetApplicationPath(BrowserRefreshPath),
            StringComparison.Ordinal)
        || requestPath.Equals(
            _configuration.BrowserRefresh.GetApplicationPath(BlazorHotReloadPath),
            StringComparison.Ordinal)
        || requestPath.Equals(
            _configuration.BrowserRefresh.GetApplicationPath(ClearBrowserCachePath),
            StringComparison.Ordinal);

    private bool IsFrameworkPath(string requestPath)
    {
        if (HasPathPrefix(requestPath, "/_framework"))
        {
            return true;
        }

        return HasPathPrefix(
            requestPath,
            _configuration.BrowserRefresh.GetApplicationPath("/_framework"));
    }

    private static bool HasPathPrefix(string requestPath, string prefix) =>
        requestPath.Equals(prefix, StringComparison.Ordinal)
        || requestPath.StartsWith(prefix + "/", StringComparison.Ordinal);

    private void ApplyCrossOriginIsolationHeaders(IHttpHeaderCollection headers)
    {
        if (!_configuration.ApplyCrossOriginIsolationHeaders)
        {
            return;
        }

        headers[CrossOriginEmbedderPolicyHeader] = "require-corp";
        headers[CrossOriginOpenerPolicyHeader] = "same-origin";
    }

    private async Task HandleHotReloadAsync(IHttpContext context)
    {
        if (context.Request.Method == HttpMethod.Get)
        {
            byte[]? payload = _deltaCache.Snapshot();
            if (payload is null)
            {
                context.Response.StatusCode = HttpStatusCode.NoContent;
                return;
            }

            await WriteBytesAsync(context, payload, "application/json", cacheControl: "no-store").ConfigureAwait(false);
            return;
        }

        if (context.Request.Method == HttpMethod.Post)
        {
            try
            {
                _ = await _deltaCache.AddAsync(context.Request.Body, context.RequestCancelled).ConfigureAwait(false);
                context.Response.StatusCode = HttpStatusCode.Ok;
            }
            catch (JsonException)
            {
                context.Response.StatusCode = HttpStatusCode.BadRequest;
            }
            return;
        }

        context.Response.StatusCode = HttpStatusCode.MethodNotAllowed;
    }

    private static void HandleClearBrowserCache(IHttpContext context)
    {
        if (context.Request.Method != HttpMethod.Get)
        {
            context.Response.StatusCode = HttpStatusCode.MethodNotAllowed;
            return;
        }

        context.Response.StatusCode = HttpStatusCode.NoContent;
        context.Response.Headers[ClearSiteDataHeader] = "\"cache\"";
        context.Response.Headers[CacheControlHeader] = "no-store";
    }

    private async Task WriteBrowserRefreshScriptAsync(IHttpContext context)
    {
        string script = BrowserRefreshClientScript.Create(_configuration.BrowserRefresh);
        byte[] payload = Encoding.UTF8.GetBytes(script);
        await WriteBytesAsync(context, payload, "application/javascript; charset=utf-8", cacheControl: "no-store").ConfigureAwait(false);
    }

    private async Task WriteAssetAsync(IHttpContext context, StaticWebAssetDescriptor asset, bool isFallback)
    {
        IHttpHeaderCollection headers = context.Response.Headers;
        foreach (KeyValuePair<string, string> header in asset.ResponseHeaders)
        {
            headers[new HttpHeaderKey(header.Key)] = header.Value;
        }

        if (!headers.ContainsKey(ContentTypeHeader))
        {
            headers[ContentTypeHeader] = ViuContentTypes.Get(asset.LogicalPath);
        }

        bool injectRefresh = _configuration.BrowserRefresh.IsEnabled
            && ViuContentTypes.IsHtml(asset.LogicalPath)
            && asset.ContentEncoding is null;

        context.Response.StatusCode = HttpStatusCode.Ok;
        if (isFallback)
        {
            headers[CacheControlHeader] = "no-store";
        }

        if (injectRefresh)
        {
            string html = await File.ReadAllTextAsync(asset.PhysicalPath, context.RequestCancelled).ConfigureAwait(false);
            byte[] payload = Encoding.UTF8.GetBytes(InjectBrowserRefresh(html));
            headers.Remove(ContentEncodingHeader);
            headers.Remove(ETagHeader);
            headers.Remove(LastModifiedHeader);
            headers[CacheControlHeader] = "no-store";
            headers[ContentLengthHeader] = payload.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (context.Request.Method == HttpMethod.Get)
            {
                await context.Response.Body.WriteAsync(payload, context.RequestCancelled).ConfigureAwait(false);
            }
            return;
        }

        FileInfo file = new(asset.PhysicalPath);
        headers[ContentLengthHeader] = file.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (context.Request.Method == HttpMethod.Head)
        {
            return;
        }

        await using FileStream source = File.OpenRead(asset.PhysicalPath);
        await source.CopyToAsync(context.Response.Body, context.RequestCancelled).ConfigureAwait(false);
    }

    private static async Task WriteBytesAsync(
        IHttpContext context,
        byte[] payload,
        string contentType,
        string cacheControl)
    {
        context.Response.StatusCode = HttpStatusCode.Ok;
        context.Response.Headers[ContentTypeHeader] = contentType;
        context.Response.Headers[CacheControlHeader] = cacheControl;
        context.Response.Headers[ContentLengthHeader] = payload.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (context.Request.Method != HttpMethod.Head)
        {
            await context.Response.Body.WriteAsync(payload, context.RequestCancelled).ConfigureAwait(false);
        }
    }

    private string InjectBrowserRefresh(string html)
    {
        string scriptPath = WebUtility.HtmlEncode(
            _configuration.BrowserRefresh.GetApplicationPath(BrowserRefreshPath));
        string browserRefreshElement = $"<script src=\"{scriptPath}\"></script>";
        int bodyEnd = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return bodyEnd < 0
            ? html + browserRefreshElement
            : html.Insert(bodyEnd, browserRefreshElement);
    }
}
