using Mapster;

namespace GastronomyApp.Api.Mapping;

public interface IMappingRegistration
{
  public void Register(TypeAdapterConfig config);
}
