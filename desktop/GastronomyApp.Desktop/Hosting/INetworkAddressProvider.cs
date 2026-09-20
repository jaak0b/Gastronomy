namespace GastronomyApp.Desktop.Hosting;

public interface INetworkAddressProvider
{
  public IReadOnlyList<NetworkAddressOption> GetAvailableAddresses();
}
