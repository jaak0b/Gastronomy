using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface ICatalogCategoryRepository
{
  public Task<CatalogCategory?> FindByIdAsync(Guid categoryId, CancellationToken cancellationToken);
}
