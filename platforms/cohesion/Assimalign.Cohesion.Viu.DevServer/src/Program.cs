using System;
using System.Threading.Tasks;

namespace Assimalign.Cohesion.Viu.DevServer;

internal static class Program
{
    public static async Task<int> Main(string[] arguments)
    {
        try
        {
            DevServerOptions options = CommandLineParser.Parse(arguments);
            using ShutdownSignals shutdown = new();
            await DevServerRunner.RunAsync(
                options,
                Console.Out,
                Environment.GetEnvironmentVariable("ASPNETCORE_URLS"),
                shutdown.CancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (CommandLineException exception)
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            if (!exception.Message.Equals(CommandLineParser.Usage, StringComparison.Ordinal))
            {
                await Console.Error.WriteLineAsync(CommandLineParser.Usage).ConfigureAwait(false);
            }

            return 2;
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 1;
        }
    }
}
