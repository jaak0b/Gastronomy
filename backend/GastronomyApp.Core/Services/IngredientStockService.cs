using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class IngredientStockService
{
  private readonly IFestivalRepository _festivalRepository;
  private readonly IIngredientStockRepository _repository;
  private readonly StockSoldOutMarker _soldOutMarker;
  private readonly TimeProvider _timeProvider;

  public IngredientStockService(IIngredientStockRepository repository, IFestivalRepository festivalRepository, StockSoldOutMarker soldOutMarker, TimeProvider timeProvider)
  {
    _repository = repository;
    _festivalRepository = festivalRepository;
    _soldOutMarker = soldOutMarker;
    _timeProvider = timeProvider;
  }

  public async Task<ErrorOr<IReadOnlyList<IngredientStockLevel>>> ListAtFestivalAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    var festival = await _festivalRepository.FindByIdAsync(festivalId, cancellationToken);

    if (festival is null)
      return Refusal.Ingredients.FestivalNotFound(festivalId);

    IReadOnlyList<FestivalIngredient> stock = await _repository.FindUsedOnTheMenuAsync(festivalId, cancellationToken);
    IReadOnlyDictionary<Guid, double> consumedByIngredientId = await _repository.SumConsumedAmountsAsync(festivalId, cancellationToken);

    List<IngredientStockLevel> levels = stock.Select(row => new IngredientStockLevel(row, consumedByIngredientId.GetValueOrDefault(row.IngredientId), RunsOutAtUtc(festival, row.AvailableAmount, consumedByIngredientId.GetValueOrDefault(row.IngredientId)))).ToList();

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

  public DateTime? RunsOutAtUtc(Festival festival, double? availableAmount, double usedAmount)
  {
    ArgumentNullException.ThrowIfNull(festival);

    var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
    var elapsed = nowUtc - DateTime.SpecifyKind(festival.StartsAtUtc, DateTimeKind.Utc);

    if (availableAmount is not { } available || usedAmount <= 0 || elapsed <= TimeSpan.Zero)
      return null;

    var remaining = available - usedAmount;

    if (remaining <= 0)
      return null;

    return nowUtc + elapsed * (remaining / usedAmount);
  }
}
