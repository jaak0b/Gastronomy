using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record ShownStationShape(FestivalStationShape Shape, Guid StationId, string StationName);
