namespace GastronomyApp.Contracts.TablePlan;

public sealed record TablePlanAreaView(Guid AreaId, string Name, IReadOnlyList<TablePlanTableView> Tables, IReadOnlyList<TablePlanStationShapeView> StationShapes);
