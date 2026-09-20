using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IFestivalRepository
{
  public Task<Festival?> FindRunningAsync(DateTime nowUtc, CancellationToken cancellationToken);

  public Task<IReadOnlyCollection<Festival>> FindAllAsync(CancellationToken cancellationToken);

  public Task<bool> ExistsAsync(Guid festivalId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<Guid>> FindIdsNotEndedAsync(DateTime nowUtc, CancellationToken cancellationToken);
}
