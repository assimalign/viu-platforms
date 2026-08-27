using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Cohesion.DevServer.Tests;

public sealed class ReadinessOutputTests
{
    [Fact(DisplayName = "Viu Cohesion DevServer - readiness: writes watch and IDE lines in order")]
    public async Task WriteAsync_BoundUrl_ShouldWriteBothContractLines()
    {
        using StringWriter output = new();

        await DevServerReadiness.WriteAsync(
            output,
            new Uri("http://127.0.0.1:5100/"),
            CancellationToken.None);

        output.ToString().ShouldBe(
            $"Now listening on: http://127.0.0.1:5100/{Environment.NewLine}" +
            $"App url: http://127.0.0.1:5100/{Environment.NewLine}");
    }
}
