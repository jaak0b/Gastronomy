using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace GastronomyApp.Desktop.Services;

public sealed class NetworkAddressProvider : INetworkAddressProvider
{
  public IReadOnlyList<NetworkAddressOption> GetAvailableAddresses()
  {
    List<NetworkAddressOption> options = [];

    foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
    {
      if (adapter.OperationalStatus != OperationalStatus.Up
          || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
      {
        continue;
      }

      foreach (var address in adapter.GetIPProperties().UnicastAddresses)
      {
        if (address.Address.AddressFamily != AddressFamily.InterNetwork)
        {
          continue;
        }

        options.Add(new(adapter.Name, address.Address.ToString()));
      }
    }

    return options;
  }
}
