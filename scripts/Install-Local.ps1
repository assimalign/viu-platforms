[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$repositoryDirectory = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryDirectory 'Assimalign.Viu.Platforms.slnx'

# The inventory every pack path shares (scripts/Pack-Release.ps1, release.yml, ci-packages.yml).
Import-Module (Join-Path $PSScriptRoot 'modules/ViuPlatformsPackaging.psm1') -Force
$releasePackage = @(Get-ViuPlatformsReleasePackage -RepositoryDirectory $repositoryDirectory)

Push-Location $repositoryDirectory
try {
    dotnet restore $solutionPath
    dotnet build $solutionPath --configuration $Configuration --no-restore
    dotnet test $solutionPath --configuration $Configuration --no-build
    foreach ($package in $releasePackage) {
        dotnet pack $package.ProjectPath --configuration $Configuration --no-build
    }
}
finally {
    Pop-Location
}
