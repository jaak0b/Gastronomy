namespace GastronomyApp.Core.Services;

public sealed record QueuedWork(int? ProductionMinutes);

public sealed class ProductionEstimateCalculator
{
  public int QueuedMinutesOf(IEnumerable<QueuedWork> work)
  {
    ArgumentNullException.ThrowIfNull(work);

    return work.Sum(item => item.ProductionMinutes ?? 0);
  }
}
