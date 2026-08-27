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
$webProjectPath = Join-Path $repositoryDirectory 'platforms/cohesion/Assimalign.Viu.Cohesion.Web/src/Assimalign.Viu.Cohesion.Web.csproj'
$developmentServerProjectPath = Join-Path $repositoryDirectory 'platforms/cohesion/Assimalign.Viu.Cohesion.DevServer/src/Assimalign.Viu.Cohesion.DevServer.csproj'
$templatesProjectPath = Join-Path $repositoryDirectory 'platforms/cohesion/Assimalign.Viu.Cohesion.Templates/Assimalign.Viu.Cohesion.Templates.csproj'

Push-Location $repositoryDirectory
try {
    dotnet restore $solutionPath
    dotnet build $solutionPath --configuration $Configuration --no-restore
    dotnet test $solutionPath --configuration $Configuration --no-build
    dotnet pack $webProjectPath --configuration $Configuration --no-build
    dotnet pack $developmentServerProjectPath --configuration $Configuration --no-build
    dotnet pack $templatesProjectPath --configuration $Configuration --no-build
}
finally {
    Pop-Location
}
