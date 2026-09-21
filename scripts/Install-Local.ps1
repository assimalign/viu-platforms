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
$hostingProjectPath = Join-Path $repositoryDirectory 'platforms/cohesion/Assimalign.Cohesion.Viu.Hosting/src/Assimalign.Cohesion.Viu.Hosting.csproj'
$applicationModelProjectPath = Join-Path $repositoryDirectory 'platforms/cohesion/Assimalign.Cohesion.Viu.ApplicationModel/src/Assimalign.Cohesion.Viu.ApplicationModel.csproj'
$markdownProjectPath = Join-Path $repositoryDirectory 'platforms/cohesion/Assimalign.Cohesion.Viu.Markdown/src/Assimalign.Cohesion.Viu.Markdown.csproj'
$browserHostingProjectPath = Join-Path $repositoryDirectory 'platforms/cohesion/Assimalign.Cohesion.Viu.Hosting.Browser/src/Assimalign.Cohesion.Viu.Hosting.Browser.csproj'
$serverProjectPath = Join-Path $repositoryDirectory 'platforms/cohesion/Assimalign.Cohesion.Viu.Server/src/Assimalign.Cohesion.Viu.Server.csproj'
$developmentServerProjectPath = Join-Path $repositoryDirectory 'platforms/cohesion/Assimalign.Cohesion.Viu.DevServer/src/Assimalign.Cohesion.Viu.DevServer.csproj'
$templatesProjectPath = Join-Path $repositoryDirectory 'platforms/cohesion/Assimalign.Cohesion.Viu.Templates/Assimalign.Cohesion.Viu.Templates.csproj'

Push-Location $repositoryDirectory
try {
    dotnet restore $solutionPath
    dotnet build $solutionPath --configuration $Configuration --no-restore
    dotnet test $solutionPath --configuration $Configuration --no-build
    dotnet pack $hostingProjectPath --configuration $Configuration --no-build
    dotnet pack $applicationModelProjectPath --configuration $Configuration --no-build
    dotnet pack $markdownProjectPath --configuration $Configuration --no-build
    dotnet pack $browserHostingProjectPath --configuration $Configuration --no-build
    dotnet pack $serverProjectPath --configuration $Configuration --no-build
    dotnet pack $developmentServerProjectPath --configuration $Configuration --no-build
    dotnet pack $templatesProjectPath --configuration $Configuration --no-build
}
finally {
    Pop-Location
}
