namespace GastronomyApp.Api.Tests.TestSupport;

public sealed record OrderWithDeliveryModesBody(Guid ClientOrderId, Guid TableId, IReadOnlyList<OrderItemBody> Items, IReadOnlyList<DeliveryModeBody> DeliveryModes);
