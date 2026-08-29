# Assimalign.Cohesion.Viu.Hosting

`Assimalign.Cohesion.Viu.Hosting` is the host-neutral Cohesion application model for Viu. It
combines Cohesion configuration, dependency injection, environment, hosted-service lifecycle,
and ownership with Viu's explicit `ApplicationOptions` composition seam. A platform package
selects the concrete Viu runtime.

## Application model

Start composition with `ViuApplication.CreateBuilder()`:

```csharp
using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Viu.Hosting;
using Assimalign.Viu.Components;

ViuApplicationBuilder builder = ViuApplication.CreateBuilder();

builder.Services.Add(ServiceDescriptor.Singleton<IClock>(
    _ => new SystemClock()));
builder.Configuration["application:title"] = "Cohesion Viu";
builder.ConfigureApplication(options =>
{
    options.RootComponent = new TextNode("Hello from Viu");
});

// A platform package calls builder.ConfigureHost(...), usually through a
// platform-specific extension such as ConfigureBrowser().
ViuApplication application = builder.Build();
await application.RunAsync();
```

The builder exposes:

- `Services`, a Cohesion `ServiceProviderBuilder`;
- `Configuration`, a live `ConfigurationManager` with a writable local provider;
- `Environment`, the `HostEnvironment` registered in the built provider;
- `ApplicationOptions`, Viu's component, root, services, state, directive, and diagnostic
  composition;
- `ConfigureApplication(...)` and `ConfigureHost(...)`;
- a single `Build()` operation.

Consumers must set `<EnablePreviewFeatures>true</EnablePreviewFeatures>` while the composed
Cohesion `10.0.0-beta.1` hosting surface remains preview-annotated.

The built Cohesion provider replaces any manually assigned `ApplicationOptions.Services`. Viu
component activation therefore sees the same provider as host services. `IConfiguration`,
`IConfigurationManager`, `ConfigurationManager`, and `IHostEnvironment` resolve from that
provider, and the same objects are available on `ViuApplicationContext`.

## AOT-safe registration boundary

This hosting model does not perform constructor discovery. Every service registration must use an
explicit instance or factory. `Build()` rejects descriptors whose `ImplementationType` is set,
including registrations produced by type-only `AddSingleton<TService, TImplementation>()`
overloads. This keeps the integration path free of reflection-based activation, assembly scans,
and module initializers.

## Lifetime and ownership

`ViuApplication` is both a Viu `IApplication` and a Cohesion `IHost`. User `IHostService`
instances start in registration order, followed by the Viu runtime. Shutdown reverses that order,
so the Viu tree unmounts before user services stop. The underlying Viu stopping token requests
Cohesion host shutdown, allowing `RunAsync()` to complete when the platform runtime stops itself.

The application lifetime is deliberately single-use even though Cohesion's general host base can
restart other hosts. Disposing the application stops it and then releases the Viu runtime, built
service provider, and configuration manager in that order. The builder itself must not be
disposed; the current Cohesion `ServiceProviderBuilder` is only a registration/build object.

## Server composition snapshot

`CreateApplicationOptionsSnapshot()` returns a new shallow options object over the frozen build
references. Designed host integrations such as Viu server rendering can use that method to create
a request-local runtime without reflection or access to Viu internals. The returned options object
is mutable, but changing it does not change the application's retained composition snapshot.
