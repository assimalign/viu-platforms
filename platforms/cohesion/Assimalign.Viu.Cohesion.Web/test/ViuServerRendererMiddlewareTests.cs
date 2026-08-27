using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Assimalign.Cohesion.Http;
using Assimalign.Viu.Cohesion.Web.Internal;
using Assimalign.Viu.Components;
using Assimalign.Viu.ServerRenderer;
using Shouldly;
using Xunit;
using CohesionHttpMethod = Assimalign.Cohesion.Http.HttpMethod;

namespace Assimalign.Viu.Cohesion.Web.Tests;

public sealed class ViuServerRendererMiddlewareTests
{
    [Fact(DisplayName = "Viu Cohesion Web - server renderer: streams markup in a disposed request scope")]
    public async Task InvokeAsync_GetRequest_ShouldRenderAndDisposeScope()
    {
        // Arrange
        RecordingScopeFactory factory = new();
        ViuServerRendererMiddleware middleware = new(
            new ServerRenderAdaptor<IHttpContext>(factory),
            _ => Element("main", [new TextNode("cohesion")]),
            _ => true);
        TestHttpContext context = new("/", CohesionHttpMethod.Get);

        // Act
        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        // Assert
        context.ReadResponseBody().ShouldBe("<main>cohesion</main>");
        context.Response.Headers[new HttpHeaderKey("Content-Type")].ToString()
            .ShouldBe("text/html; charset=utf-8");
        factory.RequestContext.ShouldBeSameAs(context);
        factory.DisposeCount.ShouldBe(1);
    }

