using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace GastronomyApp.Desktop.Services;

public sealed class NetworkAddressProvider : INetworkAddressProvider
{
  public IReadOnlyList<NetworkAddressOption> GetAvailableAddresses()
  {
    List<NetworkAddressOption> options = [];

    foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
    {
      if (adapter.OperationalStatus != OperationalStatus.Up
          || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
      {
        continue;
      }

      foreach (UnicastIPAddressInformation address in adapter.GetIPProperties().UnicastAddresses)
      {
        if (address.Address.AddressFamily != AddressFamily.InterNetwork)
        {
          continue;
        }

        options.Add(new NetworkAddressOption(adapter.Name, address.Address.ToString()));
      }
    }

    return options;
  }
}
