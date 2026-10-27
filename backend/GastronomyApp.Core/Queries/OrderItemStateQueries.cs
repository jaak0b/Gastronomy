using EntityFrameworkCore.Projectables;
using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Core.Queries;

public static class OrderItemStateQueries
{
  [Projectable]
  public static bool IsOpen(this OrderItemState state) => state == OrderItemState.Open;

  [Projectable]
  public static bool IsHandedOut(this OrderItemState state) => state == OrderItemState.HandedOut;

  [Projectable]
  public static bool IsCancelled(this OrderItemState state) => state == OrderItemState.Cancelled || state == OrderItemState.CancellationAcknowledged;
}
