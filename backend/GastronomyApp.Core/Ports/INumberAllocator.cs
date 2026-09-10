namespace GastronomyApp.Core.Ports;

public interface INumberAllocator
{
  public Task<int> AllocateGlobalOrderNumberAsync(Guid festivalId, CancellationToken cancellationToken);

  public Task<int> AllocateStationOrderNumberAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken);
}
