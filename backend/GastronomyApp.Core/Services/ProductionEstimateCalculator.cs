namespace GastronomyApp.Core.Services;

public sealed record QueuedWork(int? ProductionMinutes, bool IsQueueIndependent);

public sealed class ProductionEstimateCalculator
{
  public int QueuedMinutesOf(IEnumerable<QueuedWork> work)
  {
    ArgumentNullException.ThrowIfNull(work);

    return work.Where(item => !item.IsQueueIndependent)
               .Sum(item => item.ProductionMinutes ?? 0);
  }
}
