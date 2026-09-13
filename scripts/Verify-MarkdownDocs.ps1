param(
    [string] $DocumentationRoot = (Join-Path $PSScriptRoot '..\..\viu-docs\docs'),
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$contentRoot = (Resolve-Path -LiteralPath $DocumentationRoot).Path
$verificationRoot = Join-Path $repositoryRoot '_out\verification\markdown-docs'
$fixturePath = Join-Path $repositoryRoot 'platforms\cohesion\Assimalign.Cohesion.Viu.Markdown\test\Fixtures\viu-docs-routes.tsv'
[xml] $versionDocument = Get-Content -LiteralPath (Join-Path $repositoryRoot 'build\Targets\Build.Version.props') -Raw
$versionProperties = $versionDocument.Project.PropertyGroup | Select-Object -First 1
$version = "$($versionProperties.ViuPlatformsVersionPrefix)-$($versionProperties.ViuPlatformsVersionSuffix)"
$escapedContentRoot = [System.Security.SecurityElement]::Escape($contentRoot)

New-Item -ItemType Directory -Path $verificationRoot -Force | Out-Null
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <EnablePreviewFeatures>true</EnablePreviewFeatures>
    <ViuMarkdownContentRoot>$escapedContentRoot</ViuMarkdownContentRoot>
    <ViuMarkdownGeneratedNamespace>MarkdownParity</ViuMarkdownGeneratedNamespace>
    <NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Assimalign.Cohesion.Viu.Markdown" Version="$version" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $verificationRoot 'MarkdownParity.csproj') -Encoding utf8
@'
using System;
using System.Linq;
using Assimalign.Cohesion.Viu.Markdown;

foreach (MarkdownPage page in MarkdownParity.GeneratedMarkdownContent.Catalog.Pages.OrderBy(page => page.RoutePath, StringComparer.Ordinal))
{
    Console.WriteLine("PAGE\t" + page.RoutePath + "\t" + page.AssetPath);
}
'@ | Set-Content -LiteralPath (Join-Path $verificationRoot 'Program.cs') -Encoding utf8

$projectPath = Join-Path $verificationRoot 'MarkdownParity.csproj'
& dotnet build $projectPath --configuration $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Building the packed Markdown catalog verification application failed with exit code $LASTEXITCODE."
}
$runOutput = @(& dotnet run --project $projectPath --configuration $Configuration --no-build)
if ($LASTEXITCODE -ne 0) {
    throw "Running the packed Markdown catalog verification application failed with exit code $LASTEXITCODE."
}
$actual = @($runOutput | Where-Object { $_.StartsWith("PAGE`t", [System.StringComparison]::Ordinal) } | ForEach-Object { $_.Substring(5) })
$expected = @(Get-Content -LiteralPath $fixturePath | Where-Object { $_ -and -not $_.StartsWith('#') })
if ($actual.Count -ne 45 -or $expected.Count -ne 45) {
    throw "Expected exactly 45 pages: generated $($actual.Count), fixture $($expected.Count)."
}
$differences = @(Compare-Object -ReferenceObject $expected -DifferenceObject $actual -CaseSensitive)
if ($differences.Count -ne 0) {
    $differences | Format-Table | Out-String | Write-Host
    throw 'The generated documentation routes/assets differ from the preserved application catalog.'
}
Write-Host "Markdown documentation parity passed: all 45 generated routes and asset paths match the original catalog."
