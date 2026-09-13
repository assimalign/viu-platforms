using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;

using Xunit;

namespace Assimalign.Cohesion.Viu.Markdown.Tests;

public sealed class ContentSourceTests
{
    [Fact]
    public async Task FileSystemSourceListsAndBuildsTheSameCatalogWithAnAssetPrefix()
    {
        string directory = Path.Combine(Path.GetTempPath(), "viu-markdown-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "guide"));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "index.md"), "# Home\n\nWelcome.");
            await File.WriteAllTextAsync(Path.Combine(directory, "guide", "page.md"), "# Page\n\nSummary.");
            await File.WriteAllTextAsync(Path.Combine(directory, "ignored.txt"), "ignored");
            FileSystemMarkdownContentSource source = new(directory, "docs/");
            (await source.ListAsync()).ShouldBe(["docs/guide/page.md", "docs/index.md"]);
            (await source.ReadAsync("docs/guide/page.md")).ShouldStartWith("# Page");
            MarkdownContentCatalog catalog = await new MarkdownContentCatalogBuilder().BuildAsync(source);
            catalog.Pages.Count.ShouldBe(2);
            catalog.Pages[1].RoutePath.ShouldBe("/guide/page");
            Should.Throw<ArgumentException>(() => source.ReadAsync("docs/../../outside.md"));
            Should.Throw<ArgumentException>(() => source.ReadAsync("elsewhere/page.md"));
            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await source.ListAsync(new CancellationToken(true)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task HttpSourceUsesBorrowedClientBaseAddressAndPropagatesFailures()
    {
        RecordingHandler handler = new();
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://example.test/base/") };
        HttpMarkdownContentSource source = new(client, ["docs/page.md"]);
        (await source.ReadAsync("docs/page.md")).ShouldBe("# Page");
        handler.RequestUri.ShouldBe(new Uri("https://example.test/base/docs/page.md"));
        (await source.ListAsync()).ShouldBe(["docs/page.md"]);
        handler.StatusCode = HttpStatusCode.NotFound;
        await Should.ThrowAsync<HttpRequestException>(() => source.ReadAsync("docs/missing.md"));
        await Should.ThrowAsync<OperationCanceledException>(() => source.ReadAsync("docs/page.md", new CancellationToken(true)));
        await Should.ThrowAsync<NotSupportedException>(() => new HttpMarkdownContentSource(client).ListAsync());
    }

    [Fact]
    public async Task CustomReadOnlySourcesDoNotNeedToImplementListing()
    {
        IMarkdownContentSource source = new ReadOnlySource();
        (await source.ReadAsync("page.md")).ShouldBe("# Custom");
        await Should.ThrowAsync<NotSupportedException>(() => source.ListAsync());
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        internal Uri? RequestUri { get; private set; }
        internal HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(StatusCode) { Content = new StringContent("# Page") });
        }
    }

    private sealed class ReadOnlySource : IMarkdownContentSource
    {
        public Task<string> ReadAsync(string assetPath, CancellationToken cancellationToken = default)
            => Task.FromResult("# Custom");
    }
}
