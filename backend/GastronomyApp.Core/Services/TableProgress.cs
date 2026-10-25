using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class TableProgress
{
  private readonly OrderService _orderService;

  public TableProgress(OrderService orderService)
  {
    _orderService = orderService;
  }

  public ShareState HandOutProgressOf(ShownTable table)
  {
    ArgumentNullException.ThrowIfNull(table);

    return ProgressOfUnfinishedOrders(table.ItemFlagsPerOrder.Select(items => _orderService.ShareOfFlaggedItems(items.Select(item => item.IsHandedOut))));
  }

  public ShareState SettlementProgressOf(ShownTable table)
  {
    ArgumentNullException.ThrowIfNull(table);

    return ProgressOfUnfinishedOrders(table.ItemFlagsPerOrder.Select(items => _orderService.ShareOfFlaggedItems(items.Select(item => item.IsSettled))));
  }

  private ShareState ProgressOfUnfinishedOrders(IEnumerable<ShareState> shareOfEachOrder)
  {
    List<ShareState> sharesOfUnfinishedOrders = shareOfEachOrder.Where(share => share != ShareState.All).ToList();

    if (sharesOfUnfinishedOrders.Count == 0)
      return ShareState.All;

    if (sharesOfUnfinishedOrders.Contains(ShareState.Some))
      return ShareState.Some;

    return ShareState.None;
  }
}
