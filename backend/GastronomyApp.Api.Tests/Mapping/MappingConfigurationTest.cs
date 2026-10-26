using GastronomyApp.Api.Mapping;

namespace GastronomyApp.Api.Tests.Mapping;

[TestFixture]
public sealed class MappingConfigurationTest
{
  [Test]
  public void Build_EveryMappingOfTheCoreAssembly_CompilesWithASourceForEveryDestinationMember()
  {
    Assert.DoesNotThrow(() => new MappingConfiguration().Build());
  }
}
