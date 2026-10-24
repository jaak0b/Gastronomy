using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record ListedArea(Area Area, bool HasItemsToHandOutOrSettle, IReadOnlyList<ListedTable> Tables, IReadOnlyList<NamedStationShape> StationShapes);
