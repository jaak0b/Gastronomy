using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record ShownTable(Table Table, IReadOnlyList<IReadOnlyList<OrderItemFlags>> ItemFlagsPerOrder);
