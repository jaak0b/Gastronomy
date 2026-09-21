using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Desktop.Ports;
using Serilog;

namespace GastronomyApp.Desktop.Updates;

public sealed class UpdateInstallGate : IUpdateInstallGate
{
  private readonly TimeProvider _timeProvider;
  private readonly IFestivalReader _festivals;
  private readonly FestivalSchedule _schedule;

  public UpdateInstallGate(IFestivalReader festivals, FestivalSchedule schedule, TimeProvider timeProvider)
  {
    _festivals = festivals;
    _schedule = schedule;
    _timeProvider = timeProvider;
  }

  public async Task<bool> CanInstallNowAsync(CancellationToken cancellationToken)
  {
    try
    {
      IReadOnlyCollection<Festival> festivals = await _festivals.ReadAllAsync(cancellationToken);
      var now = _timeProvider.GetUtcNow().UtcDateTime;

      if (_schedule.FindRunningAt(festivals, now) is not null)
        return false;

      return !_schedule.HasStartWithin(festivals, now, TimeSpan.FromHours(24));
    }
    catch (Exception failure)
    {
      Log.Warning(failure, "Reading the festival schedule failed, so the update waits for the next quit.");
      return false;
    }
  }
}
