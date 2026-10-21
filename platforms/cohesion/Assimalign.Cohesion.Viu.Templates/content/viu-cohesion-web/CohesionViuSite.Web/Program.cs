using Assimalign.Cohesion.Viu.Server;
using Assimalign.Cohesion.Web.Hosting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddViuClient();
await using WebApplication application = builder.Build();
application.UseViuApplication();
await application.RunAsync();
