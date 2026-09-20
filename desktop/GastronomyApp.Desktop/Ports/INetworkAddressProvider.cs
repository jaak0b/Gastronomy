using GastronomyApp.Desktop.Values;

namespace GastronomyApp.Desktop.Ports;

public interface INetworkAddressProvider
{
  public IReadOnlyList<NetworkAddressOption> GetAvailableAddresses();
}
