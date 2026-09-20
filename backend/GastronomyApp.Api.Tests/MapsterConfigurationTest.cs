namespace GastronomyApp.Api.Tests;

[TestFixture]
public sealed class MapsterConfigurationTest
{
  [Test]
  public void Build_EveryRegisteredMapping_CompilesWithASourceForEveryDestinationMember()
  {
    Assert.DoesNotThrow(() => new MapsterConfiguration().Build());
  }
}
