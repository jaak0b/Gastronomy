using ErrorOr;
using FakeItEasy;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OpenItemsServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<IOpenItemRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _clock = new FakeTimeProvider(new(_now));

    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(RunningFestival()));
    A.CallTo(() => _repository.FindTableWithOrdersAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Table?>(null));

    _service = new(_repository, new(_festivalRepository, new(), _clock));
  }

  private readonly DateTime _now = new(2026, 9, 5, 20, 15, 0, DateTimeKind.Utc);
  private readonly DateTime _orderedAtUtc = new(2026, 9, 5, 19, 5, 0, DateTimeKind.Utc);
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");

  private int _ordersPlaced;

  private TimeProvider _clock = null!;
  private IFestivalRepository _festivalRepository = null!;
  private IOpenItemRepository _repository = null!;
  private OpenItemsService _service = null!;

  [Test]
  public async Task ReadTableWithOrdersAsync_NoFestivalIsRunning_IsRefusedAsNoRunningFestival()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));

    ErrorOr<Table> read = await _service.ReadTableWithOrdersAsync(Guid.NewGuid(), CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(read.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(read.RefusalMessageKey(), Is.EqualTo("errors.settlement.cannotBeProcessed"));
                      Assert.That(read.RefusalDescription(), Is.EqualTo(Refusal.Settlement.NoRunningFestival().Description));
                    });
    A.CallTo(() => _repository.FindTableWithOrdersAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task ReadTableWithOrdersAsync_ATableTheRunningFestivalDoesNotHold_IsRefusedAsNotFound()
  {
    var tableId = Guid.NewGuid();

    ErrorOr<Table> read = await _service.ReadTableWithOrdersAsync(tableId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(read.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
                      Assert.That(read.RefusalDescription(), Does.Contain(tableId.ToString()));
                    });
    A.CallTo(() => _repository.FindTableWithOrdersAsync(_festivalId, tableId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task ReadTableWithOrdersAsync_ATableOfTheRunningFestival_ReadsThatTableWithItsOrders()
  {
    var tableTwelve = TableNamed("Tisch 12");
    var bratwurst = At(tableTwelve, OpenItem("Bratwurst", 350));
    tableTwelve.Orders.Add(bratwurst.StationOrder.Order);
    A.CallTo(() => _repository.FindTableWithOrdersAsync(_festivalId, tableTwelve.Id, A<CancellationToken>._)).Returns(Task.FromResult<Table?>(tableTwelve));

    ErrorOr<Table> read = await _service.ReadTableWithOrdersAsync(tableTwelve.Id, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(read.Value, Is.SameAs(tableTwelve));
                      Assert.That(read.Value.Orders, Is.EqualTo(new[] { bratwurst.StationOrder.Order }));
                    });
  }

  private Table TableNamed(string name)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             FestivalId = _festivalId,
             AreaId = Guid.NewGuid(),
             Name = name,
             IsHidden = false
           };
  }

  private OrderItem At(Table table, OrderItem item)
  {
    _ordersPlaced++;

    Order order = new()
                  {
                    Id = Guid.NewGuid(),
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = _festivalId,
                    GlobalOrderNumber = _ordersPlaced,
                    StaffMemberId = Guid.NewGuid(),
                    TableId = table.Id,
                    CreatedAtUtc = _orderedAtUtc,
                    Table = table
                  };

    StationOrder stationOrder = new()
                                {
                                  Id = item.StationOrderId,
                                  OrderId = order.Id,
                                  FestivalId = _festivalId,
                                  StationId = Guid.NewGuid(),
                                  StationOrderNumber = _ordersPlaced,
                                  DeliveryMode = DeliveryMode.Together,
                                  Order = order
                                };

    stationOrder.Items.Add(item);
    order.StationOrders.Add(stationOrder);
    item.StationOrder = stationOrder;

    return item;
  }

  private OrderItem OpenItem(string itemName, int unitPriceCents)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             StationOrderId = Guid.NewGuid(),
             CatalogItemId = Guid.NewGuid(),
             ItemName = itemName,
             UnitPriceCents = unitPriceCents
           };
  }

  private Festival RunningFestival()
  {
    return new()
           {
             Id = _festivalId,
             Name = "Sommerfest",
             StartsAtUtc = _orderedAtUtc,
             EndsAtUtc = _now.AddHours(5),
             NextOrderNumber = 1,
             IsHidden = false
           };
  }
}
