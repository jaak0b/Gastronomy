using GastronomyApp.Core.Ports;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class MappingConfiguration
{
  public TypeAdapterConfig Build()
  {
    TypeAdapterConfig config = new() { RequireDestinationMemberSource = true };

    config.Scan(typeof(IGastronomyDatabase).Assembly);
    config.Compile();

    return config;
  }
}
