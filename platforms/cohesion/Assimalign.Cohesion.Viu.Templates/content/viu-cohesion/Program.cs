using Assimalign.Cohesion.Viu.Hosting;
using Assimalign.Cohesion.Viu.Hosting.Browser;
using Assimalign.Cohesion.Viu.Routing;
using Assimalign.Viu.Router;

using CohesionViuApplication;

ViuApplicationBuilder builder = ViuApplication.CreateBuilder();
builder.ConfigureBrowser();

builder.Services.AddComponents();
builder.Services.AddRouting(routing => routing.Routes.Add<Counter>("/", "Counter"));
builder.UseRootComponent<App>();

await using ViuApplication application = builder.Build();
application.UseRouter();
await application.RunAsync();
