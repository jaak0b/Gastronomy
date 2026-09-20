using GastronomyApp.Api.Hub;

namespace GastronomyApp.Api.Tests.Hub;

[TestFixture]
public sealed class HubConnectionRegistryTest
{
  [Test]
  public void Add_NullConnection_ThrowsArgumentNullException()
  {
    HubConnectionRegistry registry = new();

    Assert.That(() => registry.Add(null!), Throws.ArgumentNullException);
  }
}
