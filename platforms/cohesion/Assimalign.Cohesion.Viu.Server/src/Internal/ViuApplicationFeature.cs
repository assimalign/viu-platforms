using Assimalign.Cohesion.Http;

namespace Assimalign.Cohesion.Viu.Server;

internal sealed class ViuApplicationFeature : IHttpFeature
{
    public ViuApplicationFeature(ViuApplicationConfiguration configuration)
    {
        Configuration = configuration;
    }

    public string Name => "Assimalign.Cohesion.Viu.Server.Application";

    public ViuApplicationConfiguration Configuration { get; }
}
