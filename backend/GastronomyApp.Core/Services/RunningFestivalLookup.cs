using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;

namespace GastronomyApp.Core.Services;

public sealed class RunningFestivalLookup
{
  private readonly IClock _clock;
  private readonly IFestivalRepository _festivalRepository;
  private readonly FestivalSchedule _schedule;

  public RunningFestivalLookup(IFestivalRepository festivalRepository, FestivalSchedule schedule, IClock clock)
  {
    _festivalRepository = festivalRepository;
    _schedule = schedule;
    _clock = clock;
  }

  public Task<Festival?> FindAsync(CancellationToken cancellationToken)
  {
    return _festivalRepository.FindRunningAsync(_clock.UtcNow, cancellationToken);
  }

  public bool IsRunning(Festival festival)
  {
    ArgumentNullException.ThrowIfNull(festival);

    return _schedule.IsRunning(festival, _clock.UtcNow);
  }
}
