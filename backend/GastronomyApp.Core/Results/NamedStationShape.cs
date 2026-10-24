using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record NamedStationShape(FestivalStationShape Shape, Guid StationId, string StationName);
