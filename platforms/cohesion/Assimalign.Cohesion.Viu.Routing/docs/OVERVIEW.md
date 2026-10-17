# Assimalign.Cohesion.Viu.Routing

`Assimalign.Cohesion.Viu.Routing` registers Viu's router in a Cohesion-hosted application's service
container and installs it around the application lifetime. Like `Assimalign.Viu.Router`, it is
opt-in: an application that does not route does not reference it.

## Register and use the router

```csharp
using Assimalign.Cohesion.Viu.Hosting;
using Assimalign.Cohesion.Viu.Hosting.Browser;
using Assimalign.Cohesion.Viu.Routing;
using Assimalign.Viu.Components;
using Assimalign.Viu.Router;

ViuApplicationBuilder builder = ViuApplication.CreateBuilder();

builder.Services.AddComponents(GeneratedViuComponents.Register);
builder.Services.AddRouting(routing =>
{
    routing.Routes.Add(new RouteRecord(
        "/",
        component: new ComponentNode(ComponentReference.ForName("HomePage"))));
});
builder.ConfigureApplication(options =>
    options.RootComponent = new ComponentNode(RouterView.Registration.Reference));
builder.ConfigureBrowser();

await using ViuApplication application = builder.Build();
application.UseRouter();
await application.RunAsync();
```

`AddRouting` registers one `Router`, its `IRouterHistory`, and `RouterView` and `RouterLink` under
both their type and template names. Repeated calls configure the same `ViuRoutingOptions`.
`UseRouter()` resolves that router and installs Viu's router middleware: initial navigation
settles before the root mounts, and the router-to-DOM bridge is removed when the application
stops or fails. It throws when routing was not registered.

## Options

- `Routes` — the application's top-level route records.
- `ScrollBehavior`, `LinkActiveClass`, and `LinkExactActiveClass` — copied to the router.
- `ConfigureRouter((services, router) => ...)` — runs after construction, in registration order,
  for guards and hooks that need application services.

The router reads the options once, when it is first resolved.

## Contributed routes

A module contributes routes without receiving the application's route list. For example, with
`Assimalign.Cohesion.Viu.Markdown` registered:

```csharp
builder.Services.AddRoutes(services => MarkdownRoutes.Create(
    services.GetRequiredService<MarkdownContentCatalog>(),
    new MarkdownRouteOptions { LayoutComponent = ComponentReference.ForName("AppShell") }));
```

Each factory runs once, when the router is first resolved. Contributed routes follow `Routes` in
the route table, in registration order, and may be registered before `AddRouting`.

## History

The default history is browser web history in a browser and memory history elsewhere. Register an
`IRouterHistory` before or after `AddRouting` to replace it:

```csharp
builder.Services.AddSingleton<IRouterHistory>(_ => BrowserRouterHistory.CreateWebHash());
```

## Lifetime and ownership

The router and the default history are singletons created on first resolution and disposed with
the application's provider, the router first. A history registered as an existing instance remains
owned by the caller. Every registration is an explicit instance or factory; the package activates
no type by reflection.

## Package boundary

The package references `Assimalign.Cohesion.Viu.Hosting` and `Assimalign.Viu.Browser.Router`, which
brings `Assimalign.Viu.Router` and `Assimalign.Viu.Browser`. `Assimalign.Viu.Browser.Router` flows
its browser-history script into consuming WebAssembly applications, which is why routing is not
part of `Assimalign.Cohesion.Viu.Hosting.Browser`.
