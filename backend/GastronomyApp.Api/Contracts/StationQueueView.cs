namespace GastronomyApp.Api.Contracts;

public sealed record StationQueueView(StationSummaryView Station, IReadOnlyList<StationOrderQueueView> Orders, IReadOnlyList<StationOrderQueueView> AsItComes);
