using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Printing;

public sealed class StationCircuitBreaker
{
  private const int ConsecutiveOutcomesThatTrip = 2;

  private int _consecutiveUnknownOrTimeout;

  public bool IsTripped { get; private set; }

  public bool RecordOutcome(PrintOutcome outcome, PrintJobStatus jobStatus)
  {
    var countsTowardsTrip = jobStatus == PrintJobStatus.Unknown || outcome == PrintOutcome.Timeout;
    if (!countsTowardsTrip)
    {
      _consecutiveUnknownOrTimeout = 0;
      return false;
    }

    _consecutiveUnknownOrTimeout++;
    if (_consecutiveUnknownOrTimeout < ConsecutiveOutcomesThatTrip || IsTripped)
    {
      return false;
    }

    IsTripped = true;
    return true;
  }

  public void Reset()
  {
    _consecutiveUnknownOrTimeout = 0;
    IsTripped = false;
  }
}

public sealed class ReconnectBackoff
{
  private readonly IReadOnlyList<TimeSpan> _schedule =
  [
    TimeSpan.FromSeconds(1),
    TimeSpan.FromSeconds(2),
    TimeSpan.FromSeconds(5),
    TimeSpan.FromSeconds(10),
    TimeSpan.FromSeconds(30)
  ];

  private int _attempt;

  public TimeSpan Next()
  {
    var delay = _schedule[Math.Min(_attempt, _schedule.Count - 1)];
    _attempt++;
    return delay;
  }

  public void Reset()
  {
    _attempt = 0;
  }
}
