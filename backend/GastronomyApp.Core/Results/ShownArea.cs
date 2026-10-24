using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record ShownArea(Area Area, IReadOnlyList<ShownTable> Tables, IReadOnlyList<ShownStationShape> StationShapes);
