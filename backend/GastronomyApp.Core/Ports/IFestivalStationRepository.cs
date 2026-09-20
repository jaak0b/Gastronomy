using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IFestivalStationRepository
{
  public Task<FestivalStation?> FindLinkAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<ItemStationAssignment>> FindAssignmentsAtStationAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken);

  public Task<int> CountUnfulfilledItemsAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken);

  public Task AddLinkAsync(FestivalStation link, CancellationToken cancellationToken);

  public void RemoveLink(FestivalStation link);

  public void RemoveAssignments(IReadOnlyCollection<ItemStationAssignment> assignments);

  public Task SaveChangesAsync(CancellationToken cancellationToken);
}
