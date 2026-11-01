# __PROJECT_NAME__

The Client runs Viu in the browser. Web is an ordinary Cohesion Web application serving that client
and its public `/_cohesion/client.json` document; one call in its `Program.cs`,
`builder.Services.AddResource("__APPLICATION_NAME__-web", resource => resource.UseInProcess())`,
makes it a managed resource when a gateway starts it. Gateway declares the model and runs Web in its
own process on Cohesion's in-process platform. All project SDKs are pinned in `global.json`.

## Local packages

This template pins Viu `10.0.0-preview.2`, Cohesion `__COHESION_PACKAGE_VERSION__`, and platform
packages `10.0.0-preview.1`. Pack the local platform family before building.
When creating the template outside the sibling checkout directory, pass
`--assimalignRoot C:/Source/repos/assimalign`. The `--cohesionVersion` parameter selects another
complete SDK/framework/library identity. Keep SDK and library identities aligned.

```pwsh
dotnet build
dotnet run --project __PROJECT_NAME__.Web       # the site on its own, unmanaged
dotnet run --project __PROJECT_NAME__.Gateway   # the gateway, which runs the site and prints its address
```

On its own, Web binds Web.Hosting's development default, `http://127.0.0.1:5000`; configure an
`Http` section, or set `COHESION_ENDPOINT_HTTP_SCHEME`, `_HOST` and `_PORT` together, to choose
another. Under the gateway, the platform
assigns its port and Gateway prints the address. Stop either with Ctrl+C.
The client configuration provider is populated before components start; failed document requests
warn and retain an empty configuration. The supplied `HttpClient` belongs to Program.

## The gateway

Gateway is an executable on `Assimalign.Cohesion.Sdk.Gateway`. It references Web, registers Web's
entry point on the in-process platform, and declares one resource:

```csharp
await using var infrastructure = new InProcessInfrastructure();
infrastructure.AddProgram("Web", "__APPLICATION_NAME__-web", CohesionViuSite.Web.Program.MainAsync);
ApplicationGatewayBuilder builder = ApplicationGateway.CreateBuilder("__APPLICATION_NAME__").UseInProcess(infrastructure);
builder.AddResource("Web", "__APPLICATION_NAME__-web");
```

Settings the gateway passes (`builder.AddResource(...).WithSetting("Api:Url", ...)`) arrive in Web as
configuration. Publish one in the client document from an `AddViuClient` callback. A callback turns
off client inference, so it calls `UseClientOutput()` itself:

```csharp
builder.Services.AddViuClient(viu =>
{
    viu.UseClientOutput();
    if (builder.Configuration["Api:Url"] is { } api)
    {
        viu.AddClientReference("api", "http", new Uri(api));
    }
});
```

Another platform, such as the Kubernetes pair from `cohesion-platforms`, changes both projects. Web
references that platform's instance package and opts into it in `Program.cs` (`UseKubernetes()`
instead of, or beside, `UseInProcess()`). Gateway replaces `InProcessInfrastructure`, `AddProgram`
and its reference to Web with that platform's infrastructure and its `builder.Use<Platform>(...)`
verb, because the platform runs Web as its own deployment.

## Publish

A normal `<ProjectReference Include="../Client/Client.csproj" ViuClient="true" />` joins the restore
graph and establishes build order without a compile reference. The Browser SDK identity query
supplies exact manifest paths; `builder.Services.AddViuClient()` infers the single client.
Building Web restores Client automatically; no separate restore is required. Publishing Web
publishes the client in Release using its own trimming settings, then bundles its entire physical
`wwwroot`, including framework assets and compressed siblings. Published hosting needs no manifests.

```pwsh
dotnet publish __PROJECT_NAME__.Web -c Release -r linux-arm64
```

Components use `builder.Services.AddComponents()`, `builder.UseRootComponent<App>()` and
`routing.Routes.Add<Counter>("/", "Counter")`. No catalog aliases or component-name strings are
needed. Supply GitHub Packages credentials with `NuGetPackageSourceCredentials_assimalign`;
no credentials are stored in the template.
