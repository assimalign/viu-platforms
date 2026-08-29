using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Cohesion.Configuration;

namespace Assimalign.Cohesion.Viu.Hosting;

internal sealed class ViuApplicationConfigurationProvider : ConfigurationProvider
{
    public override string Name => "Assimalign.Cohesion.Viu.Hosting.Configuration";

    protected override Task OnLoadAsync(
        IDictionary<Path, string?> entries,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
