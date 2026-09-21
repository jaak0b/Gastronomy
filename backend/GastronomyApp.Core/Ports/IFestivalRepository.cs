using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IFestivalRepository
{
  public Task<Festival?> FindRunningAsync(DateTime nowUtc, CancellationToken cancellationToken);

  public Task<IReadOnlyCollection<Festival>> FindAllAsync(CancellationToken cancellationToken);

  public Task<IReadOnlyList<Festival>> FindAllWithContentsAsync(CancellationToken cancellationToken);

  public Task<IReadOnlyDictionary<Guid, int>> CountOrdersByFestivalAsync(CancellationToken cancellationToken);

  public Task<Festival?> FindByIdAsync(Guid festivalId, CancellationToken cancellationToken);

  public Task<bool> ExistsAsync(Guid festivalId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<Guid>> FindIdsNotEndedAsync(DateTime nowUtc, CancellationToken cancellationToken);

  public Task AddAsync(Festival festival, CancellationToken cancellationToken);

  public Task CopyContentsAsync(Guid copiedFromFestivalId, Guid newFestivalId, CancellationToken cancellationToken);

  public Task SaveChangesAsync(CancellationToken cancellationToken);
}
