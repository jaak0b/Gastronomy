namespace GastronomyApp.Api.Tests.Endpoints;

public sealed record OrderWithDeliveryModesBody(Guid ClientOrderId, string TableName, string? Note, IReadOnlyList<OrderItemBody> Items, IReadOnlyList<DeliveryModeBody> DeliveryModes);
