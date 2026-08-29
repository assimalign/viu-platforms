using System;
using System.Net;

namespace Assimalign.Cohesion.Viu.DevServer;

internal sealed record DevServerAddress(Uri Url, IPEndPoint EndPoint);
