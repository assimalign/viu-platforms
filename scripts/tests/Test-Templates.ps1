<#
.SYNOPSIS
    Builds both packed templates as isolated consumers, including the Web-to-Browser restore graph.
.DESCRIPTION
    Run Install-Local.ps1 first. Uses a fresh template hive, package cache and projects under _out.
    Viu and Cohesion stay on the generated authenticated feed; only the local platform feed is
    redirected to this worktree's packages. Publishes Web with JIT hosting and a trimmed Browser.
#>
[CmdletBinding()]
param(
    [string] $PackageVersion,
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$repositoryDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Import-Module (Join-Path $PSScriptRoot '../modules/ViuPlatformsPackaging.psm1') -Force
if ([string]::IsNullOrWhiteSpace($PackageVersion)) {
    $PackageVersion = Get-ViuPlatformsVersion -RepositoryDirectory $repositoryDirectory
}
$packageDirectory = Join-Path $repositoryDirectory '_out/packages'
$packagePath = Join-Path $packageDirectory "Assimalign.Cohesion.Viu.Templates.$PackageVersion.nupkg"
# Keep framework-pack paths below Windows MSBuild's MAX_PATH boundary.
$fixture = Join-Path $repositoryDirectory "_out/t-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
$hive = Join-Path $fixture 'hive'
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
# Generated consumers must not inherit this repository's version overrides or preview settings.
Set-Content -LiteralPath (Join-Path $fixture 'Directory.Build.props') -Value '<Project />'
Set-Content -LiteralPath (Join-Path $fixture 'Directory.Build.targets') -Value '<Project />'

dotnet new install $packagePath --force --debug:custom-hive $hive
foreach ($template in @(
    @{ ShortName = 'viu-cohesion'; Name = 'TemplateBrowser'; Directory = 'browser' },
    @{ ShortName = 'viu-cohesion-web'; Name = 'TemplateSite'; Directory = 'site' }
)) {
    $output = Join-Path $fixture $template.Directory
    dotnet new $template.ShortName --name $template.Name --output $output --assimalignRoot $repositoryDirectory --debug:custom-hive $hive
    $configPath = Join-Path $output 'NuGet.config'
    [xml] $config = Get-Content -LiteralPath $configPath -Raw
    $config.SelectSingleNode('/configuration/packageSources/add[@key="viu-platforms-local"]').SetAttribute('value', $packageDirectory)
    $config.SelectSingleNode('/configuration/config/add[@key="globalPackagesFolder"]').SetAttribute('value', (Join-Path $fixture 'p'))
    # Local is built in. Keep the optional external gateway feed out of this standalone lane.
    $externalSource = $config.SelectSingleNode('/configuration/packageSources/add[@key="cohesion-platforms-local"]')
    $null = $externalSource.ParentNode.RemoveChild($externalSource)
    $externalMapping = $config.SelectSingleNode('/configuration/packageSourceMapping/packageSource[@key="cohesion-platforms-local"]')
    $null = $externalMapping.ParentNode.RemoveChild($externalMapping)
    $config.Save($configPath)
    $project = if ($template.Directory -eq 'site') { Join-Path $output 'TemplateSite.Web/TemplateSite.Web.csproj' } else { Join-Path $output 'TemplateBrowser.csproj' }
    # One build restores the Browser through the Web ProjectReference; never restore Client alone.
    dotnet build $project --configuration $Configuration -p:TreatWarningsAsErrors=true -p:RestoreNoHttpCache=true
    if ($template.Directory -eq 'site') {
        dotnet build (Join-Path $output 'TemplateSite.slnx') --configuration $Configuration -p:TreatWarningsAsErrors=true
    }
}

$webProject = Join-Path $fixture 'site/TemplateSite.Web/TemplateSite.Web.csproj'
$publication = Join-Path $fixture 'published'
dotnet publish $webProject --configuration $Configuration -p:PublishAot=false -p:TreatWarningsAsErrors=true --output $publication
if (-not (Test-Path -LiteralPath (Join-Path $publication 'wwwroot/index.html')) -or
    @(Get-ChildItem -LiteralPath (Join-Path $publication 'wwwroot/_framework') -Filter 'dotnet.*.js').Count -eq 0 -or
    (Test-Path -LiteralPath (Join-Path $publication 'viu-client.json'))) {
    throw 'Published Web must contain the complete client wwwroot and must omit the build-only client document.'
}

# A Cohesion-only executable must aggregate a referenced Viu library without catalog callbacks.
# Extend this generated consumer after validating the unchanged template and published assets.
$shared = Join-Path $fixture 'site/Shared'
New-Item -ItemType Directory -Path $shared -Force | Out-Null
Set-Content -LiteralPath (Join-Path $shared 'Shared.csproj') -Value @'
<Project Sdk="Assimalign.Viu.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>TemplateSmoke.Shared</RootNamespace>
    <ViuServerRendering>true</ViuServerRendering>
    <EnablePreviewFeatures>true</EnablePreviewFeatures>
    <AllowMissingPrunePackageData>true</AllowMissingPrunePackageData>
  </PropertyGroup>
</Project>
'@
Set-Content -LiteralPath (Join-Path $shared 'Report.cs') -Value @'
using System;
using System.Threading.Tasks;
namespace TemplateSmoke.Shared;
public partial class Report;
public sealed record ReportMessage(string Value) : IAsyncDisposable
{
    public static int Disposals;
    public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
}
'@
Set-Content -LiteralPath (Join-Path $shared 'Report.viu') -Value @'
<template><p>{{ Message }}</p></template>
@script {
    public string Message { get; private set; } = "";
    partial void OnSetup()
    {
        Message = ((ReportMessage)Context!.Services!.GetService(typeof(ReportMessage))!).Value;
    }
}
'@
[xml] $web = Get-Content -LiteralPath $webProject -Raw
$items = $web.CreateElement('ItemGroup')
$reference = $web.CreateElement('ProjectReference')
$reference.SetAttribute('Include', '../Shared/Shared.csproj')
$null = $items.AppendChild($reference)
$context = $web.CreateElement('Compile')
$context.SetAttribute('Include', (Join-Path $repositoryDirectory 'platforms/cohesion/Assimalign.Cohesion.Viu.Server/test/TestObjects/TestHttpContext.cs'))
$context.SetAttribute('Link', 'TestHttpContext.cs')
$null = $items.AppendChild($context)
$null = $web.DocumentElement.AppendChild($items)
$web.Save($webProject)
Set-Content -LiteralPath (Join-Path (Split-Path $webProject -Parent) 'Program.cs') -Value @'
using System;
using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Http;
using Assimalign.Cohesion.Viu.Server;
using Assimalign.Cohesion.Viu.Server.Tests;
using Assimalign.Cohesion.Web;
using Assimalign.Cohesion.Web.Hosting;
using TemplateSmoke.Shared;
WebApplicationBuilder builder = WebApplication.CreateBuilder();
builder.Services.AddScoped<ReportMessage>(_ => new ReportMessage("web scoped service"));
builder.Services.AddViuServerRendering(rendering => rendering.UseRootComponent<Report>());
await using WebApplication application = builder.Build();
application.UseViuServerRenderer();
IWebApplicationPipeline pipeline = ((IWebApplicationPipelineBuilder)application).Build();
TestHttpContext context = new("/", HttpMethod.Get);
await pipeline.ExecuteAsync(context);
string markup = context.ReadResponseBody();
if (!markup.Contains("<p>web scoped service</p>") || ReportMessage.Disposals != 1)
{
    throw new InvalidOperationException(markup + "; disposed=" + ReportMessage.Disposals);
}
Console.WriteLine("Packed SSR smoke passed: " + markup);
'@
dotnet run --project $webProject --configuration $Configuration -p:TreatWarningsAsErrors=true
Write-Host "Template build/publish validation passed: $fixture" -ForegroundColor Green
