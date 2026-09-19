namespace GastronomyApp.Api.Contracts;

public sealed record OpenOrderItemView(
  Guid OrderItemId,
  Guid OrderId,
  int GlobalOrderNumber,
  string ItemName,
  string? Note,
  int UnitPriceCents,
  DateTime OrderedAtUtc);

public sealed record GivenAwayOrderItemView(
  Guid OrderItemId,
  Guid OrderId,
  int GlobalOrderNumber,
  string ItemName,
  int WaivedAmountCents,
  string? PaymentNotice,
  DateTime SettledAtUtc);

public sealed record OpenTableView(
  string TableName,
  int OpenAmountCents,
  int GivenAwayAmountCents,
  IReadOnlyList<OpenOrderItemView> Items,
  IReadOnlyList<GivenAwayOrderItemView> GivenAwayItems);

public sealed record OpenItemsView(
  IReadOnlyList<OpenTableView> Tables,
  int ItemsWithoutAnOrderCount);

public sealed record TableNamesView(IReadOnlyList<string> TableNames);

public sealed record SettleLineRequest
{
  public required Guid OrderItemId { get; init; }

  public required int? PaidPriceCents { get; init; }

  public string? PaymentNotice { get; init; }
}

public sealed record SettleItemsRequest
{
  public required IReadOnlyList<SettleLineRequest>? Lines { get; init; }
}

public sealed record SettlementView(
  IReadOnlyList<Guid> SettledOrderItemIds,
  IReadOnlyList<Guid> ReappliedOrderItemIds,
  IReadOnlyList<Guid> AlreadySettledByOthersOrderItemIds,
  bool OtherPhonesWereTold);
