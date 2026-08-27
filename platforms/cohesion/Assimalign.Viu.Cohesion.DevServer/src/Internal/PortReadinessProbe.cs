using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Cohesion.DevServer;

internal static class PortReadinessProbe
{
    public static async Task WaitAsync(
        DevServerAddress address,
        Task serverCompletion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(serverCompletion);

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        while (true)
        {
            timeout.Token.ThrowIfCancellationRequested();

            IPAddress probeAddress = address.EndPoint.Address.Equals(IPAddress.Any)
                ? IPAddress.Loopback
                : address.EndPoint.Address.Equals(IPAddress.IPv6Any)
                    ? IPAddress.IPv6Loopback
                    : address.EndPoint.Address;
            using TcpClient client = new(probeAddress.AddressFamily);
            try
            {
                Task connect = client.ConnectAsync(
                    probeAddress,
                    address.EndPoint.Port,
                    timeout.Token).AsTask();
                Task completed = await Task.WhenAny(connect, serverCompletion).ConfigureAwait(false);
                if (completed == serverCompletion)
                {
                    await serverCompletion.ConfigureAwait(false);
                    throw new InvalidOperationException($"The listener for '{address.Url}' stopped before it became ready.");
                }

                await connect.ConfigureAwait(false);
                return;
            }
            catch (SocketException) when (!timeout.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25), timeout.Token).ConfigureAwait(false);
            }
        }
    }
}
