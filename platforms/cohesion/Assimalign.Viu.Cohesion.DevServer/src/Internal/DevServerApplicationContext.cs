using System.Collections.Generic;

using Assimalign.Cohesion.Http;
using Assimalign.Cohesion.Web;

namespace Assimalign.Viu.Cohesion.DevServer;

internal sealed class DevServerApplicationContext : IWebApplicationContext
{
    public System.IO.FileSystemPath? ContentRootPath => null;

    public IEnumerable<IWebApplicationMiddleware> Middleware => [];

    public IEnumerable<IWebApplicationServer> Servers => [];

    public IEnumerable<IHttpFeature> Features => [];
}
