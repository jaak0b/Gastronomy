using GastronomyApp.Contracts.Stations;

namespace GastronomyApp.Core.Results;

public sealed record StationAtFestival(StationSummaryView Station, Guid FestivalId);
