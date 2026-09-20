using System.Net;
using System.Net.NetworkInformation;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Api.Values;

namespace GastronomyApp.Api.Tests.Hosting;

[TestFixture]
public sealed class ReachableAddressPolicyTest
{
  private readonly ReachableAddressPolicy _policy = new();

  private CandidateNetworkAddress On(string interfaceName, NetworkInterfaceType interfaceType, string address, OperationalStatus status = OperationalStatus.Up)
  {
    return new(interfaceName, interfaceType, status, IPAddress.Parse(address));
  }

  private IReadOnlyList<string> OrderAddresses(params CandidateNetworkAddress[] candidates)
  {
    return _policy.OrderReachableAddresses(candidates).Select(found => found.IPAddress).ToList();
  }

  private CandidateNetworkAddress[] BuildTheLaptopAtTheDemo()
  {
    return
    [
      On("Local Area Connection* 2", NetworkInterfaceType.Wireless80211, "169.254.120.128"),
      On("Local Area Connection* 1", NetworkInterfaceType.Wireless80211, "169.254.102.208"),
      On("Cellular", NetworkInterfaceType.Wwanpp, "169.254.158.77"),
      On("Wi-Fi", NetworkInterfaceType.Wireless80211, "10.0.0.12")
    ];
  }

  [Test]
  public void Order_TheLaptopAtTheDemo_YieldsOnlyTheAddressAPhoneCanReach()
  {
    Assert.That(OrderAddresses(BuildTheLaptopAtTheDemo()), Is.EqualTo(new[] { "10.0.0.12" }));
  }

  [Test]
  public void Order_TheLaptopAtTheDemoEnumeratedTheOtherWayRound_YieldsTheSameAddress()
  {
    CandidateNetworkAddress[] reversed = BuildTheLaptopAtTheDemo().Reverse().ToArray();

    Assert.That(OrderAddresses(reversed), Is.EqualTo(OrderAddresses(BuildTheLaptopAtTheDemo())));
  }

  [Test]
  public void Order_LinkLocalAddress_IsLeftOutBecauseNoPhoneCanReachIt()
  {
    Assert.That(OrderAddresses(On("Wi-Fi Direct", NetworkInterfaceType.Wireless80211, "169.254.1.1")), Is.Empty);
  }

  [Test]
  public void Order_LoopbackAddress_IsLeftOut()
  {
    Assert.That(OrderAddresses(On("Loopback", NetworkInterfaceType.Loopback, "127.0.0.1")), Is.Empty);
  }

  [Test]
  public void Order_TunnelInterface_IsLeftOutBecauseThePhonesAreNotInsideIt()
  {
    Assert.That(OrderAddresses(On("Company VPN", NetworkInterfaceType.Tunnel, "10.8.0.6")), Is.Empty);
  }

  [Test]
  public void Order_InterfaceThatIsDown_IsLeftOut()
  {
    Assert.That(OrderAddresses(On("Ethernet", NetworkInterfaceType.Ethernet, "192.168.1.20", OperationalStatus.Down)), Is.Empty);
  }

  [Test]
  public void Order_VersionSixAddress_IsLeftOutBecauseTheUrlCarriesVersionFour()
  {
    Assert.That(OrderAddresses(On("Wi-Fi", NetworkInterfaceType.Wireless80211, "fe80::1")), Is.Empty);
  }

  [Test]
  public void Order_PrivateAndPublicAddress_PutsThePrivateOneFirst()
  {
    IReadOnlyList<string> addresses = OrderAddresses(On("Ethernet", NetworkInterfaceType.Ethernet, "203.0.113.9"), On("Wi-Fi", NetworkInterfaceType.Wireless80211, "192.168.1.20"));

    Assert.That(addresses,
                Is.EqualTo(new[]
                           {
                             "192.168.1.20",
                             "203.0.113.9"
                           }));
  }

  [TestCase("10.0.0.12")]
  [TestCase("172.16.4.5")]
  [TestCase("172.31.4.5")]
  [TestCase("192.168.1.20")]
  public void Order_EveryPrivateRange_CountsAsTheSiteNetwork(string privateAddress)
  {
    IReadOnlyList<string> addresses = OrderAddresses(On("Ethernet", NetworkInterfaceType.Ethernet, "203.0.113.9"), On("Wi-Fi", NetworkInterfaceType.Wireless80211, privateAddress));

    Assert.That(addresses[0], Is.EqualTo(privateAddress));
  }

  [Test]
  public void Order_PrivateAddressOnACellularInterface_ComesAfterTheOneOnWiFi()
  {
    IReadOnlyList<string> addresses = OrderAddresses(On("Cellular", NetworkInterfaceType.Wwanpp, "10.0.0.5"), On("Wi-Fi", NetworkInterfaceType.Wireless80211, "10.0.0.12"));

    Assert.That(addresses,
                Is.EqualTo(new[]
                           {
                             "10.0.0.12",
                             "10.0.0.5"
                           }));
  }

  [Test]
  public void Order_TwoEquallyGoodAddresses_IsTheSameWhicheverWayTheyAreEnumerated()
  {
    var first = On("Ethernet", NetworkInterfaceType.Ethernet, "192.168.1.20");
    var second = On("Wi-Fi", NetworkInterfaceType.Wireless80211, "192.168.1.9");

    Assert.That(OrderAddresses(first, second), Is.EqualTo(OrderAddresses(second, first)));
  }
}
