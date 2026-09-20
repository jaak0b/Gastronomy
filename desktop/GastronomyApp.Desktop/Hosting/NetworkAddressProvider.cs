using GastronomyApp.Api.Hosting;
using GastronomyApp.Desktop.Values;
using GastronomyApp.Desktop.Ports;

namespace GastronomyApp.Desktop.Hosting;

public sealed class NetworkAddressProvider : INetworkAddressProvider
{
  private readonly LocalNetworkAddressProvider _reachableAddresses = new();

  public IReadOnlyList<NetworkAddressOption> GetAvailableAddresses()
  {
    return _reachableAddresses.FindReachableAddresses().Select(address => new NetworkAddressOption(address.InterfaceName, address.IPAddress)).ToList();
  }
}
