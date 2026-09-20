using FakeItEasy;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OrderStatusReaderTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<IStationOrderRepository>();

    A.CallTo(() => _repository.FindOrderIdsOfStationOrdersAsync(A<IReadOnlyCollection<Guid>>._,
                                                                 A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<Guid>>([_orderId]));
    A.CallTo(() => _repository.FindFulfillmentCountsAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<OrderFulfillmentCounts>>([]));

    _reader = new(_repository, new());
  }

  private readonly Guid _orderId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
  private readonly Guid _stationOrderId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

  private IStationOrderRepository _repository = null!;
  private OrderStatusReader _reader = null!;

  [Test]
  public async Task ReadStatusesOfStationOrdersAsync_HalfOfTheOrderIsDone_ReportsItAsPartiallyFulfilled()
  {
    GivenCounts(4, 2);

    IReadOnlyList<OrderStatusChange> statuses =
      await _reader.ReadStatusesOfStationOrdersAsync([_stationOrderId],
                                                      TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(statuses, Has.Count.EqualTo(1));
                      Assert.That(statuses[0].OrderId, Is.EqualTo(_orderId));
                      Assert.That(statuses[0].Status, Is.EqualTo(OrderStatus.PartiallyFulfilled));
                    });
  }

  [Test]
  public async Task ReadStatusesOfStationOrdersAsync_EveryItemIsDone_ReportsTheOrderAsFulfilled()
  {
    GivenCounts(4, 4);

    IReadOnlyList<OrderStatusChange> statuses =
      await _reader.ReadStatusesOfStationOrdersAsync([_stationOrderId],
                                                      TestContext.CurrentContext.CancellationToken);

    Assert.That(statuses[0].Status, Is.EqualTo(OrderStatus.Fulfilled));
  }

  [Test]
  public async Task ReadStatusesOfStationOrdersAsync_NoStationOrders_ReadsNothingAndReportsNothing()
  {
    IReadOnlyList<OrderStatusChange> statuses =
      await _reader.ReadStatusesOfStationOrdersAsync([], TestContext.CurrentContext.CancellationToken);

    Assert.That(statuses, Is.Empty);

    A.CallTo(() => _repository.FindOrderIdsOfStationOrdersAsync(A<IReadOnlyCollection<Guid>>._,
                                                                 A<CancellationToken>._))
     .MustNotHaveHappened();
  }

  [Test]
  public async Task ReadStatusesOfStationOrdersAsync_AnOrderThatIsNoLongerThere_ReportsNothingForIt()
  {
    IReadOnlyList<OrderStatusChange> statuses =
      await _reader.ReadStatusesOfStationOrdersAsync([_stationOrderId],
                                                      TestContext.CurrentContext.CancellationToken);

    Assert.That(statuses, Is.Empty);
  }

  [Test]
  public async Task ReadStatusesOfStationOrdersAsync_SeveralStationOrders_AsksForTheirCountsInOneRead()
  {
    GivenCounts(4, 1);

    await _reader.ReadStatusesOfStationOrdersAsync([_stationOrderId, Guid.NewGuid()],
                                                    TestContext.CurrentContext.CancellationToken);

    A.CallTo(() => _repository.FindFulfillmentCountsAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Test]
  public void ReadStatusesOfStationOrdersAsync_NullStationOrderIds_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _reader.ReadStatusesOfStationOrdersAsync(null!,
                                                                            TestContext.CurrentContext
                                                                                       .CancellationToken),
                Throws.ArgumentNullException);
  }

  private void GivenCounts(int itemCount, int fulfilledItemCount)
  {
    A.CallTo(() => _repository.FindFulfillmentCountsAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<OrderFulfillmentCounts>>([
                                                                       new()
                                                                       {
                                                                         OrderId = _orderId,
                                                                         ItemCount = itemCount,
                                                                         FulfilledItemCount = fulfilledItemCount
                                                                       }
                                                                     ]));
  }
}
