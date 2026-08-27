# Assimalign.Viu.Cohesion.Templates design

## Package shape

The project is a NuGet `Template` package with no build output or package dependencies. Files
under `content/viu-cohesion` are packed verbatim, allowing `dotnet new` to discover the template
from `.template.config/template.json`.

The template carries exact Viu SDK and Cohesion dev-server versions. MSBuild SDK versions must be
available before project restore can evaluate the generated project, so they are declared in its
`global.json`. The dev-server version is an ordinary `PackageReference` because its build target
is consumed after restore.

## Host boundary

The generated application contains no Cohesion APIs. It remains a normal Viu Browser application;
the `Assimalign.Viu.Cohesion.DevServer` package owns the development-host selection and command
line. This keeps the application portable and prevents Cohesion hosting assemblies from entering
the WebAssembly output.

## Local-feed contract

Phase 2 packages are consumed from local feeds. The template exposes three creation parameters
that replace feed-path placeholders in `NuGet.config`. Their defaults match the integration
workspace, while callers can provide paths for another checkout. A later publication initiative
can remove these local sources without changing the application or hosting boundary.

## AOT and trimming

The application enables full trimming for normal publishing. Ahead-of-time compilation remains an
explicit publish choice because it requires the WebAssembly tools workload and materially extends
build time. `AllowMissingPrunePackageData` keeps restore portable across .NET 10 SDK installations
that omit the optional pruning-data directory; it does not disable the trimmer. Runtime component
registration is generated at compile time; the template does not use reflection, assembly scanning,
or runtime source parsing.
