using GastronomyApp.Infrastructure.Projections;
using Mapster;

namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed class ProjectionConfiguration
{
  public TypeAdapterConfig Build()
  {
    TypeAdapterConfig config = new() { RequireDestinationMemberSource = true };

    config.Scan(typeof(CatalogProjection).Assembly);
    config.Compile();

    return config;
  }
}
