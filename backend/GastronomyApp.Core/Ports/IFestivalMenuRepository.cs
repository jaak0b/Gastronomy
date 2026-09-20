using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IFestivalMenuRepository
{
  public Task<FestivalCatalogItem?> FindMenuRowAsync(Guid festivalId, Guid catalogItemId, CancellationToken cancellationToken);

  public Task<bool> CatalogItemExistsAsync(Guid catalogItemId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<Guid>> FindStationIdsAtFestivalAsync(Guid festivalId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<ItemStationAssignment>> FindAssignmentsAsync(Guid festivalId, Guid catalogItemId, CancellationToken cancellationToken);

  public Task AddMenuRowAsync(FestivalCatalogItem menuRow, CancellationToken cancellationToken);

  public void RemoveMenuRow(FestivalCatalogItem menuRow);

  public Task AddAssignmentAsync(ItemStationAssignment assignment, CancellationToken cancellationToken);

  public void RemoveAssignments(IReadOnlyCollection<ItemStationAssignment> assignments);

  public Task SaveChangesAsync(CancellationToken cancellationToken);
}
