namespace GastronomyApp.Desktop.Services;

public interface INetworkAddressProvider
{
  public IReadOnlyList<NetworkAddressOption> GetAvailableAddresses();
}
