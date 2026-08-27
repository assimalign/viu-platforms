using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.Http;
using Assimalign.Cohesion.Web;

namespace Assimalign.Viu.Cohesion.DevServer;

internal sealed class DevServerPipeline : IWebApplicationPipeline
{
    private readonly WebApplicationMiddleware _application;

    public DevServerPipeline(WebApplicationMiddleware application)
    {
        _application = application;
    }

    public ValueTask InvokeAsync(IHttpContext context, CancellationToken cancellationToken = default)
    {
        return new ValueTask(_application.Invoke(context));
    }
}
