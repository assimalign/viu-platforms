using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Viu;
using Assimalign.Viu.Components;

using Shouldly;
using Xunit;

namespace Assimalign.Cohesion.Viu.Hosting.Browser.Tests;

public sealed class ClientConfigurationTests
{
    [Theory]
    [InlineData("success")]
    [InlineData("missing")]
    [InlineData("invalid")]
    [InlineData("schema")]
    [InlineData("network")]
    [InlineData("timeout")]
    public async Task DocumentIsAvailableBeforeRuntimeStartupAndFailuresOnlyWarn(string scenario)
    {
        List<string> warnings = [];
        using HttpClient client = new(new Handler(scenario)) { BaseAddress = new Uri("https://docs.example/") };
        ViuApplicationBuilder builder = ViuApplication.CreateBuilder();
        builder.ApplicationOptions.RootComponent = new TextNode("test");
        builder.ApplicationOptions.WarnHandler = warnings.Add;
        builder.AddClientConfiguration(client, options => options.Timeout = TimeSpan.FromMilliseconds(50));
        bool started = false;
        builder.ConfigureHost(new Factory(options =>
        {
            ViuClientConfiguration document = options.Services!.GetRequiredService<ViuClientConfigurationProvider>().Current;
            if (scenario == "success")
            {
                document.Environment.ShouldBe("Production");
                document.References["api"]["http"].ShouldBe("https://api.example:443");
            }
            else { document.ShouldBeSameAs(ViuClientConfiguration.Empty); }
            started = true;
        }));
        await using ViuApplication application = builder.Build();
        await application.StartAsync();
        started.ShouldBeTrue();
        warnings.Count.ShouldBe(scenario == "success" ? 0 : 1);
        await application.StopAsync();
    }

    [Fact]
    public void ContractCopiesNestedReferencesAndRoundTripsWithGeneratedMetadata()
    {
        Dictionary<string, string> endpoints = new() { ["http"] = "https://api.example:443" };
        Dictionary<string, IReadOnlyDictionary<string, string>> references = new() { ["api"] = endpoints };
        ViuClientConfiguration document = new(references: references);
        endpoints["http"] = "changed";
        references.Clear();
        document.References["api"]["http"].ShouldBe("https://api.example:443");
        string json = JsonSerializer.Serialize(document, ViuClientConfigurationJsonContext.Default.ViuClientConfiguration);
        JsonSerializer.Deserialize(json, ViuClientConfigurationJsonContext.Default.ViuClientConfiguration)!
            .References["api"]["http"].ShouldBe("https://api.example:443");
    }

    private sealed class Handler(string scenario) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.RequestUri!.AbsolutePath.ShouldBe("/_cohesion/client.json");
            if (scenario == "network") { throw new HttpRequestException("offline"); }
            if (scenario == "timeout") { await Task.Delay(Timeout.Infinite, cancellationToken); }
            string payload = scenario switch
            {
                "invalid" => "not json",
                "schema" => "{\"schema\":\"unknown\"}",
                _ => """{"schema":"cohesion/viu-client/v1","environment":"Production","references":{"api":{"http":"https://api.example:443"}}}""",
            };
            return new HttpResponseMessage(scenario == "missing" ? HttpStatusCode.NotFound : HttpStatusCode.OK)
            { Content = new StringContent(payload) };
        }
    }

    private sealed class Factory(Action<ApplicationOptions> start) : IViuApplicationHostFactory
    {
        public IApplication CreateApplication(ApplicationOptions options) => new Runtime(options, start);
    }

    private sealed class Runtime : IApplication
    {
        private readonly ApplicationOptions _options;
        private readonly Action<ApplicationOptions> _start;
        private readonly ApplicationLifetime _lifetime;

        internal Runtime(ApplicationOptions options, Action<ApplicationOptions> start)
        {
            _options = options;
            _start = start;
            ApplicationContext context = new(options);
            Context = context;
            _lifetime = new ApplicationLifetime(context);
        }

        public IApplicationContext Context { get; }
        public IApplication Use(ApplicationMiddleware middleware) => this;
        public ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            _start(_options);
            _lifetime.StartExecution();
            _lifetime.SignalRunning();
            return ValueTask.CompletedTask;
        }
        public ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            _lifetime.CompleteStopping();
            return ValueTask.CompletedTask;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
