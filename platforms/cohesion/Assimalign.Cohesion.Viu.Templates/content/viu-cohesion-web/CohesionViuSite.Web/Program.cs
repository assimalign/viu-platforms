using Assimalign.Cohesion.Viu.Server;
using Assimalign.Cohesion.Web.Hosting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddViuApplication(options => options.UseClientOutput("CohesionViuSite.Client"));
await using WebApplication application = builder.Build();
application.UseViuApplication();
await application.RunAsync();
