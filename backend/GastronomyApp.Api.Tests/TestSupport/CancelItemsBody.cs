namespace GastronomyApp.Api.Tests.TestSupport;

public sealed record CancelItemsBody(IReadOnlyList<Guid> OrderItemIds);
