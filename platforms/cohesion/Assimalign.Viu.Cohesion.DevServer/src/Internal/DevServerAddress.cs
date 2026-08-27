using System;
using System.Net;

namespace Assimalign.Viu.Cohesion.DevServer;

internal sealed record DevServerAddress(Uri Url, IPEndPoint EndPoint);
