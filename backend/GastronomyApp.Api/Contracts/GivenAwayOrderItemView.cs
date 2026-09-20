namespace GastronomyApp.Api.Contracts;

public sealed record GivenAwayOrderItemView(
  Guid OrderItemId,
  Guid OrderId,
  int GlobalOrderNumber,
  string ItemName,
  int WaivedAmountCents,
  string? PaymentNotice,
  DateTime SettledAtUtc);
