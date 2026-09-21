
namespace GastronomyApp.Contracts.Stations;

public sealed record StationQueueView(StationSummaryView Station, IReadOnlyList<StationOrderQueueView> Orders, IReadOnlyList<StationOrderQueueView> AsItComes);
