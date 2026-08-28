using System.Net.Sockets;

namespace GastronomyApp.Infrastructure.Tests.Printing;

public class FakeEscPosPrinterServerTest
{
  [Test]
  public async Task StartAsync_AcceptsAConnectionAndRecordsWhatItWasSent()
  {
    await using FakeEscPosPrinterServer server = new(0);
    await server.StartAsync(CancellationToken.None);
    server.ScriptDleEotResponse(4, 0x60);

    using TcpClient client = new();
    await client.ConnectAsync("127.0.0.1", server.Port);
    NetworkStream stream = client.GetStream();
    await stream.WriteAsync(new byte[] { 0x10, 0x04, 0x04 });

    byte[] response = new byte[1];
    int read = await stream.ReadAsync(response);

    Assert.That(read, Is.EqualTo(1));
    Assert.That(response[0], Is.EqualTo(0x60));
    Assert.That(server.ReceivedBytes, Is.EqualTo(new byte[] { 0x10, 0x04, 0x04 }));
    Assert.That(server.AcceptedConnectionCount, Is.EqualTo(1));
  }
}