    [Fact(DisplayName = "Viu Cohesion Web - server renderer: maps failures before output to status 500")]
    public async Task InvokeAsync_ScopeCreationFailure_ShouldSetInternalServerErrorAndRethrow()
    {
        // Arrange
        FailingScopeFactory factory = new();
        ViuServerRendererMiddleware middleware = new(
            new ServerRenderAdaptor<IHttpContext>(factory),
            _ => Element("main"),
            _ => true);
        TestHttpContext context = new("/", CohesionHttpMethod.Get);

        // Act
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context, _ => Task.CompletedTask));

        // Assert
        exception.Message.ShouldBe("scope failed");
        context.Response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        context.ReadResponseBody().ShouldBeEmpty();
    }

    [Fact(DisplayName = "Viu Cohesion Web - server renderer: delegates non-GET requests")]
    public async Task InvokeAsync_PostRequest_ShouldInvokeNextMiddleware()
    {
        // Arrange
        RecordingScopeFactory factory = new();
        ViuServerRendererMiddleware middleware = new(
            new ServerRenderAdaptor<IHttpContext>(factory),
            _ => Element("main"),
            _ => true);
        TestHttpContext context = new("/", CohesionHttpMethod.Post);
        bool passedThrough = false;

        // Act
        await middleware.InvokeAsync(context, _ =>
        {
            passedThrough = true;
            return Task.CompletedTask;
        });

        // Assert
        passedThrough.ShouldBeTrue();
        factory.RequestContext.ShouldBeNull();
    }

    [Fact(DisplayName = "Viu Cohesion Web - server renderer: delegates GET requests rejected by its predicate")]
    public async Task InvokeAsync_PredicateRejectsGet_ShouldInvokeNextMiddleware()
    {
        // Arrange
        RecordingScopeFactory factory = new();
        ViuServerRendererMiddleware middleware = new(
            new ServerRenderAdaptor<IHttpContext>(factory),
            _ => Element("main"),
            _ => false);
        TestHttpContext context = new("/asset.js", CohesionHttpMethod.Get);
        bool passedThrough = false;

        // Act
        await middleware.InvokeAsync(context, _ =>
        {
            passedThrough = true;
            return Task.CompletedTask;
        });

        // Assert
        passedThrough.ShouldBeTrue();
        factory.RequestContext.ShouldBeNull();
    }

    [Fact(DisplayName = "Viu Cohesion Web - server renderer: maps root factory failures to status 500")]
    public async Task InvokeAsync_RootFactoryFailure_ShouldSetInternalServerErrorAndRethrow()
    {
        // Arrange
        RecordingScopeFactory factory = new();
        ViuServerRendererMiddleware middleware = new(
            new ServerRenderAdaptor<IHttpContext>(factory),
            _ => throw new InvalidOperationException("root failed"),
            _ => true);
        TestHttpContext context = new("/", CohesionHttpMethod.Get);

        // Act
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context, _ => Task.CompletedTask));

        // Assert
        exception.Message.ShouldBe("root failed");
        context.Response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        context.ReadResponseBody().ShouldBeEmpty();
    }

    [Fact(DisplayName = "Viu Cohesion Web - server renderer: replaces failed buffered output with a clean error response")]
    public async Task InvokeAsync_DisposalFailureAfterBufferedOutput_ShouldReplaceResponseCleanly()
    {
        // Arrange
        FailingDisposalScopeFactory factory = new();
        ViuServerRendererMiddleware middleware = new(
            new ServerRenderAdaptor<IHttpContext>(factory),
            _ => new TextNode("buffered\uD83D"),
            _ => true);
        TestHttpContext context = new("/", CohesionHttpMethod.Get);

        // Act
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context, _ => Task.CompletedTask));

        // Assert
        exception.Message.ShouldBe("scope disposal failed");
        context.Response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        context.ReadResponseBody().ShouldBeEmpty();
        factory.DisposeCount.ShouldBe(1);
    }

    [Fact(DisplayName = "Viu Cohesion Web - server renderer: preserves a committed stream after teardown fails")]
    public async Task RenderAsync_DisposalFailureAfterTransportCommit_ShouldPreserveCommittedStream()
    {
        // Arrange
        FailingDisposalScopeFactory factory = new();
        ServerRenderAdaptor<IHttpContext> adaptor = new(factory);
        TestHttpContext context = new("/", CohesionHttpMethod.Get);
        CommittingServerRenderOutput output = new();

        // Act
        ServerRenderResult result = await adaptor.RenderAsync(
            new ServerRenderRequest<IHttpContext>(new TextNode("committed"), context),
            output);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.ResponseCommitted.ShouldBeTrue();
        result.Failure.ShouldBeOfType<InvalidOperationException>()
            .Message.ShouldBe("scope disposal failed");
        output.Text.ShouldBe("committed");
        factory.DisposeCount.ShouldBe(1);
    }

    [Fact(DisplayName = "Viu Cohesion Web - server renderer: passes the exact aborted request token to scope teardown")]
    public async Task InvokeAsync_RequestAbortedAfterBufferedWrite_ShouldPassExactTokenThroughTeardown()
    {
        // Arrange
        using CancellationTokenSource cancellationSource = new();
        TokenRecordingScopeFactory factory = new();
        ViuServerRendererMiddleware middleware = new(
            new ServerRenderAdaptor<IHttpContext>(factory),
            _ => new TextNode("progress\uD83D"),
            _ => true);
        TestHttpContext context = new(
            "/",
            CohesionHttpMethod.Get,
            requestAborted: cancellationSource.Token);
        using AbortOnFirstWriteStream responseBody = new(cancellationSource);
        context.Response.Body = responseBody;

        // Act
        await Should.ThrowAsync<OperationCanceledException>(
            () => middleware.InvokeAsync(context, _ => Task.CompletedTask));

        // Assert
        factory.CreationCancellationToken.ShouldBe(cancellationSource.Token);
        factory.Scope.ShouldNotBeNull();
        factory.Scope.TokenAwareDisposeCount.ShouldBe(1);
        factory.Scope.ParameterlessDisposeCount.ShouldBe(0);
        factory.Scope.DisposalCancellationToken.ShouldBe(cancellationSource.Token);
        factory.Scope.DisposalCancellationToken.IsCancellationRequested.ShouldBeTrue();
        responseBody.WriteCancellationTokens.Count.ShouldBe(2);
        responseBody.WriteCancellationTokens[0].ShouldBe(cancellationSource.Token);
        responseBody.WriteCancellationStates[0].ShouldBeFalse();
        responseBody.WriteCancellationTokens[1].ShouldBe(cancellationSource.Token);
        responseBody.WriteCancellationStates[1].ShouldBeTrue();
        responseBody.FlushCancellationTokens.Count.ShouldBe(2);
        responseBody.FlushCancellationTokens[0].ShouldBe(cancellationSource.Token);
        responseBody.FlushCancellationStates[0].ShouldBeTrue();
        responseBody.FlushCancellationTokens[1].ShouldBe(cancellationSource.Token);
        responseBody.FlushCancellationStates[1].ShouldBeTrue();
    }

    [Fact(DisplayName = "Viu Cohesion Web - server renderer: orders the document prefix, main render, and teleport suffix")]
    public async Task InvokeAsync_DocumentShell_ShouldOrderPrefixMainAndTeleportSuffix()
    {
        // Arrange
        RecordingScopeFactory factory = new();
        TeleportDocumentShell? documentShell = null;
        IHttpContext? documentShellContext = null;
        ViuServerRendererMiddleware middleware = new(
            new ServerRenderAdaptor<IHttpContext>(factory),
            _ => new FragmentNode(
            [
                new TextNode("main-content"),
                new TeleportNode(
                    "#modal",
                    [Element("strong", [new TextNode("teleported")])]),
            ]),
            context =>
            {
                documentShellContext = context;
                documentShell = new TeleportDocumentShell("#modal");
                return documentShell;
            },
            _ => true);
        TestHttpContext context = new("/", CohesionHttpMethod.Get);

        // Act
        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        // Assert
        context.ReadResponseBody().ShouldBe(
            "<!doctype html><main id=\"app\">"
            + HydrationMarkers.FragmentStart
            + "main-content"
            + HydrationMarkers.TeleportStart
            + HydrationMarkers.TeleportEnd
            + HydrationMarkers.FragmentEnd
            + "</main><aside id=\"modal\"><strong>teleported</strong>"
            + HydrationMarkers.TeleportAnchor
            + "</aside>");
        documentShellContext.ShouldBeSameAs(context);
        documentShell.ShouldNotBeNull();
        documentShell.SuffixInvocationCount.ShouldBe(1);
        factory.DisposeCount.ShouldBe(1);
    }

    [Fact(DisplayName = "Viu Cohesion Web - server renderer: preserves split UTF-16 surrogate pairs")]
    public async Task WriteAsync_SurrogatePairSplitAcrossChunks_ShouldWriteOneUnicodeScalar()
    {
        // Arrange
        using MemoryStream responseBody = new();
        await using CohesionServerRenderOutput output = new(
            responseBody,
            CancellationToken.None);

        // Act
        await output.WriteAsync("\uD83D".AsMemory());
        await output.FlushAsync();
        await output.WriteAsync("\uDE03".AsMemory());
        await output.FlushAsync();

        // Assert
        Encoding.UTF8.GetString(responseBody.ToArray()).ShouldBe("😃");
    }

    private static ElementNode Element(
        string name,
        System.Collections.Generic.IReadOnlyList<VirtualNode>? children = null) =>
        new(new QualifiedName(name), children: children);

    private sealed class RecordingScopeFactory : IServerRenderRequestScopeFactory<IHttpContext>
    {
        public IHttpContext? RequestContext { get; private set; }

        public int DisposeCount { get; private set; }

        public ValueTask<IServerRenderRequestScope> CreateAsync(
            ServerRenderRequest<IHttpContext> request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestContext = request.RequestContext;
            return ValueTask.FromResult<IServerRenderRequestScope>(
                new RecordingScope(request.RootComponent, this));
        }

        private sealed class RecordingScope : IServerRenderRequestScope
        {
            private readonly RecordingScopeFactory _owner;

            public RecordingScope(VirtualNode rootComponent, RecordingScopeFactory owner)
            {
                Application = new ServerRenderApplication(rootComponent, new ComponentFactory());
                RenderContext = new SsrContext();
                _owner = owner;
            }

            public ServerRenderApplication Application { get; }

            public SsrContext RenderContext { get; }

            public ValueTask DisposeAsync()
            {
                _owner.DisposeCount++;
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class FailingScopeFactory : IServerRenderRequestScopeFactory<IHttpContext>
    {
        public ValueTask<IServerRenderRequestScope> CreateAsync(
            ServerRenderRequest<IHttpContext> request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<IServerRenderRequestScope>(
                new InvalidOperationException("scope failed"));
    }

    private sealed class FailingDisposalScopeFactory :
        IServerRenderRequestScopeFactory<IHttpContext>
    {
        public int DisposeCount { get; private set; }

        public ValueTask<IServerRenderRequestScope> CreateAsync(
            ServerRenderRequest<IHttpContext> request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IServerRenderRequestScope>(
                new FailingDisposalScope(request.RootComponent, this));
        }

        private sealed class FailingDisposalScope : IServerRenderRequestScope
        {
            private readonly FailingDisposalScopeFactory _owner;

            public FailingDisposalScope(
                VirtualNode rootComponent,
                FailingDisposalScopeFactory owner)
            {
                Application = new ServerRenderApplication(rootComponent, new ComponentFactory());
                RenderContext = new SsrContext();
                _owner = owner;
            }

            public ServerRenderApplication Application { get; }

            public SsrContext RenderContext { get; }

            public ValueTask DisposeAsync()
            {
                _owner.DisposeCount++;
                return ValueTask.FromException(
                    new InvalidOperationException("scope disposal failed"));
            }
        }
    }

    private sealed class TokenRecordingScopeFactory :
        IServerRenderRequestScopeFactory<IHttpContext>
    {
        public CancellationToken CreationCancellationToken { get; private set; }

        public TokenRecordingScope? Scope { get; private set; }

        public ValueTask<IServerRenderRequestScope> CreateAsync(
            ServerRenderRequest<IHttpContext> request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CreationCancellationToken = cancellationToken;
            Scope = new TokenRecordingScope(request.RootComponent);
            return ValueTask.FromResult<IServerRenderRequestScope>(Scope);
        }
    }

    private sealed class TokenRecordingScope : IServerRenderRequestScope
    {
        public TokenRecordingScope(VirtualNode rootComponent)
        {
            Application = new ServerRenderApplication(rootComponent, new ComponentFactory());
            RenderContext = new SsrContext();
        }

        public int ParameterlessDisposeCount { get; private set; }

        public int TokenAwareDisposeCount { get; private set; }

        public CancellationToken DisposalCancellationToken { get; private set; }

        public ServerRenderApplication Application { get; }

        public SsrContext RenderContext { get; }

        public ValueTask DisposeAsync()
        {
            ParameterlessDisposeCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync(CancellationToken cancellationToken)
        {
            TokenAwareDisposeCount++;
            DisposalCancellationToken = cancellationToken;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class AbortOnFirstWriteStream : MemoryStream
    {
        private readonly CancellationTokenSource _cancellationSource;

        public AbortOnFirstWriteStream(CancellationTokenSource cancellationSource)
        {
            _cancellationSource = cancellationSource;
        }

        public List<CancellationToken> WriteCancellationTokens { get; } = [];

        public List<bool> WriteCancellationStates { get; } = [];

        public List<CancellationToken> FlushCancellationTokens { get; } = [];

        public List<bool> FlushCancellationStates { get; } = [];

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            WriteCancellationTokens.Add(cancellationToken);
            WriteCancellationStates.Add(cancellationToken.IsCancellationRequested);
            await base.WriteAsync(buffer, CancellationToken.None);
            if (WriteCancellationTokens.Count == 1)
            {
                _cancellationSource.Cancel();
            }
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            FlushCancellationTokens.Add(cancellationToken);
            FlushCancellationStates.Add(cancellationToken.IsCancellationRequested);
            return FlushCancellationTokens.Count == 1
                ? Task.FromCanceled(cancellationToken)
                : base.FlushAsync(CancellationToken.None);
        }
    }

    private sealed class CommittingServerRenderOutput : IServerRenderOutput
    {
        private readonly StringBuilder _text = new();

        public bool ResponseCommitted { get; private set; }

        public string Text => _text.ToString();

        public ValueTask WriteAsync(
            ReadOnlyMemory<char> content,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _text.Append(content.Span);
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResponseCommitted = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TeleportDocumentShell : IServerRenderDocumentShell
    {
        private readonly string _target;

        public TeleportDocumentShell(string target)
        {
            _target = target;
        }

        public int SuffixInvocationCount { get; private set; }

        public ValueTask WritePrefixAsync(
            IServerRenderOutput output,
            CancellationToken cancellationToken = default) =>
            output.WriteAsync(
                "<!doctype html><main id=\"app\">".AsMemory(),
                cancellationToken);

        public async ValueTask WriteSuffixAsync(
            IServerRenderOutput output,
            IReadOnlyDictionary<string, string> teleports,
            CancellationToken cancellationToken = default)
        {
            SuffixInvocationCount++;
            await output.WriteAsync(
                "</main><aside id=\"modal\">".AsMemory(),
                cancellationToken);
            if (teleports.TryGetValue(_target, out string? payload))
            {
                await output.WriteAsync(payload.AsMemory(), cancellationToken);
            }

            await output.WriteAsync("</aside>".AsMemory(), cancellationToken);
        }
    }
}
