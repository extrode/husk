using System.Net;
using System.Net.Sockets;

namespace Extrode.Husk.Integration;

public sealed class ListeningTests
{
    [Theory]
    [InlineData("info: Microsoft.Hosting.Lifetime[14]\n      Now listening on: http://127.0.0.1:51234\n", "http://127.0.0.1:51234/")]
    [InlineData("Now listening on: http://[::1]:5000/\r\n", "http://[::1]:5000/")]
    public void The_log_line_gives_the_bound_address(string output, string address) =>
        Assert.Equal(new Uri(address), Listening.Parse(output));

    [Fact]
    public void No_log_line_gives_no_address() =>
        Assert.Null(Listening.Parse("Hosting environment: Development\n"));

    [Fact]
    public void A_static_server_whose_first_port_was_taken_picks_again()
    {
        using var taken = new TcpListener(IPAddress.Loopback, 0);
        taken.Start();
        int busy = ((IPEndPoint)taken.LocalEndpoint).Port;
        using var free = new TcpListener(IPAddress.Loopback, 0);
        free.Start();
        int other = ((IPEndPoint)free.LocalEndpoint).Port;
        free.Stop();
        var ports = new Queue<int>([busy, other]);

        using var server = new StaticServer(Path.GetTempPath(), ports.Dequeue);

        Assert.Equal(other, server.Address.Port);
    }
}
