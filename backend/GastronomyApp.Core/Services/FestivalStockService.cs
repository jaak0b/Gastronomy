using System.Collections.ObjectModel;
using ErrorOr;
using GastronomyApp.Contracts.Admin.Ingredients;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Queries;
using GastronomyApp.Core.Refusals;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class FestivalStockService
{
  private readonly IGastronomyDatabase _database;
  private readonly StockForecast _forecast;
  private readonly TypeAdapterConfig _mapping;
  private readonly StockSoldOutMarker _soldOutMarker;
  private readonly TimeProvider _timeProvider;

  public FestivalStockService(IGastronomyDatabase database, TypeAdapterConfig mapping, StockForecast forecast, StockSoldOutMarker soldOutMarker, TimeProvider timeProvider)
  {
    _database = database;
    _mapping = mapping;
    _forecast = forecast;
    _soldOutMarker = soldOutMarker;
    _timeProvider = timeProvider;
  }

  public async Task<ErrorOr<AdminFestivalIngredientListView>> ListAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    var festival = await _database.Festivals.AsNoTracking().FirstOrDefaultAsync(row => row.Id == festivalId, cancellationToken);

    if (festival is null)
      return Refusal.Ingredients.FestivalNotFound(festivalId);

    var rows = await _database.FestivalIngredients.AsNoTracking()
                              .Where(stock => stock.FestivalId == festivalId
                                              && stock.Ingredient.IsActive
                                              && _database.CatalogItemIngredients.Any(recipeLine => recipeLine.IngredientId == stock.IngredientId && _database.FestivalCatalogItems.Any(menuRow => menuRow.FestivalId == festivalId && menuRow.CatalogItemId == recipeLine.CatalogItemId)))
                              .OrderBy(stock => stock.Ingredient.Name)
                              .ProjectToType<AdminFestivalIngredientView>(_mapping)
                              .ToListAsync(cancellationToken);
    IReadOnlyDictionary<Guid, IReadOnlyDictionary<DateTime, double>> consumedByIngredientIdAndOrderTime = await ConsumedByOrderTimeAsync(festivalId, cancellationToken);
    var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

    return new AdminFestivalIngredientListView(rows.Select(row => LevelOf(festival, row, consumedByIngredientIdAndOrderTime.GetValueOrDefault(row.IngredientId, ReadOnlyDictionary<DateTime, double>.Empty), nowUtc)).ToList());
  }

  public async Task<ErrorOr<SavedFestivalIngredientView>> SetAvailableAmountAsync(Guid festivalId, Guid ingredientId, SaveFestivalIngredientRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (request.AvailableAmount is { } amount && (!double.IsFinite(amount) || amount < 0))
      return Refusal.Ingredients.StockInvalid(amount);

    var row = await _database.FestivalIngredients.FirstOrDefaultAsync(stock => stock.FestivalId == festivalId && stock.IngredientId == ingredientId, cancellationToken);

    if (row is null)
      return Refusal.Ingredients.StockRowNotFound(festivalId, ingredientId);

    row.AvailableAmount = request.AvailableAmount;
    await _database.SaveChangesAsync(cancellationToken);

    await _soldOutMarker.MarkItemsWithoutEnoughStockSoldOutAsync(festivalId, cancellationToken);

    return row.Adapt<SavedFestivalIngredientView>(_mapping);
  }

  private AdminFestivalIngredientView LevelOf(Festival festival, AdminFestivalIngredientView row, IReadOnlyDictionary<DateTime, double> consumedAtOrderTimes, DateTime nowUtc)
  {
    var usage = _forecast.UsageOf(festival, consumedAtOrderTimes, nowUtc);
    var usedAmount = usage[^1].UsedAmount;
    var runsOutAtUtcSinceStart = _forecast.RunsOutAtUtcSinceStart(festival, row.AvailableAmount, usedAmount, nowUtc);
    var runsOutAtUtcFromLastTwoHours = _forecast.RunsOutAtUtcFromLastTwoHours(festival, row.AvailableAmount, usage, nowUtc);
    var runsOutSoonAtUtc = _forecast.RunsOutSoonAtUtc(festival, runsOutAtUtcSinceStart, runsOutAtUtcFromLastTwoHours, nowUtc);

    return row with
           {
             UsedAmount = usedAmount,
             Usage = usage,
             RunsOutAtUtcSinceStart = runsOutAtUtcSinceStart,
             RunsOutAtUtcFromLastTwoHours = runsOutAtUtcFromLastTwoHours,
             StockWarning = _forecast.StockWarningOf(row.AvailableAmount, usedAmount, runsOutSoonAtUtc),
             RunsOutSoonAtUtc = runsOutSoonAtUtc
           };
  }

  private async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<DateTime, double>>> ConsumedByOrderTimeAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    var consumption = await _database.OrderItems.AsNoTracking()
                                     .Where(orderItem => orderItem.StationOrder.Order.FestivalId == festivalId)
                                     .ConsumedIngredients(_database.CatalogItemIngredients)
                                     .GroupBy(consumed => new
                                                          {
                                                            consumed.IngredientId,
                                                            consumed.OrderedAtUtc
                                                          })
                                     .Select(group => new
                                                      {
                                                        group.Key.IngredientId,
                                                        group.Key.OrderedAtUtc,
                                                        ConsumedAmount = group.Sum(consumed => consumed.Amount)
                                                      })
                                     .ToListAsync(cancellationToken);

    return consumption.GroupBy(consumed => consumed.IngredientId).ToDictionary(ingredient => ingredient.Key, ingredient => (IReadOnlyDictionary<DateTime, double>)ingredient.ToDictionary(consumed => DateTime.SpecifyKind(consumed.OrderedAtUtc, DateTimeKind.Utc), consumed => consumed.ConsumedAmount));
  }
}
