# Hosting design

## Boundary

The package owns application composition and Cohesion lifetime mechanics, but it does not choose a
renderer. `IViuApplicationHostFactory` is the only platform seam:

```csharp
IApplication CreateApplication(ApplicationOptions applicationOptions);
```

The factory receives a private snapshot and returns an explicitly constructed, single-use Viu
runtime. Browser and server packages remain separate from this host-neutral assembly.

## Build sequence

`ViuApplicationBuilder.Build()` performs one deterministic sequence:

1. Apply the standard-error warning and error handlers to unset diagnostics.
2. Register the shared configuration and environment instances.
3. `TryAdd` the `IComponentFactory` composition factory, capturing the options' component factory.
4. Build the Cohesion provider through `IServiceProviderBuilder`.
5. Snapshot every Viu `ApplicationOptions` member, assign the built provider to `Services` and the
   resolved `IComponentFactory` to `Components`, and resolve unset `State` and `Directives` from
   the provider.
6. Give a second options object to the configured host factory.
7. Resolve `IHostService` registrations.
8. Append the internal Viu runtime adapter as the final hosted service.
9. Create `ViuApplicationContext` and `ViuApplication`.

The retained snapshot and factory snapshot share registered component/state/directive references,
but they are different mutable `ApplicationOptions` objects. A platform builder cannot rewrite the
composition snapshot later exposed for a designed server-rendering host.

Default diagnostics are applied to the builder's own options, not only to the snapshot, because
host services such as the client-configuration loader read the builder's warning handler when the
provider first creates them.

## Component composition

Registration used to require one mutable `ComponentFactory` threaded through every registration
site. A later `ConfigureApplication` assignment silently discarded components that an extension
had registered into the earlier factory. Components are now service contributions:
`AddComponents(Action<ComponentFactory>)` and `AddComponent(ComponentRegistration)` each register
one internal `ViuComponentContribution` singleton, and the composition factory applies
`GetServices<ViuComponentContribution>()` in registration order on first `IComponentFactory`
resolution. `Build()` performs that resolution, so conflicts surface while building.

The composition keeps component resolution on the mount path to a single lookup:

- when the options still hold the builder-owned default factory, contributions are registered
  into it. A second layer would add a miss, including `ComponentFactory`'s camel- and
  Pascal-case name retries, to every mount.
- when the application assigned another factory, contributions are registered into a private
  factory that a layered resolver consults only after the assigned factory misses; with no
  contributions the assigned factory is used unchanged.

`ComponentFactory.Register` reports a duplicate reference as a plain `ArgumentException` naming the
key; composition rethrows it as `InvalidOperationException`. `AddComponent` skips a registration
instance that is already resolvable, so repeated contribution of one registration is idempotent.
The composition registration uses `TryAdd`, so an application `IComponentFactory` service replaces
it. Unset `State` and `Directives` resolve from `IStateStoreRegistry` and `IDirectiveResolver`
services; assigned options win.

## Lifecycle bridge

`ViuApplication` derives from `Host<ViuApplicationContext>`. The internal runtime adapter maps
Cohesion `StartAsync` and `StopAsync` to Viu's `ValueTask` lifecycle. It also registers the Viu
`IApplicationContext.Stopping` token with `HostContext.Shutdown()`.

The adapter is last in `HostedServices`, producing these orders:

```text
start: user service 1 -> user service 2 -> Viu runtime
stop:  Viu runtime -> user service 2 -> user service 1
```

`OnStartingAsync` claims a one-time execution flag before any hosted service starts. A failed or
cancelled first attempt remains consumed, matching Viu's application contract.

## Configuration

A default Cohesion `ConfigurationManager` has no provider and uses `ExistingOnly`, so assigning a
new path would otherwise do nothing. The Viu builder creates the manager with the `Distributed`
set strategy and adds a private writable provider. Direct builder assignments are therefore real,
while callers may still add explicit Cohesion providers.

## Ownership and failure behavior

The Viu runtime borrows its services, state, directives, and component registry. The outer Cohesion
application establishes the ownership boundary instead. Disposal first asks the base host to stop,
then disposes the runtime, provider, and configuration manager. Disposal is best-effort across all
three owned resources and aggregates multiple failures.

The package uses no `InternalsVisibleTo`, runtime XML parsing, or assembly scanning.

## Service activation

`Build()` accepts every descriptor the Cohesion container accepts, including implementation-type
registrations. An earlier revision rejected any descriptor with `ImplementationType` set, which
made `AddSingleton<TService, TImplementation>()`, the most common .NET registration, fail at build
time. The container's type-based overloads are annotated with
`[DynamicallyAccessedMembers(PublicConstructors)]`, so trimming preserves the constructors they
name, and the provider selects its reflection-invoke engine whenever
`RuntimeFeature.IsDynamicCodeCompiled` is false, so WebAssembly never emits IL. Component
activation is unaffected: Viu components are still created only by the activators on their
generated registrations.

## Client configuration boundary

Hosting owns the immutable wire contract and source-generated JSON metadata, so browser and
server agree without either depending on the other. No resource runtime, mounts, settings,
credentials or trust keys are serialized by this type. Mutable caller dictionaries are copied
into read-only nested maps. Browser failure uses the shared `Empty` instance.

The dependency direction remains host-neutral:

```mermaid
flowchart LR
    Browser["Hosting.Browser"] --> Hosting["Hosting: ViuClientConfiguration"]
    Server["Server"] --> Hosting
```

Hosting packages still enumerate Cohesion's `ServiceProviderBuilder.Container` when they need to
find a registered options instance, as `ConfigureBrowser` and `AddRouting` do.
