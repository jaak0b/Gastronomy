using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IStationRepository
{
  public Task<IReadOnlyCollection<Station>> FindActiveAsync(CancellationToken cancellationToken);
}
