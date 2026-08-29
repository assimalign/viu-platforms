using System;
using System.IO;

using Shouldly;
using Xunit;

namespace Assimalign.Cohesion.Viu.DevServer.Tests;

public sealed class CommandLineParserTests
{
    [Fact(DisplayName = "Viu Cohesion DevServer - arguments: parses the fixed CLI contract")]
    public void Parse_CompleteCommandLine_ShouldPopulateOptions()
    {
        DevServerOptions options = CommandLineParser.Parse(
        [
            "--content-root", "wwwroot",
            "--endpoints-manifest", "App.staticwebassets.endpoints.json",
            "--runtime-manifest", "App.staticwebassets.runtime.json",
            "--apply-cop-headers",
            "--urls", "http://127.0.0.1:5100;http://127.0.0.1:5101"
        ]);

        options.ContentRootPath.ShouldBe(Path.GetFullPath("wwwroot"));
        options.EndpointsManifestPath.ShouldBe(Path.GetFullPath("App.staticwebassets.endpoints.json"));
        options.RuntimeManifestPath.ShouldBe(Path.GetFullPath("App.staticwebassets.runtime.json"));
        options.ApplyCrossOriginIsolationHeaders.ShouldBeTrue();
        options.Urls.ShouldBe("http://127.0.0.1:5100;http://127.0.0.1:5101");
    }

    [Theory(DisplayName = "Viu Cohesion DevServer - arguments: requires every asset location")]
    [InlineData("--content-root")]
    [InlineData("--endpoints-manifest")]
    [InlineData("--runtime-manifest")]
    public void Parse_MissingRequiredOption_ShouldRejectArguments(string missingOption)
    {
        string[] arguments =
        [
            "--content-root", "wwwroot",
            "--endpoints-manifest", "App.staticwebassets.endpoints.json",
            "--runtime-manifest", "App.staticwebassets.runtime.json"
        ];
        int optionIndex = Array.IndexOf(arguments, missingOption);
        string[] incomplete = [.. arguments[..optionIndex], .. arguments[(optionIndex + 2)..]];

        Action action = () => CommandLineParser.Parse(incomplete);

        action.ShouldThrow<CommandLineException>().Message.ShouldContain(missingOption);
    }

    [Fact(DisplayName = "Viu Cohesion DevServer - arguments: rejects unknown switches")]
    public void Parse_UnknownOption_ShouldRejectArguments()
    {
        Action action = () => CommandLineParser.Parse(["--unknown"]);

        action.ShouldThrow<CommandLineException>().Message.ShouldContain("--unknown");
    }
}
