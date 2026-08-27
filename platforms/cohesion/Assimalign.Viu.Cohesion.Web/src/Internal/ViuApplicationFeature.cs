using Assimalign.Cohesion.Http;

namespace Assimalign.Viu.Cohesion.Web.Internal;

internal sealed class ViuApplicationFeature : IHttpFeature
{
    public ViuApplicationFeature(ViuApplicationConfiguration configuration)
    {
        Configuration = configuration;
    }

    public string Name => "Assimalign.Viu.Cohesion.Web.Application";

    public ViuApplicationConfiguration Configuration { get; }
}
