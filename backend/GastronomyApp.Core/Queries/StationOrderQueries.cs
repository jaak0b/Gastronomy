using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Queries;

public static class StationOrderQueries
{
  public static IQueryable<StationOrder> WhereAnItemIsOpenOrAwaitsCancellationAcknowledgement(this IQueryable<StationOrder> stationOrders)
  {
    return stationOrders.Where(stationOrder => stationOrder.Items.Any(item => item.IsOpenOrAwaitsCancellationAcknowledgement()));
  }

  public static IQueryable<StationOrder> WhereListedAsDone(this IQueryable<StationOrder> stationOrders)
  {
    return stationOrders.Where(stationOrder => stationOrder.Items.Any(item => item.IsHandedOut()) || !stationOrder.Items.Any(item => item.IsOpenOrAwaitsCancellationAcknowledgement()));
  }
}
