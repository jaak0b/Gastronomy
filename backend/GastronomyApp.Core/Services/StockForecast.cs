using GastronomyApp.Contracts.Admin.Ingredients;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Services;

public sealed class StockForecast
{
  private const int ForecastWindowHours = 2;
  private const int WarningHorizonHours = 2;
  private readonly IngredientAmountComparison _amountComparison;
  private readonly FestivalSchedule _schedule;

  public StockForecast(FestivalSchedule schedule, IngredientAmountComparison amountComparison)
  {
    _schedule = schedule;
    _amountComparison = amountComparison;
  }

  public IReadOnlyList<IngredientUsagePoint> UsageOf(Festival festival, IReadOnlyDictionary<DateTime, double> consumedAtOrderTimes, DateTime nowUtc)
  {
    ArgumentNullException.ThrowIfNull(festival);
    ArgumentNullException.ThrowIfNull(consumedAtOrderTimes);

    var lastMomentUtc = new DateTime(Math.Clamp(nowUtc.Ticks, festival.StartsAtUtc.Ticks, festival.EndsAtUtc.Ticks), DateTimeKind.Utc);
    List<IngredientUsagePoint> usage = [new(festival.StartsAtUtc, 0)];
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

  public DateTime? RunsOutAtUtcFromLastTwoHours(Festival festival, double? availableAmount, IReadOnlyList<IngredientUsagePoint> usage, DateTime nowUtc)
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
