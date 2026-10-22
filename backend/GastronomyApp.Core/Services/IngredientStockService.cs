using System.Collections.ObjectModel;
using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class IngredientStockService
{
  private const int ForecastWindowHours = 2;
  private const int WarningHorizonHours = 2;
  private readonly IngredientAmountComparison _amountComparison;
  private readonly IFestivalRepository _festivalRepository;
  private readonly IIngredientStockRepository _repository;
  private readonly FestivalSchedule _schedule;
  private readonly StockSoldOutMarker _soldOutMarker;
  private readonly TimeProvider _timeProvider;

  public IngredientStockService(IIngredientStockRepository repository, IFestivalRepository festivalRepository, StockSoldOutMarker soldOutMarker, FestivalSchedule schedule, IngredientAmountComparison amountComparison, TimeProvider timeProvider)
  {
    _repository = repository;
    _festivalRepository = festivalRepository;
    _soldOutMarker = soldOutMarker;
    _schedule = schedule;
    _amountComparison = amountComparison;
    _timeProvider = timeProvider;
  }

  public async Task<ErrorOr<IReadOnlyList<IngredientStockLevel>>> ListAtFestivalAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    var festival = await _festivalRepository.FindByIdAsync(festivalId, cancellationToken);

    if (festival is null)
      return Refusal.Ingredients.FestivalNotFound(festivalId);

    IReadOnlyList<FestivalIngredient> stock = await _repository.FindActiveUsedOnTheMenuAsync(festivalId, cancellationToken);
    IReadOnlyDictionary<Guid, IReadOnlyDictionary<DateTime, double>> consumedByIngredientIdAndOrderTime = await _repository.SumConsumedAmountsByOrderTimeAsync(festivalId, cancellationToken);
    var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
    List<IngredientStockLevel> levels = [];

    foreach (var row in stock)
    {
      IReadOnlyList<IngredientUsagePointResult> usage = UsageOf(festival, consumedByIngredientIdAndOrderTime.GetValueOrDefault(row.IngredientId, ReadOnlyDictionary<DateTime, double>.Empty), nowUtc);
      var usedAmount = usage[^1].UsedAmount;
      var runsOutAtUtcSinceStart = RunsOutAtUtcSinceStart(festival, row.AvailableAmount, usedAmount, nowUtc);
      var runsOutAtUtcFromLastTwoHours = RunsOutAtUtcFromLastTwoHours(festival, row.AvailableAmount, usage, nowUtc);
      var runsOutSoonAtUtc = RunsOutSoonAtUtc(festival, runsOutAtUtcSinceStart, runsOutAtUtcFromLastTwoHours, nowUtc);

      levels.Add(new(row, usedAmount, usage, runsOutAtUtcSinceStart, runsOutAtUtcFromLastTwoHours, StockWarningOf(row.AvailableAmount, usedAmount, runsOutSoonAtUtc), runsOutSoonAtUtc));
    }

    return levels;
  }

  public async Task<ErrorOr<FestivalIngredient>> SetAvailableAmountAsync(Guid festivalId, Guid ingredientId, double? availableAmount, CancellationToken cancellationToken)
  {
    if (availableAmount is { } amount && (!double.IsFinite(amount) || amount < 0))
      return Refusal.Ingredients.StockInvalid(amount);

    var row = await _repository.FindAsync(festivalId, ingredientId, cancellationToken);

    if (row is null)
      return Refusal.Ingredients.StockRowNotFound(festivalId, ingredientId);

    row.AvailableAmount = availableAmount;
    await _repository.SaveChangesAsync(cancellationToken);

    await _soldOutMarker.MarkItemsWithoutEnoughStockSoldOutAsync(festivalId, cancellationToken);

    return row;
  }

  public IReadOnlyList<IngredientUsagePointResult> UsageOf(Festival festival, IReadOnlyDictionary<DateTime, double> consumedAtOrderTimes, DateTime nowUtc)
  {
    ArgumentNullException.ThrowIfNull(festival);
    ArgumentNullException.ThrowIfNull(consumedAtOrderTimes);

    var lastMomentUtc = new DateTime(Math.Clamp(nowUtc.Ticks, festival.StartsAtUtc.Ticks, festival.EndsAtUtc.Ticks), DateTimeKind.Utc);
    List<IngredientUsagePointResult> usage = [new(festival.StartsAtUtc, 0)];
    var cumulativeAmount = 0d;

    foreach (var consumed in consumedAtOrderTimes.OrderBy(consumed => consumed.Key).Append(new KeyValuePair<DateTime, double>(lastMomentUtc, 0)))
    {
      cumulativeAmount += consumed.Value;
      var atUtc = new DateTime(Math.Clamp(consumed.Key.Ticks, festival.StartsAtUtc.Ticks, lastMomentUtc.Ticks), DateTimeKind.Utc);

      if (usage[^1].AtUtc == atUtc)
        usage[^1] = new(atUtc, cumulativeAmount);
      else
        usage.Add(new(atUtc, cumulativeAmount));
    }

    return usage;
  }

  public DateTime? RunsOutAtUtcSinceStart(Festival festival, double? availableAmount, double usedAmount, DateTime nowUtc)
  {
    ArgumentNullException.ThrowIfNull(festival);

    if (!_schedule.IsRunning(festival, nowUtc) || availableAmount is not { } available)
      return null;

    if (_amountComparison.IsAtLeast(0, usedAmount) || _amountComparison.IsAtLeast(usedAmount, available))
      return null;

    return RunsOutAtUtcAtTheMeasuredRate(festival.StartsAtUtc, usedAmount, available - usedAmount, nowUtc);
  }

  public DateTime? RunsOutAtUtcFromLastTwoHours(Festival festival, double? availableAmount, IReadOnlyList<IngredientUsagePointResult> usage, DateTime nowUtc)
  {
    ArgumentNullException.ThrowIfNull(festival);
    ArgumentNullException.ThrowIfNull(usage);

    if (usage.Count == 0)
      throw new ArgumentException("The usage of an ingredient always holds the festival's start.", nameof(usage));

    if (!_schedule.IsRunning(festival, nowUtc) || availableAmount is not { } available)
      return null;

    var windowStartUtc = new DateTime(Math.Max((nowUtc - TimeSpan.FromHours(ForecastWindowHours)).Ticks, festival.StartsAtUtc.Ticks), DateTimeKind.Utc);
    var usedAmount = usage[^1].UsedAmount;
    var usedBeforeTheWindow = usage.LastOrDefault(point => point.AtUtc < windowStartUtc)?.UsedAmount ?? 0;

    if (_amountComparison.IsAtLeast(usedBeforeTheWindow, usedAmount) || _amountComparison.IsAtLeast(usedAmount, available))
      return null;

    return RunsOutAtUtcAtTheMeasuredRate(windowStartUtc, usedAmount - usedBeforeTheWindow, available - usedAmount, nowUtc);
  }

  public DateTime? RunsOutAtUtcAtTheMeasuredRate(DateTime rateMeasuredFromUtc, double usedSinceThen, double remainingAmount, DateTime nowUtc)
  {
    ArgumentOutOfRangeException.ThrowIfGreaterThan(rateMeasuredFromUtc, nowUtc);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(usedSinceThen);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(remainingAmount);

    var ticksUntilUsedUp = Math.Round((nowUtc - rateMeasuredFromUtc).Ticks * (remainingAmount / usedSinceThen));

    if (double.IsNaN(ticksUntilUsedUp) || ticksUntilUsedUp >= (DateTime.MaxValue - nowUtc).Ticks)
      return null;

    return nowUtc.AddTicks((long)ticksUntilUsedUp);
  }

  public DateTime? RunsOutSoonAtUtc(Festival festival, DateTime? runsOutAtUtcSinceStart, DateTime? runsOutAtUtcFromLastTwoHours, DateTime nowUtc)
  {
    ArgumentNullException.ThrowIfNull(festival);

    if (!_schedule.IsRunning(festival, nowUtc)
        || new[]
           {
             runsOutAtUtcSinceStart,
             runsOutAtUtcFromLastTwoHours
           }.Min() is not { } earlierUtc)
      return null;

    if (earlierUtc >= festival.EndsAtUtc || earlierUtc > nowUtc + TimeSpan.FromHours(WarningHorizonHours))
      return null;

    return earlierUtc;
  }

  public StockWarning StockWarningOf(double? availableAmount, double usedAmount, DateTime? runsOutSoonAtUtc)
  {
    if (availableAmount is { } available && _amountComparison.IsAtLeast(usedAmount, available))
      return StockWarning.UsedUp;

    if (runsOutSoonAtUtc is null)
      return StockWarning.None;

    return StockWarning.RunsOutSoon;
  }
}
