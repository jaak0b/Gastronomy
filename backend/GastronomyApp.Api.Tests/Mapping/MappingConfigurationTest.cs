using GastronomyApp.Api.Mapping;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Mapping;

[TestFixture]
public sealed class MappingConfigurationTest
{
  [SetUp]
  public void SetUp()
  {
    _dataDirectory = Path.Combine(Path.GetTempPath(), $"gastronomy-mapping-{Guid.NewGuid():N}");
    Directory.CreateDirectory(_dataDirectory);
  }

  [TearDown]
  public void TearDown()
  {
    SqliteConnection.ClearAllPools();

    if (Directory.Exists(_dataDirectory))
      Directory.Delete(_dataDirectory, true);
  }

  private string _dataDirectory = null!;

  [Test]
  public void Build_EveryRegisteredMapping_CompilesWithASourceForEveryDestinationMember()
  {
    IEnumerable<IMappingRegistration> registrations = BuildServices().GetServices<IMappingRegistration>();

    Assert.DoesNotThrow(() => new MappingConfiguration(registrations).Build());
  }

  [Test]
  public void Register_EveryMappingInTheApiAssembly_IsHandedToTheMappingConfiguration()
  {
    IEnumerable<Type> registered = BuildServices().GetServices<IMappingRegistration>().Select(registration => registration.GetType());

    IEnumerable<Type> all = typeof(MappingConfiguration).Assembly.GetTypes().Where(type => type is { IsAbstract: false, IsInterface: false } && typeof(IMappingRegistration).IsAssignableFrom(type));

    Assert.That(registered, Is.EquivalentTo(all));
  }

  private ServiceProvider BuildServices()
  {
    ServiceCollection services = new();

    new ApiServiceRegistration().Register(services,
                                          new()
                                          {
                                            DataDirectory = _dataDirectory,
                                            Port = 0,
                                            BindAddress = "127.0.0.1"
                                          });

    return services.BuildServiceProvider();
  }
}
