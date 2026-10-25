using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record ListedArea(Area Area, bool HasItemsToHandOutOrSettleAtTheRunningFestival, IReadOnlyList<ListedTable> Tables, IReadOnlyList<NamedStationShape> StationShapes);
