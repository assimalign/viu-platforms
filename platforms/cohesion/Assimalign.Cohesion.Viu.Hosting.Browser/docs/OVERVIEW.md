# Assimalign.Cohesion.Viu.Hosting.Browser

`Assimalign.Cohesion.Viu.Hosting.Browser` selects Viu's browser renderer for a Cohesion-hosted Viu
application. It adds one explicit builder extension and has no dependency on the full Cohesion App
framework.

## Browser application

Use it from an `Assimalign.Viu.Sdk.Browser` project:

```csharp
using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Viu.Hosting;
using Assimalign.Cohesion.Viu.Hosting.Browser;
using Assimalign.Viu.Components;

ViuApplicationBuilder builder = ViuApplication.CreateBuilder();

builder.Services.Add(ServiceDescriptor.Singleton<IClock>(
    _ => new SystemClock()));
builder.Configuration["application:title"] = "Browser application";
builder.ConfigureApplication(options =>
{
    options.RootComponent = new TextNode("Hello from the browser");
});
builder.ConfigureBrowser(options =>
{
    options.MountTargetSelector = "#app";
    options.Hydrate = false;
});

ViuApplication application = builder.Build();
await application.RunAsync();
```

The consuming project must set `<EnablePreviewFeatures>true</EnablePreviewFeatures>` for the
preview-annotated Cohesion 10.0 preview hosting APIs.

`ConfigureBrowser(...)` installs a host factory explicitly; it does not scan assemblies or use a
module initializer. Its settings live in one `BrowserApplicationOptions` singleton in
`builder.Services`, so repeated calls configure the same instance: a later
`ConfigureBrowser(options => options.Hydrate = true)` keeps a selector set by an earlier call.
`Build()` copies those settings and every Viu application option into `BrowserApplicationBuilder`
and is DOM-free. Browser JavaScript is reached only when the application starts.

`RunAsync()` starts Cohesion host services, mounts Viu into the selected DOM element, and keeps the
WebAssembly application alive until its cancellation token is cancelled or the underlying Viu
runtime requests shutdown. Viu's current public browser lifetime has no separate unload-event hook;
an application may pass a cancellation token when its browser environment can express one.

## Package boundary

The package references only `Assimalign.Cohesion.Viu.Hosting` and `Assimalign.Viu.Browser`.
Configuration, dependency injection, and hosting arrive through the host-neutral package. It does
not reference `Assimalign.Cohesion.App`, does not use `DedicatedThreadService`, and ships no
`wwwroot` content. The Browser framework supplies `_content/Assimalign.Viu.Browser/viu-dom.js`.

## Load the resource client document

Call `builder.AddClientConfiguration(httpClient)` before `Build()`. An optional
`ViuClientConfigurationOptions` callback changes `Path` (default `/_cohesion/client.json`) and
`Timeout` (ten seconds). Register the application's `WarnHandler` to observe missing, malformed,
unsupported-schema, failed or timed-out requests. They retain `ViuClientConfiguration.Empty`.
The supplied HTTP client is borrowed and must have a base address for relative paths.
Components resolve `ViuClientConfigurationProvider` and synchronously read `Current`.
