# Assimalign.Cohesion.Viu.Hosting

`Assimalign.Cohesion.Viu.Hosting` is the host-neutral Cohesion application model for Viu. It
combines Cohesion configuration, dependency injection, environment, hosted-service lifecycle,
and ownership with Viu's explicit `ApplicationOptions` composition seam. Application components
and services are registered in the Cohesion service container; `Build()` composes Viu's options
from it. A platform package selects the concrete Viu runtime.

## Application model

Start composition with `ViuApplication.CreateBuilder()`:

```csharp
using Assimalign.Cohesion.DependencyInjection;
using Assimalign.Cohesion.Viu.Hosting;
using Assimalign.Viu.Components;

ViuApplicationBuilder builder = ViuApplication.CreateBuilder();

builder.Services.AddComponents();
builder.Services.AddSingleton<IClock>(_ => new SystemClock());
builder.Configuration["application:title"] = "Cohesion Viu";
builder.UseRootComponent<App>(); // equivalent to ComponentNode.For<App>() in RootComponent

// A platform package calls builder.ConfigureHost(...), usually through a
// platform-specific extension such as ConfigureBrowser().
ViuApplication application = builder.Build();
await application.RunAsync();
```

The builder exposes:

- `Services`, a Cohesion `ServiceProviderBuilder` that holds services and component contributions;
- `Configuration`, a live `ConfigurationManager` with a writable local provider;
- `Environment`, the `HostEnvironment` registered in the built provider;
- `ApplicationOptions`, Viu's component, root, services, state, directive, and diagnostic
  composition;
- `ConfigureApplication(...)` and `ConfigureHost(...)`;
- a single `Build()` operation.

## Composition from services

`builder.Services.AddComponents(register)` contributes an explicit registration callback, such as
the generated `GeneratedViuComponents.Register` catalog; `AddComponent(registration)` contributes
one registration. Contributions run once, in registration order, when `Build()` composes the
application's component factory, so they do not depend on the order of `ConfigureApplication`
calls. A library can therefore contribute its components from its own registration method without
receiving the application's `ComponentFactory`.

- Contributions join the builder's default `ComponentFactory`, which is also the initial
  `ApplicationOptions.Components`.
- A factory assigned through `ConfigureApplication` takes precedence; contributed components
  resolve only when it misses.
- The composed factory is registered as the `IComponentFactory` service, so application services
  resolve the same resolver as the runtime. Registering your own `IComponentFactory` service
  replaces the composition.
- Contributing the same registration instance twice registers it once. Two different components
  under one reference fail `Build()` with the conflicting reference in the message.

When `ApplicationOptions.State` or `Directives` is unset, `Build()` resolves an
`IStateStoreRegistry` or `IDirectiveResolver` service. Unset `WarnHandler` and `ErrorHandler`
default to writing `Viu warning: ...` and `Viu error (source): ...` lines to standard error.
Assigned values always win.

Use the provider-aware action when component registration needs another service:

```csharp
builder.Services.AddComponents((serviceProvider, components) =>
{
    var catalog = serviceProvider.GetRequiredService<MyComponentCatalog>();
    catalog.Register(components);
});
```

This overload takes `Action<IServiceProvider, ComponentFactory>` and returns no component factory
from the callback. Both callback forms contribute to the same factory in registration order.
The provider-aware action runs once when the factory is first resolved, normally during
application `Build()`, and can use services registered after the `AddComponents` call. It receives
the root service resolver; do not resolve `IComponentFactory` from inside its own configuration.
An explicitly registered `IComponentFactory` service replaces composition and suppresses these
callbacks, just as it suppresses the original component contributions.

Consumers must set `<EnablePreviewFeatures>true</EnablePreviewFeatures>` while the composed
Cohesion 10.0 preview hosting surface remains preview-annotated.

The built Cohesion provider replaces any manually assigned `ApplicationOptions.Services`. Viu
component activation therefore sees the same provider as host services. `IConfiguration`,
`IConfigurationManager`, `ConfigurationManager`, and `IHostEnvironment` resolve from that
provider, and the same objects are available on `ViuApplicationContext`.

## Registration and trimming

Use instance and explicit factory registrations for trimming-safe activation:

```csharp
builder.Services.AddSingleton<IClock>(_ => new SystemClock());
```

The hosting integration performs no reflection activation or assembly scanning. Viu's compiler
emits direct catalog delegates and typed `IComponentDefinition.Registration` members.

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

## Client document contract

Version `10.0.0-preview.1` includes immutable `ViuClientConfiguration` and
`ViuClientConfigurationJsonContext` for `cohesion/viu-client/v1`. The document contains public
application, resource, environment and gateway names plus a nested resource/endpoint URL map.
The constructor defensively copies both map levels; `Empty` represents unavailable configuration.
Server and Hosting.Browser share this contract without adding a Web or browser dependency here.

With no explicit factory or component contributions, `ViuApplicationBuilder` populates the
compile-time generated aggregate by default. `services.AddComponents()` explicitly requests that
aggregate and is idempotent per factory, including application and referenced Viu assemblies.
Explicit callbacks and `AddComponent` suppress the implicit aggregate; combine either with
`AddComponents()` when both are wanted. An options-assigned factory stays first, with explicit
contributions as fallback; a service-registered `IComponentFactory` replaces composition entirely.
`AddRouting` contributes built-in router components; call `AddComponents()` explicitly to
include the generated aggregate alongside routing or other feature contributions.

For HTTP server rendering, prefer `WebApplicationBuilder.Services.AddViuServerRendering` from
the Server package. It composes inside the Web provider and creates one DI scope per render.
