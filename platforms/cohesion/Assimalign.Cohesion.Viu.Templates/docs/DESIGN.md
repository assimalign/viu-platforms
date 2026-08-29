# Assimalign.Cohesion.Viu.Templates design

## Package shape

The project is a NuGet `Template` package with no build output or package dependencies. Files
under `content/viu-cohesion` are packed verbatim, allowing `dotnet new` to discover the template
from `.template.config/template.json`.

The template carries exact Viu SDK and Cohesion platform-package versions. MSBuild SDK versions must be
available before project restore can evaluate the generated project, so they are declared in its
`global.json`. The hosting and development-server versions are ordinary `PackageReference` values
consumed after restore.

## Host boundary

The generated application uses `ViuApplication.CreateBuilder()` as its composition root and
explicitly registers the browser runtime through `ConfigureBrowser()`. The
`Assimalign.Cohesion.Viu.DevServer` package separately owns development-host selection and command
line. This hosting model intentionally carries Cohesion dependency-injection, configuration, and
hosting assemblies into the WebAssembly application while keeping the development server out of
the runtime dependency closure.

## Package-feed contract

Published Assimalign packages are consumed from the organization GitHub Packages feed. The
generated `NuGet.config` reads `GITHUB_ACTOR` and `GITHUB_TOKEN`; the token must grant
`read:packages`. nuget.org remains available for Microsoft and third-party dependencies. The
template contains no machine-specific feed paths or checkout parameters.

## AOT and trimming

The application enables full trimming for normal publishing. Ahead-of-time compilation remains an
explicit publish choice because it requires the WebAssembly tools workload and materially extends
build time. `AllowMissingPrunePackageData` keeps restore portable across .NET 10 SDK installations
that omit the optional pruning-data directory; it does not disable the trimmer. Runtime component
registration is generated at compile time; the template does not use reflection, assembly scanning,
or runtime source parsing.
