namespace GastronomyApp.Api.Tests.TestSupport;

public sealed record OrderWithDeliveryModesBody(Guid ClientOrderId, string TableName, string? Note, IReadOnlyList<OrderItemBody> Items, IReadOnlyList<DeliveryModeBody> DeliveryModes);
