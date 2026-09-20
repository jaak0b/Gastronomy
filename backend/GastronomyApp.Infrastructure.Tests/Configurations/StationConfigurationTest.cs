using GastronomyApp.Infrastructure.Configurations;

namespace GastronomyApp.Infrastructure.Tests.Configurations;

public sealed class StationConfigurationTest
{
  [Test]
  public void Configure_NullBuilder_ThrowsArgumentNullException()
  {
    StationConfiguration configuration = new();

    Assert.That(() => configuration.Configure(null!), Throws.ArgumentNullException);
  }
}
