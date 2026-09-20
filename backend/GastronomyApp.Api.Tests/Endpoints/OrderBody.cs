namespace GastronomyApp.Api.Tests.Endpoints;

public sealed record OrderBody(
  Guid ClientOrderId,
  string TableName,
  string? Note,
  IReadOnlyList<OrderItemBody> Items);
