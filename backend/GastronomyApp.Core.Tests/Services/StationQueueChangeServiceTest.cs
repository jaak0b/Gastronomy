using ErrorOr;
using FakeItEasy;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class StationQueueChangeServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _stationOrderRepository = A.Fake<IStationOrderRepository>();
    _stationRepository = A.Fake<IStationRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _festivalStationRepository = A.Fake<IFestivalStationRepository>();
    _clock = new FakeTimeProvider(new(_now));

    A.CallTo(() => _stationRepository.FindByIdAsync(_stationId, A<CancellationToken>._)).Returns(Task.FromResult<Station?>(Kitchen()));
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(RunningFestival()));
    A.CallTo(() => _festivalStationRepository.FindLinkAsync(_festivalId, _stationId, A<CancellationToken>._)).Returns(Task.FromResult<FestivalStation?>(Link()));
    A.CallTo(() => _stationOrderRepository.FindItemsAtStationAsync(A<IReadOnlyCollection<Guid>>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<OrderItem>>([]));
    A.CallTo(() => _stationOrderRepository.FindStationWithUnfinishedOrdersAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Station?>(Kitchen()));
    A.CallTo(() => _stationOrderRepository.FindOrderIdsOfStationOrdersAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Guid>>([_orderId]));
    A.CallTo(() => _stationOrderRepository.FindOrdersWithItemsAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Order>>([TouchedOrder()]));

    StationAtFestivalLookup lookup = new(_stationRepository, _festivalStationRepository, new(_festivalRepository, new(), _clock));

    _stationOrdersAnnouncer = A.Fake<IStationOrdersAnnouncer>();
    _orderStatusAnnouncer = A.Fake<IOrderStatusAnnouncer>();

    _service = new(lookup, new(_stationOrderRepository, new(), new(), _clock), new(lookup, _stationOrderRepository), new(_stationOrderRepository), _stationOrdersAnnouncer, _orderStatusAnnouncer, new ImmediateAfterCommitActions());
  }

  private readonly DateTime _now = new(2026, 9, 5, 20, 15, 0, DateTimeKind.Utc);
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _stationId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
  private readonly Guid _orderId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

  private TimeProvider _clock = null!;
  private IFestivalRepository _festivalRepository = null!;
  private IFestivalStationRepository _festivalStationRepository = null!;
  private IStationOrderRepository _stationOrderRepository = null!;
  private IStationRepository _stationRepository = null!;
  private IOrderStatusAnnouncer _orderStatusAnnouncer = null!;
  private StationQueueChangeService _service = null!;
  private IStationOrdersAnnouncer _stationOrdersAnnouncer = null!;

  [Test]
  public async Task FulfillAsync_AnOpenItem_AnswersWithTheFreshQueueAndTheNewStatusOfItsOrder()
  {
    var bratwurst = OpenItem();
    GivenItemsAtThisStation(bratwurst);

    ErrorOr<Station> queue = await _service.FulfillAsync([bratwurst.Id], _stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.IsSuccess, Is.True);
                      Assert.That(queue.Value.Id, Is.EqualTo(_stationId));
                    });

    A.CallTo(() => _stationOrdersAnnouncer.AnnounceStationOrdersChangedAsync(_stationId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _orderStatusAnnouncer.AnnounceOrderStatusChangedAsync(A<Order>.That.Matches(order => order.Id == _orderId), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task FulfillAsync_AnItemAnotherTapHadAlreadyFinished_StillReportsTheStatusOfThatOrder()
  {
    var bratwurst = OpenItem();
    bratwurst.FulfilledAtUtc = _now.AddMinutes(-1);
    GivenItemsAtThisStation(bratwurst);

    ErrorOr<Station> queue = await _service.FulfillAsync([bratwurst.Id], _stationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(queue.IsSuccess, Is.True);

    A.CallTo(() => _orderStatusAnnouncer.AnnounceOrderStatusChangedAsync(A<Order>.That.Matches(order => order.Id == _orderId), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task FulfillAsync_NoFestivalIsRunning_RefusesAndWritesNothing()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));

    ErrorOr<Station> queue = await _service.FulfillAsync([Guid.NewGuid()], _stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.IsSuccess, Is.False);
                      Assert.That(queue.RefusalMessageKey(), Is.EqualTo("station.noFestivalIsRunning"));
                    });

    A.CallTo(() => _stationOrderRepository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public void FulfillAsync_NullSelection_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _service.FulfillAsync(null!, _stationId, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public async Task UnfulfillAsync_AnItemTheStationHandedOut_AnswersWithTheStatusOfItsOrder()
  {
    var bratwurst = OpenItem();
    bratwurst.FulfilledAtUtc = _now.AddMinutes(-1);
    GivenItemsAtThisStation(bratwurst);

    ErrorOr<Station> queue = await _service.UnfulfillAsync([bratwurst.Id], _stationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(queue.IsSuccess, Is.True);

    A.CallTo(() => _orderStatusAnnouncer.AnnounceOrderStatusChangedAsync(A<Order>.That.Matches(order => order.Id == _orderId), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public void UnfulfillAsync_NullSelection_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _service.UnfulfillAsync(null!, _stationId, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public async Task HideFromAsItComesQueueAsync_AnAsItComesOrder_AnswersWithTheQueueAndNoStatusChange()
  {
    var stationOrder = AsItComesStationOrder();
    A.CallTo(() => _stationOrderRepository.FindAtStationAsync(stationOrder.Id, _stationId, _festivalId, A<CancellationToken>._)).Returns(Task.FromResult<StationOrder?>(stationOrder));

    ErrorOr<Station> queue = await _service.HideFromAsItComesQueueAsync(stationOrder.Id, _stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.IsSuccess, Is.True);
                      Assert.That(stationOrder.IsHiddenFromAsItComesQueue, Is.True);
                    });

    A.CallTo(() => _orderStatusAnnouncer.AnnounceOrderStatusChangedAsync(A<Order>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task HideFromAsItComesQueueAsync_TheStationIsNotAtTheRunningFestival_RefusesBeforeItReadsTheOrder()
  {
    A.CallTo(() => _festivalStationRepository.FindLinkAsync(_festivalId, _stationId, A<CancellationToken>._)).Returns(Task.FromResult<FestivalStation?>(null));

    ErrorOr<Station> queue = await _service.HideFromAsItComesQueueAsync(Guid.NewGuid(), _stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue.IsSuccess, Is.False);
                      Assert.That(queue.RefusalMessageKey(), Is.EqualTo("station.notPartOfTheFestival"));
                    });

    A.CallTo(() => _stationOrderRepository.FindAtStationAsync(A<Guid>._, A<Guid>._, A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  private void GivenItemsAtThisStation(params OrderItem[] items)
  {
    A.CallTo(() => _stationOrderRepository.FindItemsAtStationAsync(A<IReadOnlyCollection<Guid>>._, _stationId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<OrderItem>>(items.ToList()));
  }

  private Order TouchedOrder()
  {
    return new()
           {
             Id = _orderId,
             ClientOrderId = Guid.NewGuid(),
             FestivalId = _festivalId,
             GlobalOrderNumber = 4,
             StaffMemberId = Guid.NewGuid(),
             TableName = "Tisch 12",
             CreatedAtUtc = _now
           };
  }

  private OrderItem OpenItem()
  {
    var stationOrder = AsItComesStationOrder();

    OrderItem item = new()
                     {
                       Id = Guid.NewGuid(),
                       StationOrderId = stationOrder.Id,
                       CatalogItemId = Guid.NewGuid(),
                       ItemName = "Bratwurst",
                       UnitPriceCents = 350,
                       StationOrder = stationOrder
                     };

    stationOrder.Items.Add(item);

    return item;
  }

  private StationOrder AsItComesStationOrder()
  {
    return new()
           {
             Id = Guid.NewGuid(),
             OrderId = _orderId,
             FestivalId = _festivalId,
             StationId = _stationId,
             StationOrderNumber = 1,
             DeliveryMode = DeliveryMode.AsItComes
           };
  }

  private Station Kitchen()
  {
    return new()
           {
             Id = _stationId,
             Name = "Kueche",
             SortOrder = 1,
             IsActive = true
           };
  }

  private Festival RunningFestival()
  {
    return new()
           {
             Id = _festivalId,
             Name = "Sommerfest",
             StartsAtUtc = _now.AddHours(-2),
             EndsAtUtc = _now.AddHours(5),
             NextOrderNumber = 1,
             IsHidden = false
           };
  }

  private FestivalStation Link()
  {
    return new()
           {
             Id = Guid.NewGuid(),
             FestivalId = _festivalId,
             StationId = _stationId,
             NextStationOrderNumber = 1
           };
  }
}
