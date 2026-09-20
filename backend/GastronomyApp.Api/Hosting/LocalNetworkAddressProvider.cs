using System.Net.NetworkInformation;

namespace GastronomyApp.Api.Hosting;

public sealed class LocalNetworkAddressProvider
{
  private readonly ReachableAddressPolicy _policy = new();

  public IReadOnlyList<LocalNetworkAddress> FindReachableAddresses()
  {
    return _policy.OrderReachableAddresses(EnumerateReportedAddresses());
  }

  private IEnumerable<CandidateNetworkAddress> EnumerateReportedAddresses()
  {
    foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
    foreach (var unicast in networkInterface.GetIPProperties().UnicastAddresses)
      yield return new(networkInterface.Name, networkInterface.NetworkInterfaceType, networkInterface.OperationalStatus, unicast.Address);
  }
}
