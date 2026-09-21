using GastronomyApp.Infrastructure.Projections;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class MappingConfiguration
{
  public TypeAdapterConfig Build()
  {
    TypeAdapterConfig config = new() { RequireDestinationMemberSource = true };

    config.Scan(typeof(MappingConfiguration).Assembly, typeof(DeviceOwnerProjection).Assembly);
    config.Compile();

    return config;
  }
}
