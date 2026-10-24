namespace GastronomyApp.Contracts.Admin.Festivals;

public sealed record AdminAreaView(Guid AreaId, string Name, bool IsHidden, bool HasItemsToHandOutOrSettle, IReadOnlyList<AdminTableView> Tables, IReadOnlyList<AdminStationShapeView> StationShapes);
