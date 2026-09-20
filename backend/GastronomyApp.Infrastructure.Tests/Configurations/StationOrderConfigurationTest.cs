using GastronomyApp.Infrastructure.Configurations;

namespace GastronomyApp.Infrastructure.Tests.Configurations;

public sealed class StationOrderConfigurationTest
{
  [Test]
  public void Configure_NullBuilder_ThrowsArgumentNullException()
  {
    StationOrderConfiguration configuration = new();

    Assert.That(() => configuration.Configure(null!), Throws.ArgumentNullException);
  }
}
