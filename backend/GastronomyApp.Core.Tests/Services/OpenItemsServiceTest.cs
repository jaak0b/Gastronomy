using ErrorOr;
using FakeItEasy;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
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
    A.CallTo(() => _repository.FindOpenAtFestivalAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<OrderItem>>([]));
    A.CallTo(() => _repository.FindTableWithOrdersAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Table?>(null));

    _service = new(_repository, new(_festivalRepository, new(), _clock), NullLogger<OpenItemsService>.Instance);
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
  public async Task ReadOpenItemsAsync_NoFestivalIsRunning_ReadsNothing()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));

    Assert.That(await _service.ReadOpenItemsAsync(CancellationToken.None), Is.Empty);
  }

  [Test]
  public async Task ReadOpenItemsAsync_AFestivalIsRunning_ReadsTheOpenItemsOfThatFestival()
  {
    var bier = At(TableNamed("Tisch 3"), OpenItem("Bier", 400));
    GivenOpenItems(bier);

    Assert.That(await _service.ReadOpenItemsAsync(CancellationToken.None), Is.EqualTo(new[] { bier }));
  }

  [Test]
  public void GroupItemsWithAKnownOrderByTable_OpenItemsAtTwoTables_PutsEachTableOnceInTableNameOrder()
  {
    var tableThree = TableNamed("Tisch 3");
    var tableTwelve = TableNamed("Tisch 12");
    var bier = At(tableThree, OpenItem("Bier", 400));
    var bratwurst = At(tableTwelve, OpenItem("Bratwurst", 350));
    var limonade = At(tableTwelve, OpenItem("Limonade", 250));

    IReadOnlyList<IGrouping<Guid, OrderItem>> tables = _service.GroupItemsWithAKnownOrderByTable([
                                                                                                   bier,
                                                                                                   bratwurst,
                                                                                                   limonade
                                                                                                 ]);

    Assert.Multiple(() =>
                    {
                      Assert.That(tables.Select(table => table.Key),
                                  Is.EqualTo(new[]
                                             {
                                               tableTwelve.Id,
                                               tableThree.Id
                                             }));
                      Assert.That(tables[0],
                                  Is.EqualTo(new[]
                                             {
                                               bratwurst,
                                               limonade
                                             }));
                      Assert.That(tables[1], Is.EqualTo(new[] { bier }));
                    });
  }

  [Test]
  public void GroupItemsWithAKnownOrderByTable_ItemsOfSeveralOrders_SortsThemByOrderNumberAndThenByName()
  {
    var tableTwelve = TableNamed("Tisch 12");
    var limonade = At(tableTwelve, OpenItem("Limonade", 250));
    var bratwurst = At(tableTwelve, OpenItem("Bratwurst", 350));
    var bier = SameOrderAs(bratwurst, OpenItem("Bier", 400));

    IReadOnlyList<IGrouping<Guid, OrderItem>> tables = _service.GroupItemsWithAKnownOrderByTable([
                                                                                                   bratwurst,
                                                                                                   limonade,
                                                                                                   bier
                                                                                                 ]);

    Assert.That(tables[0],
                Is.EqualTo(new[]
                           {
                             limonade,
                             bier,
                             bratwurst
                           }));
  }

  [Test]
  public void GroupItemsWithAKnownOrderByTable_TwoUnitsOfOneArticleWithDifferentNotes_KeepsThemAsTwoLines()
  {
    var plainBratwurst = At(TableNamed("Tisch 12"), OpenItem("Bratwurst", 350));
    var notedBratwurst = SameOrderAs(plainBratwurst, OpenItem("Bratwurst", 350));
    notedBratwurst.Note = "Ohne Ketchup";

    IReadOnlyList<IGrouping<Guid, OrderItem>> tables = _service.GroupItemsWithAKnownOrderByTable([
                                                                                                   plainBratwurst,
                                                                                                   notedBratwurst
                                                                                                 ]);

    Assert.Multiple(() =>
                    {
                      Assert.That(tables[0].Count(), Is.EqualTo(2));
                      Assert.That(tables[0].Select(item => item.Note),
                                  Is.EquivalentTo(new[]
                                                  {
                                                    null,
                                                    "Ohne Ketchup"
                                                  }));
                    });
  }

  [Test]
  public void GroupItemsWithAKnownOrderByTable_AnItemWhoseOrderCannotBeFound_LeavesItOutOfTheTables()
  {
    var stray = OpenItem("Bratwurst", 350);

    Assert.That(_service.GroupItemsWithAKnownOrderByTable([stray]), Is.Empty);
  }

  [Test]
  public void ItemIdsWithoutAnOrder_AnItemWhoseOrderCannotBeFound_NamesThatItem()
  {
    var stray = OpenItem("Bratwurst", 350);
    var bier = At(TableNamed("Tisch 3"), OpenItem("Bier", 400));

    Assert.That(_service.ItemIdsWithoutAnOrder([
                                                 stray,
                                                 bier
                                               ]),
                Is.EqualTo(new[] { stray.Id }));
  }

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

  private void GivenOpenItems(params OrderItem[] items)
  {
    A.CallTo(() => _repository.FindOpenAtFestivalAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<OrderItem>>(items.ToList()));
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

  private OrderItem SameOrderAs(OrderItem placedItem, OrderItem item)
  {
    var stationOrder = placedItem.StationOrder;

    item.StationOrderId = stationOrder.Id;
    item.StationOrder = stationOrder;
    stationOrder.Items.Add(item);

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
