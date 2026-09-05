using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using GastronomyApp.Api.Options;

namespace GastronomyApp.Api.Hosting;

public sealed record LocalNetworkAddress(string InterfaceName, string IPAddress);

public sealed record CandidateNetworkAddress(
  string InterfaceName,
  NetworkInterfaceType InterfaceType,
  OperationalStatus InterfaceStatus,
  IPAddress Address);

public sealed class ReachableAddressPolicy
{
  private const int LinkLocalFirstOctet = 169;
  private const int LinkLocalSecondOctet = 254;
  private const int PrivateTenFirstOctet = 10;
  private const int PrivateOneSevenTwoFirstOctet = 172;
  private const int PrivateOneSevenTwoLowestSecondOctet = 16;
  private const int PrivateOneSevenTwoHighestSecondOctet = 31;
  private const int PrivateOneNineTwoFirstOctet = 192;
  private const int PrivateOneNineTwoSecondOctet = 168;
  private const int PreferredRank = 0;
  private const int RemainingRank = 1;

  private readonly HashSet<NetworkInterfaceType> _interfacesCarryingTheSiteNetwork =
  [
    NetworkInterfaceType.Ethernet,
    NetworkInterfaceType.Ethernet3Megabit,
    NetworkInterfaceType.FastEthernetT,
    NetworkInterfaceType.FastEthernetFx,
    NetworkInterfaceType.GigabitEthernet,
    NetworkInterfaceType.Wireless80211
  ];

  private readonly HashSet<NetworkInterfaceType> _interfacesNoPhoneCanReach =
  [
    NetworkInterfaceType.Loopback,
    NetworkInterfaceType.Tunnel
  ];

  public IReadOnlyList<LocalNetworkAddress> InTheOrderAPhoneShouldTry(IEnumerable<CandidateNetworkAddress> candidates)
  {
    List<RankedNetworkAddress> reachable = [];

    foreach (var candidate in candidates)
    {
      if (!APhoneCouldReach(candidate))
      {
        continue;
      }

      reachable.Add(new(new(candidate.InterfaceName, candidate.Address.ToString()),
                        IsPrivateSiteAddress(candidate.Address) ? PreferredRank : RemainingRank,
                        _interfacesCarryingTheSiteNetwork.Contains(candidate.InterfaceType)
                          ? PreferredRank
                          : RemainingRank,
                        NumericValueOf(candidate.Address)));
    }

    return
    [
      .. reachable.OrderBy(ranked => ranked.AddressRank)
                  .ThenBy(ranked => ranked.InterfaceRank)
                  .ThenBy(ranked => ranked.NumericValue)
                  .Select(ranked => ranked.Address)
    ];
  }

  private bool APhoneCouldReach(CandidateNetworkAddress candidate)
  {
    if (candidate.InterfaceStatus != OperationalStatus.Up
        || _interfacesNoPhoneCanReach.Contains(candidate.InterfaceType))
    {
      return false;
    }

    if (candidate.Address.AddressFamily != AddressFamily.InterNetwork
        || IPAddress.IsLoopback(candidate.Address))
    {
      return false;
    }

    return !IsLinkLocal(candidate.Address);
  }

  private bool IsLinkLocal(IPAddress address)
  {
    var octets = address.GetAddressBytes();

    return octets[0] == LinkLocalFirstOctet && octets[1] == LinkLocalSecondOctet;
  }

  private bool IsPrivateSiteAddress(IPAddress address)
  {
    var octets = address.GetAddressBytes();

    return octets[0] == PrivateTenFirstOctet
           || (octets[0] == PrivateOneSevenTwoFirstOctet
               && octets[1] >= PrivateOneSevenTwoLowestSecondOctet
               && octets[1] <= PrivateOneSevenTwoHighestSecondOctet)
           || (octets[0] == PrivateOneNineTwoFirstOctet && octets[1] == PrivateOneNineTwoSecondOctet);
  }

  private uint NumericValueOf(IPAddress address)
  {
    var octets = address.GetAddressBytes();

    return ((uint)octets[0] << 24) | ((uint)octets[1] << 16) | ((uint)octets[2] << 8) | octets[3];
  }

  private sealed record RankedNetworkAddress(
    LocalNetworkAddress Address,
    int AddressRank,
    int InterfaceRank,
    uint NumericValue);
}

public sealed class LocalNetworkAddressProvider
{
  private readonly ReachableAddressPolicy _policy = new();

  public IReadOnlyList<LocalNetworkAddress> FindReachableAddresses()
  {
    return _policy.InTheOrderAPhoneShouldTry(EveryAddressTheOperatingSystemReports());
  }

  private IEnumerable<CandidateNetworkAddress> EveryAddressTheOperatingSystemReports()
  {
    foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
    {
      foreach (var unicast in networkInterface.GetIPProperties().UnicastAddresses)
      {
        yield return new(networkInterface.Name,
                         networkInterface.NetworkInterfaceType,
                         networkInterface.OperationalStatus,
                         unicast.Address);
      }
    }
  }
}

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

    return string.IsNullOrWhiteSpace(bindAddress)
           || bindAddress is "0.0.0.0" or "::" or "*" or "+";
  }

  public string ResolveHost()
  {
    if (!BindsEveryAddress())
    {
      return _hostOptions.BindAddress;
    }

    IReadOnlyList<string> addresses = ReachableAddresses();

    return addresses.Count == 0 ? LoopbackHost : addresses[0];
  }

  public IReadOnlyList<string> ReachableAddresses()
  {
    return [.. _addressProvider.FindReachableAddresses().Select(address => address.IPAddress)];
  }
}
