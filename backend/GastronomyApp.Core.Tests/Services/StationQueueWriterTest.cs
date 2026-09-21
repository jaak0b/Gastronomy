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
public sealed class StationQueueWriterTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<IStationOrderRepository>();
    _clock = new FakeTimeProvider(new(_now));
    _transactionRunner = new();

    A.CallTo(() => _repository.FindItemsAtStationAsync(A<IReadOnlyCollection<Guid>>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<OrderItem>>([]));

    _writer = new(_repository, new(), new(), _transactionRunner, _clock);
  }

  private readonly DateTime _now = new(2026, 9, 5, 20, 15, 0, DateTimeKind.Utc);
  private readonly Guid _stationId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");

  private TimeProvider _clock = null!;
  private IStationOrderRepository _repository = null!;
  private RecordingTransactionRunner _transactionRunner = null!;
  private StationQueueWriter _writer = null!;

  [Test]
  public async Task FulfillAsync_AnOpenItemAtThisStation_StampsItAndCommits()
  {
    var bratwurst = OpenItem();
    GivenItemsAtThisStation(bratwurst);

    ErrorOr<IReadOnlyList<StationOrder>> written = await _writer.FulfillAsync([bratwurst.Id], _stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(written.IsSuccess, Is.True);
                      Assert.That(bratwurst.FulfilledAtUtc, Is.EqualTo(_now));
                      Assert.That(_transactionRunner.Committed, Is.True);
                    });

    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappened();
  }

  [Test]
  public async Task FulfillAsync_AnItemThatIsNotAtThisStation_RefusesAndRollsBack()
  {
    ErrorOr<IReadOnlyList<StationOrder>> written = await _writer.FulfillAsync([Guid.NewGuid()], _stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(written.IsSuccess, Is.False);
                      Assert.That(written.RefusalMessageKey(), Is.EqualTo("station.itemNotAtThisStation"));
                      Assert.That(_transactionRunner.Committed, Is.False);
                    });

    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public void FulfillAsync_NullSelection_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _writer.FulfillAsync(null!, _stationId, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public async Task UnfulfillAsync_AnItemTheStationHandedOut_ClearsTheStampAndCommits()
  {
    var bratwurst = OpenItem();
    bratwurst.FulfilledAtUtc = _now.AddMinutes(-1);
    GivenItemsAtThisStation(bratwurst);

    ErrorOr<IReadOnlyList<StationOrder>> written = await _writer.UnfulfillAsync([bratwurst.Id], _stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(written.IsSuccess, Is.True);
                      Assert.That(bratwurst.FulfilledAtUtc, Is.Null);
                      Assert.That(_transactionRunner.Committed, Is.True);
                    });
  }

  [Test]
  public async Task UnfulfillAsync_AnItemThatIsStillOpen_RefusesAndRollsBack()
  {
    var bratwurst = OpenItem();
    GivenItemsAtThisStation(bratwurst);

    ErrorOr<IReadOnlyList<StationOrder>> written = await _writer.UnfulfillAsync([bratwurst.Id], _stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(written.IsSuccess, Is.False);
                      Assert.That(written.RefusalMessageKey(), Is.EqualTo("station.changeNotSaved"));
                      Assert.That(_transactionRunner.Committed, Is.False);
                    });
  }

  [Test]
  public void UnfulfillAsync_NullSelection_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _writer.UnfulfillAsync(null!, _stationId, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public async Task HideFromAsItComesQueueAsync_AnAsItComesOrderAtThisStation_HidesItAndSaves()
  {
    var stationOrder = StationOrderWith(DeliveryMode.AsItComes);
    GivenStationOrder(stationOrder);

    ErrorOr<StationOrder> hidden = await _writer.HideFromAsItComesQueueAsync(stationOrder.Id, _stationId, _festivalId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(hidden.IsSuccess, Is.True);
                      Assert.That(stationOrder.IsHiddenFromAsItComesQueue, Is.True);
                    });

    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappened();
  }

  [Test]
  public async Task HideFromAsItComesQueueAsync_ATogetherOrder_RefusesAndSavesNothing()
  {
    var stationOrder = StationOrderWith(DeliveryMode.Together);
    GivenStationOrder(stationOrder);

    ErrorOr<StationOrder> hidden = await _writer.HideFromAsItComesQueueAsync(stationOrder.Id, _stationId, _festivalId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(hidden.IsSuccess, Is.False);
                      Assert.That(hidden.RefusalMessageKey(), Is.EqualTo("station.changeNotSaved"));
                      Assert.That(stationOrder.IsHiddenFromAsItComesQueue, Is.False);
                    });

    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task HideFromAsItComesQueueAsync_AnOrderOfAnotherStation_RefusesBecauseItIsNotAtThisStation()
  {
    A.CallTo(() => _repository.FindAtStationAsync(A<Guid>._, A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<StationOrder?>(null));

    ErrorOr<StationOrder> hidden = await _writer.HideFromAsItComesQueueAsync(Guid.NewGuid(), _stationId, _festivalId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(hidden.IsSuccess, Is.False);
                      Assert.That(hidden.RefusalMessageKey(), Is.EqualTo("station.orderNotAtThisStation"));
                    });
  }

  private void GivenItemsAtThisStation(params OrderItem[] items)
  {
    A.CallTo(() => _repository.FindItemsAtStationAsync(A<IReadOnlyCollection<Guid>>._, _stationId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<OrderItem>>(items.ToList()));
  }

  private void GivenStationOrder(StationOrder stationOrder)
  {
    A.CallTo(() => _repository.FindAtStationAsync(stationOrder.Id, _stationId, _festivalId, A<CancellationToken>._)).Returns(Task.FromResult<StationOrder?>(stationOrder));
  }

  private OrderItem OpenItem()
  {
    var stationOrder = StationOrderWith(DeliveryMode.Together);

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

  private StationOrder StationOrderWith(DeliveryMode deliveryMode)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             OrderId = Guid.NewGuid(),
             FestivalId = _festivalId,
             StationId = _stationId,
             StationOrderNumber = 1,
             DeliveryMode = deliveryMode
           };
  }
}
