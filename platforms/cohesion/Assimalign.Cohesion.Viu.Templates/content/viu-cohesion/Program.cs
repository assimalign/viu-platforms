using Assimalign.Cohesion.Viu.Hosting;
using Assimalign.Cohesion.Viu.Hosting.Browser;
using Assimalign.Viu;
using Assimalign.Viu.Components;

using CohesionViuApplication;

ViuApplicationBuilder builder = ViuApplication.CreateBuilder();
builder.ConfigureBrowser();

builder.ConfigureApplication(options =>
{
    ComponentFactory components = new();
    GeneratedViuComponents.Register(components);

    options.RootComponent = new ComponentNode(
        ComponentReference.ForName("Counter"));
    options.Components = components;
});

ViuApplication application = builder.Build();
await application.RunAsync();
