using GastronomyApp.Contracts.TablePlan;

namespace GastronomyApp.Core.Results;

public sealed record ShownArea(Guid AreaId, string Name, IReadOnlyList<ShownTable> Tables, IReadOnlyList<TablePlanStationShapeView> StationShapes);
