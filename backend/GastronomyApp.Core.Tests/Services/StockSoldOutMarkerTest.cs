using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class StockSoldOutMarkerTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<IIngredientStockRepository>();
    _limitedStock.Clear();
    _consumed.Clear();
    _menuRows.Clear();

    A.CallTo(() => _repository.FindActiveWithAvailableAmountAsync(_festivalId, A<CancellationToken>._)).ReturnsLazily(() => Task.FromResult<IReadOnlyList<FestivalIngredient>>(_limitedStock.ToList()));
    A.CallTo(() => _repository.SumConsumedAmountsAsync(_festivalId, A<CancellationToken>._)).ReturnsLazily(() => Task.FromResult<IReadOnlyDictionary<Guid, double>>(_consumed));
    A.CallTo(() => _repository.FindAvailableMenuRowsWithRecipesAsync(_festivalId, A<CancellationToken>._)).ReturnsLazily(() => Task.FromResult<IReadOnlyList<FestivalCatalogItem>>(_menuRows.Where(menuRow => menuRow.IsAvailable).ToList()));

    _marker = new(_repository, NullLogger<StockSoldOutMarker>.Instance);
  }

  private readonly Guid _festivalId = Guid.NewGuid();
  private readonly Guid _mustardId = Guid.NewGuid();
  private readonly Guid _bunId = Guid.NewGuid();
  private readonly List<FestivalIngredient> _limitedStock = [];
  private readonly Dictionary<Guid, double> _consumed = [];
  private readonly List<FestivalCatalogItem> _menuRows = [];

  private IIngredientStockRepository _repository = null!;
  private StockSoldOutMarker _marker = null!;

  [Test]
  public async Task MarkItemsWithoutEnoughStockSoldOutAsync_LessLeftThanOnePortion_SwitchesTheArticleOff()
  {
    GivenStock(_mustardId, 100);
    _consumed[_mustardId] = 85;
    var bratwurst = GivenMenuRow(new Dictionary<Guid, double> { [_mustardId] = 20 });

    IReadOnlyList<FestivalCatalogItem> soldOut = await _marker.MarkItemsWithoutEnoughStockSoldOutAsync(_festivalId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(bratwurst.IsAvailable, Is.False);
                      Assert.That(soldOut, Is.EquivalentTo(new[] { bratwurst }));
                    });
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task MarkItemsWithoutEnoughStockSoldOutAsync_ExactlyOnePortionLeft_KeepsTheArticleOn()
  {
    GivenStock(_mustardId, 100);
    _consumed[_mustardId] = 80;
    var bratwurst = GivenMenuRow(new Dictionary<Guid, double> { [_mustardId] = 20 });

    await _marker.MarkItemsWithoutEnoughStockSoldOutAsync(_festivalId, CancellationToken.None);

    Assert.That(bratwurst.IsAvailable, Is.True);
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task MarkItemsWithoutEnoughStockSoldOutAsync_MoreConsumedThanWasAvailable_SwitchesTheArticleOffWithoutFailing()
  {
    GivenStock(_mustardId, 10);
    _consumed[_mustardId] = 30;
    var bratwurst = GivenMenuRow(new Dictionary<Guid, double> { [_mustardId] = 5 });

    await _marker.MarkItemsWithoutEnoughStockSoldOutAsync(_festivalId, CancellationToken.None);

    Assert.That(bratwurst.IsAvailable, Is.False);
  }

  [Test]
  public async Task MarkItemsWithoutEnoughStockSoldOutAsync_OnlyOneOfTwoIngredientsRunsShort_SwitchesOffOnlyTheArticlesUsingIt()
  {
    GivenStock(_mustardId, 100);
    GivenStock(_bunId, 50);
    _consumed[_mustardId] = 99;
    _consumed[_bunId] = 10;
    var bratwurst = GivenMenuRow(new Dictionary<Guid, double> { [_mustardId] = 20, [_bunId] = 1 });
    var plainBun = GivenMenuRow(new Dictionary<Guid, double> { [_bunId] = 1 });

    await _marker.MarkItemsWithoutEnoughStockSoldOutAsync(_festivalId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(bratwurst.IsAvailable, Is.False);
                      Assert.That(plainBun.IsAvailable, Is.True);
                    });
  }

  [Test]
  public async Task MarkItemsWithoutEnoughStockSoldOutAsync_AnIngredientWithoutALimit_NeverSwitchesAnythingOff()
  {
    _consumed[_mustardId] = 10000;
    var bratwurst = GivenMenuRow(new Dictionary<Guid, double> { [_mustardId] = 20 });

    IReadOnlyList<FestivalCatalogItem> soldOut = await _marker.MarkItemsWithoutEnoughStockSoldOutAsync(_festivalId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(bratwurst.IsAvailable, Is.True);
                      Assert.That(soldOut, Is.Empty);
                    });
    A.CallTo(() => _repository.FindAvailableMenuRowsWithRecipesAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task MarkItemsWithoutEnoughStockSoldOutAsync_PlentyLeftForAnArticleSwitchedOffByHand_LeavesItOff()
  {
    GivenStock(_mustardId, 1000);
    var bratwurst = GivenMenuRow(new Dictionary<Guid, double> { [_mustardId] = 20 });
    bratwurst.IsAvailable = false;

    await _marker.MarkItemsWithoutEnoughStockSoldOutAsync(_festivalId, CancellationToken.None);

    Assert.That(bratwurst.IsAvailable, Is.False);
  }

  private void GivenStock(Guid ingredientId, double availableAmount)
  {
    _limitedStock.Add(new()
                      {
                        Id = Guid.NewGuid(),
                        FestivalId = _festivalId,
                        IngredientId = ingredientId,
                        AvailableAmount = availableAmount
                      });
  }

  private FestivalCatalogItem GivenMenuRow(IReadOnlyDictionary<Guid, double> recipe)
  {
    var itemId = Guid.NewGuid();
    FestivalCatalogItem menuRow = new()
    {
      Id = Guid.NewGuid(),
      FestivalId = _festivalId,
      CatalogItemId = itemId,
      PriceCents = 350,
      IsAvailable = true,
      CatalogItem = new()
      {
        Id = itemId,
        Name = "Artikel",
        CategoryId = Guid.NewGuid(),
        SortOrder = 1,
        IsActive = true
      }
    };

    foreach (var line in recipe)
      menuRow.CatalogItem.Ingredients.Add(new()
                                          {
                                            Id = Guid.NewGuid(),
                                            CatalogItemId = itemId,
                                            IngredientId = line.Key,
                                            Amount = line.Value
                                          });

    _menuRows.Add(menuRow);

    return menuRow;
  }
}
