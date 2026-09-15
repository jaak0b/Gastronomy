namespace GastronomyApp.Core.Services;

public sealed record QueuedWork(double? ProductionMinutes, bool IsQueueIndependent);

public sealed class ProductionEstimateCalculator
{
  public double QueuedMinutesOf(IEnumerable<QueuedWork> work)
  {
    ArgumentNullException.ThrowIfNull(work);

    var queuedMinutes = work.Where(item => !item.IsQueueIndependent)
                            .Sum(item => item.ProductionMinutes ?? 0);

    return Math.Round(queuedMinutes, 1);
  }
}
