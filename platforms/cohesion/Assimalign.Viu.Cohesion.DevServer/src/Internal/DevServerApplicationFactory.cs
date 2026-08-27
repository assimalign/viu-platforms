using System.Collections.Generic;

using Assimalign.Cohesion.Web.Hosting;
using Assimalign.Viu.Cohesion.Web;

namespace Assimalign.Viu.Cohesion.DevServer;

internal static class DevServerApplicationFactory
{
    public static WebApplication Create(
        DevServerOptions options,
        IReadOnlyList<DevServerAddress> addresses)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.AddViuApplication(viu =>
        {
            viu.ContentRootPath = options.ContentRootPath;
            viu.EndpointsManifestPath = options.EndpointsManifestPath;
            viu.RuntimeManifestPath = options.RuntimeManifestPath;
            viu.ApplyCrossOriginIsolationHeaders = options.ApplyCrossOriginIsolationHeaders;
        });
        builder.Server.UseServer(listener =>
        {
            foreach (DevServerAddress address in addresses)
            {
                listener.UseHttp1(transport =>
                {
                    transport.EndPoint = address.EndPoint;
                    transport.NoDelay = true;
                });
            }
        });

        WebApplication application = builder.Build();
        application.UseViuApplication();
        return application;
    }
}
