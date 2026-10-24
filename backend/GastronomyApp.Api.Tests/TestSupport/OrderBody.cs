namespace GastronomyApp.Api.Tests.TestSupport;

public sealed record OrderBody(Guid ClientOrderId, Guid TableId, IReadOnlyList<OrderItemBody> Items);
