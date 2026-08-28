using System.Net;
using System.Net.Sockets;

namespace GastronomyApp.Desktop.Services;

public sealed class FreePortProvider : IFreePortProvider
{
  public int Reserve()
  {
    TcpListener probe = new(IPAddress.Loopback, 0);
    probe.Start();

    try
    {
      return ((IPEndPoint)probe.LocalEndpoint).Port;
    } finally
    {
      probe.Stop();
    }
  }
}
