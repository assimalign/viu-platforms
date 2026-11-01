using System.Threading.Tasks;

using Assimalign.Cohesion.ApplicationModel.Hosting.InProcess;
using Assimalign.Cohesion.Viu.Server;
using Assimalign.Cohesion.Web.Hosting;
using Assimalign.Cohesion.Web.Hosting.ApplicationModel;

namespace CohesionViuSite.Web;

/// <summary>The Web program; the gateway runs <see cref="MainAsync"/> in its own process.</summary>
public static class Program
{
    /// <summary>Runs the site on its own.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>A task that completes when the site has stopped.</returns>
    public static Task Main(string[] args) => MainAsync(args);

    /// <summary>Runs the site.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>A task that completes when the site has stopped.</returns>
    public static async Task MainAsync(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Started by the gateway, the site publishes itself through the platform the gateway names and
        // the gateway manages it; started on its own, no platform is named and this registers nothing.
        builder.Services.AddResource("__APPLICATION_NAME__-web", resource => resource.UseInProcess());
        builder.Services.AddViuClient();

        await using WebApplication application = builder.Build();
        application.UseViuApplication();
        await application.RunAsync();
    }
}
