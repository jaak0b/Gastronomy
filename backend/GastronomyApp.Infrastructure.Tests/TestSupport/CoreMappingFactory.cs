using GastronomyApp.Core.Ports;
using Mapster;

namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed class CoreMappingFactory
{
  public TypeAdapterConfig Create()
  {
    TypeAdapterConfig config = new() { RequireDestinationMemberSource = true };

    config.Scan(typeof(IGastronomyDatabase).Assembly);
    config.Compile();

    return config;
  }
}
