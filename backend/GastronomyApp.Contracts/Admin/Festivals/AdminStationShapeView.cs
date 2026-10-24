using GastronomyApp.Contracts.TablePlan;

namespace GastronomyApp.Contracts.Admin.Festivals;

public sealed record AdminStationShapeView(Guid StationId, string StationName, AreaShapeGeometry Geometry);
