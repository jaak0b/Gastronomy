using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Services;

public sealed record QueuedWork(ProductionStatus Status, int? ProductionMinutes);

public sealed class ProductionEstimateCalculator
{
  public int QueuedMinutesOf(IEnumerable<QueuedWork> work)
  {
    ArgumentNullException.ThrowIfNull(work);

    return work
          .Where(item => item.Status != ProductionStatus.Finished)
          .Sum(item => item.ProductionMinutes ?? 0);
  }
}
