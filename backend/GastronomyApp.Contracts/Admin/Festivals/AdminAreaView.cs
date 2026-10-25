namespace GastronomyApp.Contracts.Admin.Festivals;

public sealed record AdminAreaView(Guid AreaId, string Name, bool IsHidden, bool HasItemsToHandOutOrSettleAtTheRunningFestival, IReadOnlyList<AdminTableView> Tables, IReadOnlyList<AdminStationShapeView> StationShapes);
