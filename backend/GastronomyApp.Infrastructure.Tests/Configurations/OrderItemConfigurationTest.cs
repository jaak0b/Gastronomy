using GastronomyApp.Infrastructure.Configurations;

namespace GastronomyApp.Infrastructure.Tests.Configurations;

public sealed class OrderItemConfigurationTest
{
  [Test]
  public void Configure_NullBuilder_ThrowsArgumentNullException()
  {
    OrderItemConfiguration configuration = new();

    Assert.That(() => configuration.Configure(null!), Throws.ArgumentNullException);
  }
}
