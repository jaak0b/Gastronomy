namespace GastronomyApp.Contracts.Admin.Festivals;

public sealed record AdminAreaView(Guid AreaId, string Name, int SortOrder, bool IsHidden, IReadOnlyList<AdminTableView> Tables, IReadOnlyList<AdminStationShapeView> StationShapes);
