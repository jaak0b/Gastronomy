using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface ICatalogItemRepository
{
    public Task<CatalogItem?> FindByIdAsync(Guid catalogItemId, CancellationToken cancellationToken);

    public Task<IReadOnlyCollection<ItemStationAssignment>> FindAssignmentsAsync(Guid catalogItemId, CancellationToken cancellationToken);
}
