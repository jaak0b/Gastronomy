using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;

namespace GastronomyApp.Core.Services;

public sealed class RunningFestivalLookup
{
  private readonly TimeProvider _timeProvider;
  private readonly IFestivalRepository _festivalRepository;
  private readonly FestivalSchedule _schedule;

  public RunningFestivalLookup(IFestivalRepository festivalRepository, FestivalSchedule schedule, TimeProvider timeProvider)
  {
    _festivalRepository = festivalRepository;
    _schedule = schedule;
    _timeProvider = timeProvider;
  }

  public Task<Festival?> FindAsync(CancellationToken cancellationToken)
  {
    return _festivalRepository.FindRunningAsync(_timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
  }

  public bool IsRunning(Festival festival)
  {
    ArgumentNullException.ThrowIfNull(festival);

    return _schedule.IsRunning(festival, _timeProvider.GetUtcNow().UtcDateTime);
  }
}
