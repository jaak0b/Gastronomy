using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts.TablePlan;

public sealed record TablePlanTableView(Guid TableId, string Name, AreaShapeGeometry Geometry, ShareState HandOutProgress, ShareState SettlementProgress);
