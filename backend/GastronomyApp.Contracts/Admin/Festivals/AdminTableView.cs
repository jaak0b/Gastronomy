using GastronomyApp.Contracts.TablePlan;

namespace GastronomyApp.Contracts.Admin.Festivals;

public sealed record AdminTableView(Guid TableId, string Name, AreaShapeGeometry Geometry, bool IsHidden);
