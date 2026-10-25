using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts.Admin.Ingredients;

public sealed record AdminFestivalIngredientView(Guid IngredientId,
                                                 string Name,
                                                 Guid UnitId,
                                                 string UnitName,
                                                 double? AvailableAmount,
                                                 double UsedAmount,
                                                 IReadOnlyList<IngredientUsagePoint> Usage,
                                                 DateTime? RunsOutAtUtcSinceStart,
                                                 DateTime? RunsOutAtUtcFromLastTwoHours,
                                                 StockWarning StockWarning,
                                                 DateTime? RunsOutSoonAtUtc);
