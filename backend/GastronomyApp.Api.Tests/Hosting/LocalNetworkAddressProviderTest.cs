using GastronomyApp.Api.Hosting;
using GastronomyApp.Api.Values;

namespace GastronomyApp.Api.Tests.Hosting;

[TestFixture]
public sealed class LocalNetworkAddressProviderTest
{
  private readonly LocalNetworkAddressProvider _addressProvider = new();

  [Test]
  public void Addresses_OnThisMachine_AreVersionFourAddressesAPhoneCouldReach()
  {
    IReadOnlyList<LocalNetworkAddress> addresses = _addressProvider.FindReachableAddresses();

    Assert.That(addresses, Is.Not.Null);

    foreach (var address in addresses)
      Assert.Multiple(() =>
                      {
                        Assert.That(address.IPAddress, Does.Not.StartWith("127."));
                        Assert.That(address.IPAddress, Does.Not.StartWith("169.254."));
                        Assert.That(address.IPAddress.Split('.'), Has.Length.EqualTo(4));
                        Assert.That(address.InterfaceName, Is.Not.Empty);
                      });
  }

  [Test]
  public void Addresses_OnThisMachineAskedTwice_ComeBackInTheSameOrder()
  {
    IReadOnlyList<LocalNetworkAddress> first = _addressProvider.FindReachableAddresses();
    IReadOnlyList<LocalNetworkAddress> second = _addressProvider.FindReachableAddresses();

    Assert.That(second, Is.EqualTo(first));
  }
}
