using GastronomyApp.Infrastructure.Configurations;

namespace GastronomyApp.Infrastructure.Tests.Configurations;

public sealed class DeviceConfigurationTest
{
  [Test]
  public void Configure_NullBuilder_ThrowsArgumentNullException()
  {
    DeviceConfiguration configuration = new();

    Assert.That(() => configuration.Configure(null!), Throws.ArgumentNullException);
  }
}
