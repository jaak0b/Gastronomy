using GastronomyApp.Infrastructure.Projections;
using Mapster;

namespace GastronomyApp.Api;

public sealed class MapsterConfiguration
{
  public TypeAdapterConfig Build()
  {
    TypeAdapterConfig config = new() { RequireDestinationMemberSource = true };

    config.Scan(typeof(MapsterConfiguration).Assembly, typeof(CatalogProjection).Assembly);
    config.Compile();

    return config;
  }
}
