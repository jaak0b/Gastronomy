using GastronomyApp.Api.Hosting;

namespace GastronomyApp.Desktop.Services;

public sealed class NetworkAddressProvider : INetworkAddressProvider
{
  private readonly LocalNetworkAddressProvider _reachableAddresses = new();

  public IReadOnlyList<NetworkAddressOption> GetAvailableAddresses()
  {
    return
    [
      .. _reachableAddresses.FindReachableAddresses()
                            .Select(address => new NetworkAddressOption(address.InterfaceName, address.IPAddress))
    ];
  }
}
