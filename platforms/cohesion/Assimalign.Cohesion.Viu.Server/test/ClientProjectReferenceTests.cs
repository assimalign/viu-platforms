using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

namespace Assimalign.Cohesion.Viu.Server.Tests;

public sealed class ClientProjectReferenceTests
{
    [Fact]
    public async Task ServerBuildRestoresClientAndUsesQueriedIdentityWithoutCompilingAgainstClient()
    {
        using BuildFixture fixture = new();

        await fixture.RunAsync("build", "Web/Web.csproj", "-c", "Release");

        File.Exists(Path.Combine(fixture.Root, "Client", "obj", "project.assets.json")).ShouldBeTrue();
        string webOutput = Path.Combine(fixture.Root, "Web", "bin", "Release", "net10.0");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(webOutput, "viu-client.json")));
        document.RootElement.GetProperty("client").GetString().ShouldBe("Custom.Client");
        document.RootElement.GetProperty("outputPath").GetString()!.ShouldContain("custom output");
        document.RootElement.GetProperty("runtimeManifestPath").GetString()!.ShouldEndWith("custom.runtime.json");
        document.RootElement.GetProperty("endpointsManifestPath").GetString()!.ShouldEndWith("custom.endpoints.json");
        File.Exists(Path.Combine(webOutput, "Custom.Client.dll")).ShouldBeFalse();
        File.Exists(Path.Combine(webOutput, "wwwroot", "index.html")).ShouldBeFalse();
        File.ReadAllText(Path.Combine(webOutput, "Web.deps.json")).ShouldNotContain("Custom.Client");
        File.Exists(Path.Combine(fixture.Root, "custom output", "Release", "net10.0", "Custom.Client.dll")).ShouldBeTrue();
    }

    [Fact]
    public async Task PublishBundlesCompleteClientRootAndKeepsBrowserPropertiesIndependent()
    {
        using BuildFixture fixture = new();

        await fixture.RunAsync("publish", "Web/Web.csproj", "-c", "Release", "-o", "published");

        string published = Path.Combine(fixture.Root, "published");
        File.ReadAllText(Path.Combine(published, "wwwroot", "index.html")).ShouldBe("client document");
        File.ReadAllText(Path.Combine(published, "wwwroot", "_framework", "runtime.js")).ShouldBe("browser runtime");
        File.ReadAllText(Path.Combine(published, "wwwroot", "_content", "Library", "styles.css")).ShouldBe("library styles");
        File.Exists(Path.Combine(published, "viu-client.json")).ShouldBeFalse();
        // The synthetic Browser query asserts that Web-only RID/AOT settings were removed.
        await fixture.RunAsync("msbuild", "Web/Web.csproj", "-t:ViuResolveClientOutput",
            "-p:RuntimeIdentifier=linux-arm64", "-p:PublishAot=true", "-p:PublishTrimmed=true");
    }

    [Theory]
    [InlineData("browser")]
    [InlineData("server-only")]
    public async Task ViuFrameworksDoNotFlowIntoWebAndExplicitFrameworksArePreserved(string kind)
    {
        using BuildFixture fixture = new(kind);

        await fixture.RunAsync("msbuild", "Web/Web.csproj", "-t:InspectFrameworkIsolation");
    }

    [Theory]
    [InlineData("legacy", "ProjectReference")]
    [InlineData("multiple", "exactly one")]
    [InlineData("missing", "does not exist")]
    [InlineData("ordinary", "Assimalign.Viu.Sdk.Browser")]
    [InlineData("invalid", "not a valid Viu Browser")]
    public async Task InvalidClientReferencesProduceActionableErrors(string kind, string expected)
    {
        using BuildFixture fixture = new(kind);
        string target = kind is "ordinary" or "invalid" ? "ViuResolveClientOutput" : "ViuValidateClientReference";

        string output = await fixture.RunAsync(false, "msbuild", "Web/Web.csproj", "-t:" + target);

        output.ShouldContain(expected);
    }

    private sealed class BuildFixture : IDisposable
    {
        public BuildFixture(string kind = "browser")
        {
            string repository = FindRepository();
            Root = Path.Combine(repository, "_out", "tests", "client-reference", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Write("Directory.Build.props", "<Project><PropertyGroup><AllowMissingPrunePackageData>true</AllowMissingPrunePackageData></PropertyGroup></Project>");
            Write("Directory.Build.targets", "<Project />");
            Write("NuGet.config", "<configuration><packageSources><clear /></packageSources></configuration>");
            Write("Client/Client.cs", "public sealed class ClientOnlyType { }");
            Write("Client/wwwroot/index.html", "client document");
            Write("Client/wwwroot/_framework/runtime.js", "browser runtime");
            Write("Client/wwwroot/_content/Library/styles.css", "library styles");
            string query = kind == "ordinary" ? "" : $$"""
              <Target Name="GetViuBrowserClientInfo" DependsOnTargets="GetTargetPath" Returns="@(BrowserInfo)">
                <Error Condition="'$(RuntimeIdentifier)' != '' or '$(PublishAot)' == 'true' or '$(PublishTrimmed)' == 'true'" Text="Web runtime properties leaked into Browser query." />
                <ItemGroup>
                  <BrowserInfo Include="$(MSBuildProjectFullPath)">
                    <IsViuBrowserApplication>{{(kind == "invalid" ? "false" : "true")}}</IsViuBrowserApplication>
                    <AssemblyName>$(AssemblyName)</AssemblyName><TargetFramework>$(TargetFramework)</TargetFramework>
                    <TargetPath>$(TargetPath)</TargetPath><OutputDirectory>$(TargetDir)</OutputDirectory>
                    <StaticWebAssetsRuntimeManifestPath>$(TargetDir)custom.runtime.json</StaticWebAssetsRuntimeManifestPath>
                    <StaticWebAssetsEndpointsManifestPath>$(TargetDir)custom.endpoints.json</StaticWebAssetsEndpointsManifestPath>
                  </BrowserInfo>
                </ItemGroup>
              </Target>
              """;
            Write("Client/Client.csproj", $$"""
              <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup><TargetFramework>net10.0</TargetFramework><AssemblyName>Custom.Client</AssemblyName><BaseOutputPath>../custom output/</BaseOutputPath></PropertyGroup>
                <ItemGroup><Content Include="wwwroot/**/*" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" /></ItemGroup>
                {{query}}
              </Project>
              """);
            string reference = kind switch
            {
                "legacy" => "<ViuClientReference Include=\"../Client/Client.csproj\" />",
                "missing" => "<ProjectReference Include=\"../Missing.csproj\" ViuClient=\"true\" />",
                "multiple" => "<ProjectReference Include=\"../Client/Client.csproj\" ViuClient=\"true\" /><ProjectReference Include=\"../Other.csproj\" ViuClient=\"true\" />",
                "server-only" => "<ProjectReference Include=\"../Client/Client.csproj\" />",
                _ => "<ProjectReference Include=\"../Client/Client.csproj\" ViuClient=\"true\" />",
            };
            string build = Path.Combine(repository, "platforms", "cohesion", "Assimalign.Cohesion.Viu.Server", "build");
            Write("Web/Web.csproj", $$"""
              <Project Sdk="Microsoft.NET.Sdk">
                <Import Project="{{build}}/Assimalign.Cohesion.Viu.Server.props" />
                <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
                <ItemGroup>{{reference}}</ItemGroup>
                <Import Project="{{build}}/Assimalign.Cohesion.Viu.Server.targets" />
                <Target Name="SeedFrameworkReferences">
                  <ItemGroup>
                    <TransitiveFrameworkReference Include="Assimalign.Viu.App;Assimalign.Viu.App.Browser;Other.Framework" />
                    <FrameworkReference Include="Assimalign.Viu.App" />
                  </ItemGroup>
                </Target>
                <Target Name="InspectFrameworkIsolation" DependsOnTargets="SeedFrameworkReferences;ViuIsolateFrameworkReferences">
                  <Error Condition="'@(TransitiveFrameworkReference)' != 'Other.Framework'" Text="Only the Browser client's transitive framework references should be removed." />
                  <Error Condition="'@(FrameworkReference->WithMetadataValue('Identity', 'Assimalign.Viu.App'))' == ''" Text="Explicit framework references must remain." />
                </Target>
              </Project>
              """);
        }

        public string Root { get; }

        public Task<string> RunAsync(params string[] arguments) => RunAsync(true, arguments);

        public async Task<string> RunAsync(bool success, params string[] arguments)
        {
            ProcessStartInfo start = new("dotnet")
            {
                WorkingDirectory = Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
            start.ArgumentList.Add("-nologo");
            start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
            using Process process = Process.Start(start)!;
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(2));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch { process.Kill(entireProcessTree: true); throw; }
            string log = await output + await error;
            (process.ExitCode == 0).ShouldBe(success, log);
            return log;
        }

        private void Write(string relative, string contents)
        {
            string path = Path.Combine(Root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents);
        }

        private static string FindRepository()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Assimalign.Viu.Platforms.slnx")))
            { directory = directory.Parent; }
            return directory?.FullName ?? throw new InvalidOperationException("Cannot locate the repository for MSBuild fixtures.");
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
