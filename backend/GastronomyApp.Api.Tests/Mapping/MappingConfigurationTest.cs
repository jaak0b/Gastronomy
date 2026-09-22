using GastronomyApp.Api.Mapping;

namespace GastronomyApp.Api.Tests.Mapping;

[TestFixture]
public sealed class MappingConfigurationTest
{
  [Test]
  public void Build_EveryRegisteredMapping_CompilesWithASourceForEveryDestinationMember()
  {
    Assert.DoesNotThrow(() => new MappingConfiguration(new()).Build());
  }
}
