using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record IngredientStockLevel(FestivalIngredient Stock, double UsedAmount, IReadOnlyList<IngredientUsagePointResult> Usage, DateTime? RunsOutAtUtcSinceStart, DateTime? RunsOutAtUtcFromLastTwoHours, StockWarning StockWarning, DateTime? RunsOutSoonAtUtc);
