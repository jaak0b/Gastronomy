namespace GastronomyApp.Api.Tests.Endpoints;

public sealed record SettleItemsBody(IReadOnlyList<SettleLineBody> Lines);
