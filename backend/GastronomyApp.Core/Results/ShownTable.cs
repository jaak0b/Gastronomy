using GastronomyApp.Contracts.TablePlan;

namespace GastronomyApp.Core.Results;

public sealed record ShownTable(Guid TableId, string Name, AreaShapeGeometry Geometry, IReadOnlyList<IReadOnlyList<OrderItemFlags>> ItemFlagsPerOrder);
