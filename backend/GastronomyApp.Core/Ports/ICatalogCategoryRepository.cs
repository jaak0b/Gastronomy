using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface ICatalogCategoryRepository
{
  public Task<CatalogCategory?> FindByIdAsync(Guid categoryId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<CatalogCategory>> FindAllOrderedAsync(CancellationToken cancellationToken);

  public Task<bool> HoldsActiveItemsAsync(Guid categoryId, CancellationToken cancellationToken);

  public Task AddAsync(CatalogCategory category, CancellationToken cancellationToken);

  public Task SaveChangesAsync(CancellationToken cancellationToken);
}
