using ErrorOr;
using FakeItEasy;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class IngredientStockServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<IIngredientStockRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _clock = new(new(_now));
    _festival = new()
    {
      Id = Guid.NewGuid(),
      Name = "Sommerfest",
      StartsAtUtc = _now.AddHours(-2),
      EndsAtUtc = _now.AddHours(6),
      NextOrderNumber = 1,
      IsHidden = false
    };
    _mustardStock = new()
    {
      Id = Guid.NewGuid(),
      FestivalId = _festival.Id,
      IngredientId = Guid.NewGuid(),
      AvailableAmount = null,
      Ingredient = new()
      {
        Id = Guid.NewGuid(),
        Name = "Senf",
        Unit = IngredientUnit.Gram,
        IsActive = true
      }
    };

    A.CallTo(() => _festivalRepository.FindByIdAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));
    A.CallTo(() => _festivalRepository.FindByIdAsync(_festival.Id, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(_festival));
    A.CallTo(() => _repository.FindUsedOnTheMenuAsync(_festival.Id, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<FestivalIngredient>>([_mustardStock]));
    A.CallTo(() => _repository.SumConsumedAmountsAsync(_festival.Id, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyDictionary<Guid, double>>(new Dictionary<Guid, double> { [_mustardStock.IngredientId] = 300 }));
    A.CallTo(() => _repository.FindAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<FestivalIngredient?>(null));
    A.CallTo(() => _repository.FindAsync(_festival.Id, _mustardStock.IngredientId, A<CancellationToken>._)).Returns(Task.FromResult<FestivalIngredient?>(_mustardStock));
    A.CallTo(() => _repository.FindActiveWithAvailableAmountAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<FestivalIngredient>>([]));

    _service = new(_repository, _festivalRepository, new(_repository, NullLogger<StockSoldOutMarker>.Instance), _clock);
  }

  private readonly DateTime _now = new(2026, 9, 26, 20, 0, 0, DateTimeKind.Utc);

  private IIngredientStockRepository _repository = null!;
  private IFestivalRepository _festivalRepository = null!;
  private FakeTimeProvider _clock = null!;
  private Festival _festival = null!;
  private FestivalIngredient _mustardStock = null!;
  private IngredientStockService _service = null!;

  [Test]
  public async Task ListAtFestivalAsync_AnIngredientOnTheMenu_CarriesTheConsumedAmountFromTheFestivalsOrders()
  {
    _mustardStock.AvailableAmount = 900;

    ErrorOr<IReadOnlyList<IngredientStockLevel>> levels = await _service.ListAtFestivalAsync(_festival.Id, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(levels.Value, Has.Count.EqualTo(1));
                      Assert.That(levels.Value[0].Stock, Is.SameAs(_mustardStock));
                      Assert.That(levels.Value[0].UsedAmount, Is.EqualTo(300));
                      Assert.That(levels.Value[0].RunsOutAtUtc, Is.EqualTo(_now.AddHours(4)));
                    });
  }

  [Test]
  public async Task ListAtFestivalAsync_AnUnknownFestival_IsRefusedAsNotFound()
  {
    ErrorOr<IReadOnlyList<IngredientStockLevel>> levels = await _service.ListAtFestivalAsync(Guid.NewGuid(), CancellationToken.None);

    Assert.That(levels.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  [Test]
  public void RunsOutAtUtc_HalfUsedAfterTwoHours_RunsOutTwoHoursFromNow()
  {
    Assert.That(_service.RunsOutAtUtc(_festival, 600, 300), Is.EqualTo(_now.AddHours(2)));
  }

  [Test]
  public void RunsOutAtUtc_NoLimit_IsUnknown()
  {
    Assert.That(_service.RunsOutAtUtc(_festival, null, 300), Is.Null);
  }

  [Test]
  public void RunsOutAtUtc_NothingUsedYet_IsUnknown()
  {
    Assert.That(_service.RunsOutAtUtc(_festival, 600, 0), Is.Null);
  }

  [Test]
  public void RunsOutAtUtc_BeforeTheFestivalStarts_IsUnknown()
  {
    _festival.StartsAtUtc = _now.AddHours(1);

    Assert.That(_service.RunsOutAtUtc(_festival, 600, 300), Is.Null);
  }

  [TestCase(300d)]
  [TestCase(200d)]
  public void RunsOutAtUtc_NothingLeft_IsUnknown(double availableAmount)
  {
    Assert.That(_service.RunsOutAtUtc(_festival, availableAmount, 300), Is.Null);
  }

  [Test]
  public async Task SetAvailableAmountAsync_AnAmount_StoresItAndChecksTheMenuForThatFestival()
  {
    ErrorOr<FestivalIngredient> saved = await _service.SetAvailableAmountAsync(_festival.Id, _mustardStock.IngredientId, 500, CancellationToken.None);

    Assert.That(saved.Value.AvailableAmount, Is.EqualTo(500));
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly().Then(A.CallTo(() => _repository.FindActiveWithAvailableAmountAsync(_festival.Id, A<CancellationToken>._)).MustHaveHappenedOnceExactly());
  }

  [Test]
  public async Task SetAvailableAmountAsync_NoAmount_StoresUnlimited()
  {
    _mustardStock.AvailableAmount = 500;

    ErrorOr<FestivalIngredient> saved = await _service.SetAvailableAmountAsync(_festival.Id, _mustardStock.IngredientId, null, CancellationToken.None);

    Assert.That(saved.Value.AvailableAmount, Is.Null);
  }

  [TestCase(-1d)]
  [TestCase(double.NaN)]
  [TestCase(double.PositiveInfinity)]
  public async Task SetAvailableAmountAsync_AnAmountBelowZeroOrNotANumber_IsRefusedAsStockInvalid(double availableAmount)
  {
    ErrorOr<FestivalIngredient> saved = await _service.SetAvailableAmountAsync(_festival.Id, _mustardStock.IngredientId, availableAmount, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(saved.RefusalMessageKey(), Is.EqualTo("errors.admin.ingredients.stockInvalid"));
                      Assert.That(saved.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(saved.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("ValidationFailed"));
                      Assert.That(_mustardStock.AvailableAmount, Is.Null);
                    });
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task SetAvailableAmountAsync_AnIngredientTheFestivalHasNoRowFor_IsRefusedAsNotFound()
  {
    ErrorOr<FestivalIngredient> saved = await _service.SetAvailableAmountAsync(_festival.Id, Guid.NewGuid(), 5, CancellationToken.None);

    Assert.That(saved.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }
}
