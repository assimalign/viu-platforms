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

builder.Services.AddComponents(GeneratedViuComponents.Register);
builder.Services.AddSingleton<IClock>(_ => new SystemClock());
builder.Configuration["application:title"] = "Cohesion Viu";
builder.ConfigureApplication(options =>
{
    options.RootComponent = new ComponentNode(ComponentReference.ForName("App"));
});

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

Consumers must set `<EnablePreviewFeatures>true</EnablePreviewFeatures>` while the composed
Cohesion `10.0.0-preview.1.local` hosting surface remains preview-annotated.

The built Cohesion provider replaces any manually assigned `ApplicationOptions.Services`. Viu
component activation therefore sees the same provider as host services. `IConfiguration`,
`IConfigurationManager`, `ConfigurationManager`, and `IHostEnvironment` resolve from that
provider, and the same objects are available on `ViuApplicationContext`.

## Registration and trimming

Services may be registered as instances, factories, or implementation types:

```csharp
builder.Services.AddSingleton<IClock, SystemClock>();
```

An implementation type is activated by the Cohesion container, which selects a public constructor
and resolves its parameters from the provider. The container's type-based registration overloads
carry `[DynamicallyAccessedMembers(PublicConstructors)]`, so trimming keeps the constructors they
name. Where dynamic code is not compiled, as on WebAssembly, the container invokes constructors
through reflection rather than emitting IL. A registration whose implementation `Type` the trimmer
cannot see, such as one computed at run time, produces a trimming warning at that call site.

The hosting package itself performs no assembly scanning, configuration binding, or module
initialization, and Viu components are still activated only through their generated registrations.

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
