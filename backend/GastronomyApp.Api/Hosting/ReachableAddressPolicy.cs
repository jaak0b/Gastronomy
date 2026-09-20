using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using GastronomyApp.Api.Values;

namespace GastronomyApp.Api.Hosting;

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

  public IReadOnlyList<LocalNetworkAddress> OrderReachableAddresses(IEnumerable<CandidateNetworkAddress> candidates)
  {
    List<RankedNetworkAddress> reachable = [];

    foreach (var candidate in candidates)
    {
      if (!APhoneCouldReach(candidate))
        continue;

      reachable.Add(new(new(candidate.InterfaceName, candidate.Address.ToString()), RankOfAddress(candidate.Address), RankOfInterface(candidate.InterfaceType), ToNumericValue(candidate.Address)));
    }

    return reachable.OrderBy(ranked => ranked.AddressRank).ThenBy(ranked => ranked.InterfaceRank).ThenBy(ranked => ranked.NumericValue).Select(ranked => ranked.Address).ToList();
  }

  private int RankOfAddress(IPAddress address)
  {
    if (IsPrivateSiteAddress(address))
      return PreferredRank;

    return RemainingRank;
  }

  private int RankOfInterface(NetworkInterfaceType interfaceType)
  {
    if (_interfacesCarryingTheSiteNetwork.Contains(interfaceType))
      return PreferredRank;

    return RemainingRank;
  }

  private bool APhoneCouldReach(CandidateNetworkAddress candidate)
  {
    if (candidate.InterfaceStatus != OperationalStatus.Up || _interfacesNoPhoneCanReach.Contains(candidate.InterfaceType))
      return false;

    if (candidate.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(candidate.Address))
      return false;

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

    return octets[0] == PrivateTenFirstOctet || octets[0] == PrivateOneSevenTwoFirstOctet && octets[1] >= PrivateOneSevenTwoLowestSecondOctet && octets[1] <= PrivateOneSevenTwoHighestSecondOctet || octets[0] == PrivateOneNineTwoFirstOctet && octets[1] == PrivateOneNineTwoSecondOctet;
  }

  private uint ToNumericValue(IPAddress address)
  {
    var octets = address.GetAddressBytes();

    return (uint)octets[0] << 24 | (uint)octets[1] << 16 | (uint)octets[2] << 8 | octets[3];
  }

  private sealed record RankedNetworkAddress(LocalNetworkAddress Address, int AddressRank, int InterfaceRank, uint NumericValue);
}
