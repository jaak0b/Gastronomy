using GastronomyApp.Core.ReadModels;

namespace GastronomyApp.Core.Ports;

public interface ICatalogRepository
{
  public Task<CatalogAtFestival> ReadAtFestivalAsync(Guid festivalId, string festivalName, CancellationToken cancellationToken);
}
