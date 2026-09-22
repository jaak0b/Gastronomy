using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class MappingConfiguration
{
  private readonly IEnumerable<IMappingRegistration> _registrations;

  public MappingConfiguration(IEnumerable<IMappingRegistration> registrations)
  {
    ArgumentNullException.ThrowIfNull(registrations);

    _registrations = registrations;
  }

  public TypeAdapterConfig Build()
  {
    TypeAdapterConfig config = new() { RequireDestinationMemberSource = true };

    foreach (var registration in _registrations)
      registration.Register(config);

    config.Compile();

    return config;
  }
}
