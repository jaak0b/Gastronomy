using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Printing;

public sealed class StationCircuitBreaker
{
  private const int ConsecutiveOutcomesThatTrip = 2;

  private int consecutiveUnknownOrTimeout;

  public bool IsTripped { get; private set; }

  public bool RecordOutcome(PrintOutcome outcome, PrintJobStatus jobStatus)
  {
    var countsTowardsTrip = jobStatus == PrintJobStatus.Unknown || outcome == PrintOutcome.Timeout;
    if (!countsTowardsTrip)
    {
      consecutiveUnknownOrTimeout = 0;
      return false;
    }

    consecutiveUnknownOrTimeout++;
    if (consecutiveUnknownOrTimeout < ConsecutiveOutcomesThatTrip || IsTripped)
    {
      return false;
    }

    IsTripped = true;
    return true;
  }

  public void Reset()
  {
    consecutiveUnknownOrTimeout = 0;
    IsTripped = false;
  }
}

public sealed class ReconnectBackoff
{
  private readonly IReadOnlyList<TimeSpan> schedule =
  [
    TimeSpan.FromSeconds(1),
    TimeSpan.FromSeconds(2),
    TimeSpan.FromSeconds(5),
    TimeSpan.FromSeconds(10),
    TimeSpan.FromSeconds(30)
  ];

  private int attempt;

  public TimeSpan Next()
  {
    var delay = schedule[Math.Min(attempt, schedule.Count - 1)];
    attempt++;
    return delay;
  }

  public void Reset()
  {
    attempt = 0;
  }
}
