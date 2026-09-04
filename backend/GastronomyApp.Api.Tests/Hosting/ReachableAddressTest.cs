using GastronomyApp.Api.Hosting;

namespace GastronomyApp.Api.Tests.Hosting;

[TestFixture]
public sealed class ReachableAddressTest
{
  private readonly LocalNetworkAddressProvider _addressProvider = new();

  [TestCase("0.0.0.0")]
  [TestCase("::")]
  [TestCase("*")]
  [TestCase("")]
  public void Resolve_WildcardBindAddress_YieldsARoutableAddressInstead(string bindAddress)
  {
    var resolver = BuildResolver(bindAddress);

    var host = resolver.ResolveHost();

    Assert.Multiple(() =>
                    {
                      Assert.That(host, Is.Not.EqualTo(bindAddress));
                      Assert.That(host, Does.Not.StartWith("0.0.0.0"));
                      Assert.That(host, Is.Not.Empty);
                    });
  }

  [Test]
  public void Resolve_SpecificBindAddress_KeepsIt()
  {
    var resolver = BuildResolver("192.168.1.23");

    Assert.That(resolver.ResolveHost(), Is.EqualTo("192.168.1.23"));
  }

  [Test]
  public void Addresses_OnThisMachine_AreNonLoopbackVersionFourAddresses()
  {
    IReadOnlyList<string> addresses = _addressProvider.FindReachableAddresses();

    Assert.That(addresses, Is.Not.Null);

    foreach (var address in addresses)
    {
      Assert.Multiple(() =>
                      {
                        Assert.That(address, Does.Not.StartWith("127."));
                        Assert.That(address.Split('.'), Has.Length.EqualTo(4));
                      });
    }
  }

  private ReachableHostResolver BuildResolver(string bindAddress)
  {
    return new(new()
               {
                 DataDirectory = Path.GetTempPath(),
                 Port = 5000,
                 BindAddress = bindAddress
               },
               _addressProvider);
  }
}
