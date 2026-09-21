using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface ICatalogRepository
{
  public Task<Festival?> FindWithMenuAsync(Guid festivalId, IReadOnlyCollection<Guid> orderableItemIds, CancellationToken cancellationToken);
}
