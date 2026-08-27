using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.Http;

using CohesionHttpMethod = Assimalign.Cohesion.Http.HttpMethod;

namespace Assimalign.Viu.Cohesion.Web.Tests;

internal sealed class TestHttpContext : IHttpContext
{
    private readonly CancellationToken _requestAborted;

    public TestHttpContext(
        string path,
        CohesionHttpMethod method,
        string? body = null,
        CancellationToken requestAborted = default)
    {
        Request = new TestHttpRequest(this, path, method, body);
        Response = new TestHttpResponse(this);
        _requestAborted = requestAborted;
    }

    public HttpVersion Version => HttpVersion.Http11;

    public IHttpRequest Request { get; }

    public IHttpResponse Response { get; }

    public IHttpConnectionInfo ConnectionInfo => HttpConnectionInfo.Empty;

    public IHttpFeatureCollection Features { get; } = new HttpFeatureCollection();

    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    public CancellationToken RequestCancelled => _requestAborted;

    public void Cancel()
    {
    }

    public Task CancelAsync() => Task.CompletedTask;

    public string ReadResponseBody()
    {
        return Encoding.UTF8.GetString(((MemoryStream)Response.Body).ToArray());
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class TestHttpRequest : IHttpRequest
{
    public TestHttpRequest(IHttpContext context, string path, CohesionHttpMethod method, string? body)
    {
        HttpContext = context;
        Path = new HttpPath(path);
        Method = method;
        Body = body is null
            ? Stream.Null
            : new MemoryStream(Encoding.UTF8.GetBytes(body));
    }

    public HttpHost Host => HttpHost.Empty;

    public HttpPath Path { get; }

    public CohesionHttpMethod Method { get; }

    public HttpScheme Scheme => HttpScheme.Http;

    public IHttpQueryCollection Query { get; } = new HttpQueryCollection();

    public IHttpHeaderCollection Headers { get; } = new HttpHeaderCollection();

    public IHttpContext HttpContext { get; }

    public Stream Body { get; }
}

internal sealed class TestHttpResponse : IHttpResponse
{
    public TestHttpResponse(IHttpContext context)
    {
        HttpContext = context;
    }

    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.Ok;

    public IHttpHeaderCollection Headers { get; } = new HttpHeaderCollection();

    public IHttpContext HttpContext { get; }

    public Stream Body { get; set; } = new MemoryStream();
}
