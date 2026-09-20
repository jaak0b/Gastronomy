using GastronomyApp.Infrastructure.Repositories;
using Mapster;

namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed class ProjectionConfiguration
{
  public TypeAdapterConfig Build()
  {
    TypeAdapterConfig config = new() { RequireDestinationMemberSource = true };

    new CatalogProjection().Register(config);
    config.Compile();

    return config;
  }
}
