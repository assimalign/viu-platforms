using Assimalign.Cohesion.Viu.Hosting;
using Assimalign.Viu;

namespace Assimalign.Cohesion.Viu.Server;

internal sealed class ServerViuApplicationHostFactory : IViuApplicationHostFactory
{
    public static ServerViuApplicationHostFactory Instance { get; } = new();

    private ServerViuApplicationHostFactory()
    {
    }

    public IApplication CreateApplication(ApplicationOptions applicationOptions)
    {
        return new ServerViuApplication(new ApplicationContext(applicationOptions));
    }
}
