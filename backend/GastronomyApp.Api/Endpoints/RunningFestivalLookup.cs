using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;

namespace GastronomyApp.Api.Endpoints;

public sealed class RunningFestivalLookup
{
  private readonly IClock _clock;
  private readonly IFestivalRepository _festivalRepository;

  public RunningFestivalLookup(IFestivalRepository festivalRepository, IClock clock)
  {
    _festivalRepository = festivalRepository;
    _clock = clock;
  }

  public Task<Festival?> FindAsync(CancellationToken cancellationToken)
  {
    return _festivalRepository.FindRunningAsync(_clock.UtcNow, cancellationToken);
  }
}
