using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface ICatalogItemRepository
{
  public Task<CatalogItem?> FindByIdAsync(Guid catalogItemId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<CatalogItem>> FindAllOrderedAsync(CancellationToken cancellationToken);

  public Task<IReadOnlyCollection<ItemStationAssignment>> FindAssignmentsAsync(Guid festivalId, Guid catalogItemId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<ItemStationAssignment>> FindAssignmentsAtFestivalAsync(Guid festivalId, CancellationToken cancellationToken);

  public Task<FestivalCatalogItem?> FindMenuRowAsync(Guid festivalId, Guid catalogItemId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<FestivalCatalogItem>> FindMenuRowsAtFestivalAsync(Guid festivalId, CancellationToken cancellationToken);

  public Task<bool> IsNameTakenAsync(string name, Guid? itemBeingSaved, CancellationToken cancellationToken);

  public Task AddAsync(CatalogItem item, CancellationToken cancellationToken);

  public Task SaveChangesAsync(CancellationToken cancellationToken);
}
