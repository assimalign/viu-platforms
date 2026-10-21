using System;
using System.Net.Http;
using System.Runtime.InteropServices.JavaScript;

using Assimalign.Cohesion.Viu.Hosting;
using Assimalign.Cohesion.Viu.Hosting.Browser;
using Assimalign.Cohesion.Viu.Routing;
using Assimalign.Viu.Router;

using CohesionViuSite.Client;

using JSObject location = JSHost.GlobalThis.GetPropertyAsJSObject("location")!;
using HttpClient httpClient = new() { BaseAddress = new Uri(location.GetPropertyAsString("href")!) };
ViuApplicationBuilder builder = ViuApplication.CreateBuilder();
builder.ConfigureBrowser();
builder.AddClientConfiguration(httpClient);
builder.Services.AddComponents();
builder.Services.AddRouting(routing => routing.Routes.Add<Counter>("/", "Counter"));
builder.UseRootComponent<App>();
builder.ConfigureApplication(options => options.WarnHandler = Console.Error.WriteLine);

await using ViuApplication application = builder.Build();
application.UseRouter();
await application.RunAsync();
