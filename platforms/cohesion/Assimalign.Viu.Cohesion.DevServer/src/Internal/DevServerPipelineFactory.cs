using Assimalign.Cohesion.Web;
using Assimalign.Viu.Cohesion.Web;

namespace Assimalign.Viu.Cohesion.DevServer;

internal static class DevServerPipelineFactory
{
    public static IWebApplicationPipeline Create(DevServerOptions options)
    {
        DevServerPipelineBuilder builder = new();
        builder.UseViuApplication(viu =>
        {
            viu.ContentRootPath = options.ContentRootPath;
            viu.EndpointsManifestPath = options.EndpointsManifestPath;
            viu.RuntimeManifestPath = options.RuntimeManifestPath;
            viu.ApplyCrossOriginIsolationHeaders = options.ApplyCrossOriginIsolationHeaders;
        });

        return builder.Build();
    }
}
