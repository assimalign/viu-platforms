using System;
using System.Net.Http;
using System.Runtime.InteropServices.JavaScript;

using Assimalign.Cohesion.Viu.Hosting;
using Assimalign.Cohesion.Viu.Hosting.Browser;
using Assimalign.Viu;
using Assimalign.Viu.Components;

using CohesionViuSite.Client;

using JSObject location = JSHost.GlobalThis.GetPropertyAsJSObject("location")!;
using HttpClient httpClient = new() { BaseAddress = new Uri(location.GetPropertyAsString("href")!) };
ViuApplicationBuilder builder = ViuApplication.CreateBuilder();
builder.ConfigureBrowser();
builder.AddClientConfiguration(httpClient);
builder.ConfigureApplication(options =>
{
    ComponentFactory components = new();
    GeneratedViuComponents.Register(components);
    options.Components = components;
    options.RootComponent = new ComponentNode(ComponentReference.ForName("Counter"));
    options.WarnHandler = Console.Error.WriteLine;
});

await using ViuApplication application = builder.Build();
await application.RunAsync();
