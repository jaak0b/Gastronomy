using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class RunningFestivalLookup
{
  private readonly TimeProvider _timeProvider;
  private readonly IGastronomyDatabase _database;
  private readonly FestivalSchedule _schedule;

  public RunningFestivalLookup(IGastronomyDatabase database, FestivalSchedule schedule, TimeProvider timeProvider)
  {
    _database = database;
    _schedule = schedule;
    _timeProvider = timeProvider;
  }

  public async Task<Festival?> FindAsync(CancellationToken cancellationToken)
  {
    var shown = await _database.Festivals.AsNoTracking().Where(festival => !festival.IsHidden).ToListAsync(cancellationToken);

    return _schedule.FindRunningAt(shown, _timeProvider.GetUtcNow().UtcDateTime);
  }

  public bool IsRunning(Festival festival)
  {
    ArgumentNullException.ThrowIfNull(festival);

    return _schedule.IsRunning(festival, _timeProvider.GetUtcNow().UtcDateTime);
  }

  public bool IsRunning(bool isHidden, DateTime startsAtUtc, DateTime endsAtUtc)
  {
    return _schedule.IsRunning(isHidden, startsAtUtc, endsAtUtc, _timeProvider.GetUtcNow().UtcDateTime);
  }
}
