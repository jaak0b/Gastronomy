namespace GastronomyApp.Api.Tests.TestSupport;

public sealed record OrderBody(Guid ClientOrderId, string TableName, IReadOnlyList<OrderItemBody> Items);
