<#
.SYNOPSIS
    Produces the complete, version-consistent Viu Platforms NuGet release set.

.DESCRIPTION
    Restores and builds the solution at the requested version, then packs every package in the
    inventory (scripts/modules/ViuPlatformsPackaging.psm1) into _out/release/packages. The
    generated package-order.txt records dependency-safe publication order and checksums.sha256
    covers every package. .github/workflows/release.yml packs the canonical version through this
    script; .github/workflows/ci-packages.yml packs a CI version.

    The packages restore Cohesion and Viu at whatever global.json pins, so the caller must supply
    the feed credential the same way an ordinary restore does (see README.md, "Getting the
    Assimalign.Cohesion packages").

.PARAMETER Version
    The SemVer package version to produce, for example 10.0.0-preview.1 or
    10.0.0-preview.0.ci.42.

.PARAMETER Configuration
    Build configuration (default Release).

.PARAMETER RepositoryCommit
    The commit stamped into every package's repository metadata. Defaults to GITHUB_SHA, then to
    the checked-out HEAD. ci-packages.yml passes a pull request's head commit, because GITHUB_SHA
    there is GitHub's synthetic merge commit.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Version,

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [ValidatePattern('^[0-9a-f]{40}$')]
    [string] $RepositoryCommit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$versionPattern = '^(?<prefix>(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*))(?:-(?<suffix>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$'
$versionMatch = [System.Text.RegularExpressions.Regex]::Match(
    $Version,
    $versionPattern,
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
if (-not $versionMatch.Success) {
    throw "Version '$Version' must be SemVer in the supported form MAJOR.MINOR.PATCH[-PRERELEASE]."
}

foreach ($identifier in @($versionMatch.Groups['suffix'].Value.Split('.') | Where-Object { $_ })) {
    if ($identifier -match '^[0-9]+$' -and $identifier -notmatch '^(0|[1-9][0-9]*)$') {
        throw "Version '$Version' contains a numeric prerelease identifier with a leading zero."
    }
}

$versionPrefix = $versionMatch.Groups['prefix'].Value
$versionSuffix = $versionMatch.Groups['suffix'].Value

$repositoryDirectory = [System.IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
Import-Module (Join-Path $PSScriptRoot 'modules/ViuPlatformsPackaging.psm1') -Force
$releasePackage = @(Get-ViuPlatformsReleasePackage -RepositoryDirectory $repositoryDirectory)

$packageDirectory = Join-Path $repositoryDirectory '_out/release/packages'
New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
Get-ChildItem -LiteralPath $packageDirectory -File |
    Where-Object { $_.Extension -in @('.nupkg', '.snupkg') -or $_.Name -in @('checksums.sha256', 'package-order.txt') } |
    Remove-Item -Force

# A separate variable: assigning to $RepositoryCommit would re-run its ValidatePattern.
$packageCommit = $RepositoryCommit
if ([string]::IsNullOrWhiteSpace($packageCommit)) {
    $packageCommit = $env:GITHUB_SHA
}
if ([string]::IsNullOrWhiteSpace($packageCommit)) {
    $packageCommit = (& git -C $repositoryDirectory rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw 'Resolving the repository commit failed.'
    }
}

$buildProperties = @(
    "-p:ViuPlatformsVersionPrefix=$versionPrefix",
    "-p:ViuPlatformsVersionSuffix=$versionSuffix",
    "-p:VersionPrefix=$versionPrefix",
    "-p:VersionSuffix=$versionSuffix",
    "-p:PackageVersion=$Version",
    '-p:ContinuousIntegrationBuild=true',
    "-p:RepositoryCommit=$packageCommit",
    "-p:PackageOutputPath=$packageDirectory"
)

function Invoke-Dotnet {
    param([string[]] $Argument)

    & dotnet @Argument
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Argument[0]) failed with exit code $LASTEXITCODE."
    }
}

$solutionPath = Join-Path $repositoryDirectory 'Assimalign.Viu.Platforms.slnx'
Write-Host "Packing Viu Platforms $Version from $packageCommit" -ForegroundColor Cyan
# Restore sees the same version properties as build and pack: NuGet records a project reference's
# package version in the assets file at restore time, and pack writes that version as the
# dependency, so a restore at the canonical version would make a CI package depend on the release.
Invoke-Dotnet (@('restore', $solutionPath) + $buildProperties)
Invoke-Dotnet (@('build', $solutionPath, '--configuration', $Configuration, '--no-restore') + $buildProperties)
foreach ($package in $releasePackage) {
    Write-Host "Packing $($package.Id)" -ForegroundColor Green
    Invoke-Dotnet (@('pack', $package.ProjectPath, '--configuration', $Configuration, '--no-build') + $buildProperties)
}

$expectedPackageFile = @($releasePackage | ForEach-Object { "$($_.Id).$Version.nupkg" })
$actualPackageFile = @(Get-ChildItem -LiteralPath $packageDirectory -Filter '*.nupkg' -File | ForEach-Object Name)
$difference = @(Compare-Object ($expectedPackageFile | Sort-Object) ($actualPackageFile | Sort-Object))
if ($difference.Count -ne 0) {
    throw "The release package set is not exactly the inventory at ${Version}: $($difference | Out-String)"
}

foreach ($package in $releasePackage) {
    $packagePath = Join-Path $packageDirectory "$($package.Id).$Version.nupkg"
    Assert-ViuPlatformsPackageLayout -PackagePath $packagePath -RequiredEntry $package.RequiredEntry
    # CI packs on Linux x64; consumers run on any architecture.
    Assert-ViuPlatformsPortableAssembly -PackagePath $packagePath
}
Write-Host "Validated the package layout and assembly portability of all $($releasePackage.Count) packages." -ForegroundColor Green

$expectedPackageFile | Set-Content -LiteralPath (Join-Path $packageDirectory 'package-order.txt') -Encoding utf8
$checksumLine = foreach ($packageFile in $expectedPackageFile) {
    $hash = Get-FileHash -LiteralPath (Join-Path $packageDirectory $packageFile) -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $packageFile"
}
$checksumLine | Set-Content -LiteralPath (Join-Path $packageDirectory 'checksums.sha256') -Encoding ascii

Write-Host "Created $($expectedPackageFile.Count) packages in $packageDirectory" -ForegroundColor Cyan
$expectedPackageFile | ForEach-Object { Write-Host "  $_" }
