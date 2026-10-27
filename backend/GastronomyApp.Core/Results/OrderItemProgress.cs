using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Core.Results;

public sealed record OrderItemProgress(OrderItemState State, bool IsSettled);
