using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class ChangedOrderReaderTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<IStationOrderRepository>();

    A.CallTo(() => _repository.FindOrderIdsOfStationOrdersAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Guid>>([_orderId]));
    A.CallTo(() => _repository.FindOrdersWithItemsAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Order>>([]));

    _reader = new(_repository);
  }

  private readonly DateTime _orderedAtUtc = new(2026, 9, 5, 19, 5, 0, DateTimeKind.Utc);
  private readonly Guid _orderId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
  private readonly Guid _stationOrderId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

  private IStationOrderRepository _repository = null!;
  private ChangedOrderReader _reader = null!;

  [Test]
  public async Task ReadOrdersOfStationOrdersAsync_AStationOrderThatWasTouched_ReturnsTheOrderItBelongsTo()
  {
    GivenTheOrderIsStillThere();

    IReadOnlyList<Order> orders = await _reader.ReadOrdersOfStationOrdersAsync([_stationOrderId], TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(orders, Has.Count.EqualTo(1));
                      Assert.That(orders[0].Id, Is.EqualTo(_orderId));
                    });
  }

  [Test]
  public async Task ReadOrdersOfStationOrdersAsync_NoStationOrders_ReadsNothingAndReportsNothing()
  {
    IReadOnlyList<Order> orders = await _reader.ReadOrdersOfStationOrdersAsync([], TestContext.CurrentContext.CancellationToken);

    Assert.That(orders, Is.Empty);

    A.CallTo(() => _repository.FindOrderIdsOfStationOrdersAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task ReadOrdersOfStationOrdersAsync_AnOrderThatIsNoLongerThere_ReportsNothingForIt()
  {
    IReadOnlyList<Order> orders = await _reader.ReadOrdersOfStationOrdersAsync([_stationOrderId], TestContext.CurrentContext.CancellationToken);

    Assert.That(orders, Is.Empty);
  }

  [Test]
  public async Task ReadOrdersOfStationOrdersAsync_SeveralStationOrders_AsksForTheirOrdersInOneRead()
  {
    GivenTheOrderIsStillThere();

    await _reader.ReadOrdersOfStationOrdersAsync([
                                                   _stationOrderId,
                                                   Guid.NewGuid()
                                                 ],
                                                 TestContext.CurrentContext.CancellationToken);

    A.CallTo(() => _repository.FindOrdersWithItemsAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public void ReadOrdersOfStationOrdersAsync_NullStationOrderIds_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _reader.ReadOrdersOfStationOrdersAsync(null!, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  private void GivenTheOrderIsStillThere()
  {
    Order order = new()
                  {
                    Id = _orderId,
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = Guid.NewGuid(),
                    GlobalOrderNumber = 4,
                    StaffMemberId = Guid.NewGuid(),
                    TableName = "Tisch 12",
                    CreatedAtUtc = _orderedAtUtc
                  };

    A.CallTo(() => _repository.FindOrdersWithItemsAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Order>>([order]));
  }
}
