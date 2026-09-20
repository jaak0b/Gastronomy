namespace GastronomyApp.Api.Tests.TestSupport;

public sealed record OrderWithDeliveryModesBody(Guid ClientOrderId, string TableName, IReadOnlyList<OrderItemBody> Items, IReadOnlyList<DeliveryModeBody> DeliveryModes);
