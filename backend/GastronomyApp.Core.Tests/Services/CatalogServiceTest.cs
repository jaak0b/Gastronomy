using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class CatalogServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _catalogRepository = A.Fake<ICatalogRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _orderabilityRepository = A.Fake<IItemOrderabilityRepository>();
    _clock = A.Fake<IClock>();

    A.CallTo(() => _clock.UtcNow).Returns(_now);
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(BuildFestival()));
    A.CallTo(() => _catalogRepository.ReadAtFestivalAsync(_festivalId, "Sommerfest", A<CancellationToken>._)).ReturnsLazily(() => Task.FromResult(BuildCatalog()));
    A.CallTo(() => _orderabilityRepository.FindActiveStationIdsAtFestivalAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Guid>>([_kitchenId]));
    A.CallTo(() => _orderabilityRepository.FindActiveMenuItemIdsAsync(_festivalId, A<CancellationToken>._))
   .Returns(Task.FromResult<IReadOnlyList<Guid>>([
                                                   _bratwurstId,
                                                   _beerId
                                                 ]));
    A.CallTo(() => _orderabilityRepository.FindItemIdsPreparedByAsync(_festivalId, A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Guid>>([_bratwurstId]));

    _service = new(_catalogRepository, new(_orderabilityRepository, _festivalRepository, _clock), new(_festivalRepository, new(), _clock));
  }

  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);
  private readonly Guid _beerId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
  private readonly Guid _bratwurstId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
  private readonly Guid _drinkCategoryId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _foodCategoryId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
  private readonly Guid _kitchenId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

  private ICatalogRepository _catalogRepository = null!;
  private IClock _clock = null!;
  private IFestivalRepository _festivalRepository = null!;
  private IItemOrderabilityRepository _orderabilityRepository = null!;
  private CatalogService _service = null!;

  [Test]
  public async Task ReadRunningFestivalCatalogAsync_NoFestivalIsRunning_ReturnsNothing()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));

    var catalog = await _service.ReadRunningFestivalCatalogAsync(CancellationToken.None);

    Assert.That(catalog, Is.Null);
  }

  [Test]
  public async Task ReadRunningFestivalCatalogAsync_AnItemNoStationPrepares_LeavesTheItemAndItsCategoryOut()
  {
    var catalog = await _service.ReadRunningFestivalCatalogAsync(CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(catalog!.Items.Select(item => item.ItemId), Is.EqualTo(new[] { _bratwurstId }));
                      Assert.That(catalog.Categories.Select(category => category.CategoryId), Is.EqualTo(new[] { _foodCategoryId }));
                    });
  }

  [Test]
  public async Task ReadRunningFestivalCatalogAsync_ARunningFestival_KeepsItsNameAndStations()
  {
    var catalog = await _service.ReadRunningFestivalCatalogAsync(CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(catalog!.FestivalName, Is.EqualTo("Sommerfest"));
                      Assert.That(catalog.Stations.Select(station => station.StationId), Is.EqualTo(new[] { _kitchenId }));
                    });
  }

  private Festival BuildFestival()
  {
    return new()
           {
             Id = _festivalId,
             Name = "Sommerfest",
             StartsAtUtc = _now.AddHours(-1),
             EndsAtUtc = _now.AddHours(5),
             NextOrderNumber = 1,
             IsHidden = false
           };
  }

  private CatalogAtFestival BuildCatalog()
  {
    return new(_festivalId,
               "Sommerfest",
               [new(_kitchenId, "Kueche", 1)],
               [
                 new(_foodCategoryId, "Speisen", "#C62828", 1),
                 new(_drinkCategoryId, "Getraenke", "#1565C0", 2)
               ],
               [
                 new(_bratwurstId, _foodCategoryId, "Bratwurst", 350, 1, true, 5, false, [_kitchenId]),
                 new(_beerId, _drinkCategoryId, "Bier", 250, 2, true, null, true, [])
               ]);
  }
}
