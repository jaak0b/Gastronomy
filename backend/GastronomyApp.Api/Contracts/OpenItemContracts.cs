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

public sealed record SettleItemsRequest
{
  public required IReadOnlyList<Guid>? OrderItemIds { get; init; }
}

public sealed record SettleItemsFreeOfChargeRequest
{
  public required IReadOnlyList<Guid>? OrderItemIds { get; init; }

  public required string? PaymentNotice { get; init; }
}

public sealed record SettlementView(
  IReadOnlyList<Guid> SettledOrderItemIds,
  IReadOnlyList<Guid> AlreadySettledOrderItemIds,
  bool OtherPhonesWereTold);
