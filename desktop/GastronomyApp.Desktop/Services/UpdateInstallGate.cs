using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using Serilog;

namespace GastronomyApp.Desktop.Services;

public sealed class UpdateInstallGate : IUpdateInstallGate
{
  private readonly IClock _clock;
  private readonly IFestivalReader _festivals;
  private readonly FestivalSchedule _schedule;

  public UpdateInstallGate(IFestivalReader festivals, FestivalSchedule schedule, IClock clock)
  {
    _festivals = festivals;
    _schedule = schedule;
    _clock = clock;
  }

  public async Task<bool> CanInstallNowAsync(CancellationToken cancellationToken)
  {
    try
    {
      var festivals = await _festivals.ReadAllAsync(cancellationToken);
      var now = _clock.UtcNow;

      if (_schedule.RunningAt(festivals, now) is not null)
      {
        return false;
      }

      return !_schedule.HasStartWithin(festivals, now, TimeSpan.FromHours(24));
    }
    catch (Exception failure)
    {
      Log.Warning(failure, "Reading the festival schedule failed, so the update waits for the next quit.");
      return false;
    }
  }
}
