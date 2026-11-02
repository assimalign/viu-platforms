using System;
using System.Threading.Tasks;

using Assimalign.Cohesion.ApplicationModel.Hosting.InProcess;
using Assimalign.Cohesion.Web.Hosting;
using Assimalign.Cohesion.Web.Hosting.ApplicationModel;

namespace Assimalign.Cohesion.Viu.Server.Tests;

/// <summary>
/// The <c>docs-site</c> resource's program, as a Viu site's <c>Program.cs</c> is: a plain Web
/// application that makes itself a resource, serves its browser client, and publishes the API
/// address its <c>Api:Url</c> setting received in the client document. Nothing in it knows where it
/// runs.
/// </summary>
internal static class SiteProgram
{
    public const string ResourceName = "docs-site";

    /// <summary>Gets or sets the client's physical content root the program serves.</summary>
    public static string ContentRootPath { get; set; } = string.Empty;

    public static async Task MainAsync(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.Services.AddResource(ResourceName, resource => resource.UseInProcess());
        builder.Services.AddViuClient(viu =>
        {
            viu.ContentRootPath = ContentRootPath;
            if (builder.Configuration["Api:Url"] is { } api)
            {
                viu.AddClientReference("api", "http", new Uri(api));
            }
        });

        await using WebApplication application = builder.Build();
        application.UseViuApplication();
        await application.RunAsync();
    }
}
