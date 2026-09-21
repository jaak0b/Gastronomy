using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using Microsoft.Extensions.Time.Testing;

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
    _clock = new FakeTimeProvider(new(_now));

    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(BuildFestival()));
    A.CallTo(() => _catalogRepository.FindWithMenuAsync(_festivalId, A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).ReturnsLazily(() => Task.FromResult<Festival?>(BuildFestival()));
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
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _kitchenId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

  private ICatalogRepository _catalogRepository = null!;
  private TimeProvider _clock = null!;
  private IFestivalRepository _festivalRepository = null!;
  private IItemOrderabilityRepository _orderabilityRepository = null!;
  private CatalogService _service = null!;

  [Test]
  public async Task ReadRunningFestivalCatalogAsync_NoFestivalIsRunning_ReturnsNothing()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));

    var festival = await _service.ReadRunningFestivalCatalogAsync(CancellationToken.None);

    Assert.That(festival, Is.Null);
  }

  [Test]
  public async Task ReadRunningFestivalCatalogAsync_AnItemNoStationPrepares_AsksOnlyForTheItemsTheWaiterCanOrder()
  {
    await _service.ReadRunningFestivalCatalogAsync(CancellationToken.None);

    A.CallTo(() => _catalogRepository.FindWithMenuAsync(_festivalId, A<IReadOnlyCollection<Guid>>.That.IsSameSequenceAs(TheBratwurstAlone()), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task ReadRunningFestivalCatalogAsync_ARunningFestival_ReturnsTheFestivalWithItsMenu()
  {
    var festival = await _service.ReadRunningFestivalCatalogAsync(CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(festival!.Id, Is.EqualTo(_festivalId));
                      Assert.That(festival.Name, Is.EqualTo("Sommerfest"));
                    });
  }

  private IReadOnlyCollection<Guid> TheBratwurstAlone()
  {
    return [_bratwurstId];
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
}
