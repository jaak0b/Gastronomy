using GastronomyApp.Api.Values;

namespace GastronomyApp.Api.Hosting;

public sealed class ReachableHostResolver
{
  private const string LoopbackHost = "127.0.0.1";
  private readonly LocalNetworkAddressProvider _addressProvider;

  private readonly ApiHostOptions _hostOptions;

  public ReachableHostResolver(ApiHostOptions hostOptions, LocalNetworkAddressProvider addressProvider)
  {
    _hostOptions = hostOptions;
    _addressProvider = addressProvider;
  }

  public bool BindsEveryAddress()
  {
    var bindAddress = _hostOptions.BindAddress;

    return string.IsNullOrWhiteSpace(bindAddress) || bindAddress is "0.0.0.0" or "::" or "*" or "+";
  }

  public string ResolveHost()
  {
    if (!BindsEveryAddress())
      return _hostOptions.BindAddress;

    IReadOnlyList<string> addresses = ReachableAddresses();

    if (addresses.Count == 0)
      return LoopbackHost;

    return addresses[0];
  }

  public IReadOnlyList<string> ReachableAddresses()
  {
    return _addressProvider.FindReachableAddresses().Select(address => address.IPAddress).ToList();
  }
}
