namespace GastronomyApp.Core.Ports;

public interface IItemOrderabilityRepository
{
  public Task<IReadOnlyList<Guid>> FindActiveStationIdsAtFestivalAsync(Guid festivalId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<Guid>> FindItemIdsPreparedByAsync(Guid festivalId, IReadOnlyCollection<Guid> stationIds, CancellationToken cancellationToken);

  public Task<IReadOnlyList<Guid>> FindActiveMenuItemIdsAsync(Guid festivalId, CancellationToken cancellationToken);
}
