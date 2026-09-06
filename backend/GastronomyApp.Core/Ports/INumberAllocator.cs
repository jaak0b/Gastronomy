namespace GastronomyApp.Core.Ports;

public interface INumberAllocator
{
  public Task<int> AllocateGlobalOrderNumberAsync(CancellationToken cancellationToken);

  public Task<int> AllocateStationOrderNumberAsync(Guid stationId, CancellationToken cancellationToken);

  public Task ResetOrderAndStationNumbersAsync(CancellationToken cancellationToken);
}
