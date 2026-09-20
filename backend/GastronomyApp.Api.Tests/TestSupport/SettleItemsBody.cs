namespace GastronomyApp.Api.Tests.TestSupport;

public sealed record SettleItemsBody(IReadOnlyList<SettleLineBody> Lines);
