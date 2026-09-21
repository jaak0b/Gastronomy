using FakeItEasy;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
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
    A.CallTo(() => _repository.FindTableNamesAtFestivalAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<string>>([]));
    A.CallTo(() => _repository.FindTableOrdersAsync(A<Guid>._, A<string>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Order>>([]));

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
  public async Task ReadOpenItemsAsync_NoFestivalIsRunning_ReadsNothing()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));

    Assert.That(await _service.ReadOpenItemsAsync(CancellationToken.None), Is.Empty);
  }

  [Test]
  public async Task ReadOpenItemsAsync_AFestivalIsRunning_ReadsTheOpenItemsOfThatFestival()
  {
    var bier = At("Tisch 3", OpenItem("Bier", 400));
    GivenOpenItems(bier);

    Assert.That(await _service.ReadOpenItemsAsync(CancellationToken.None), Is.EqualTo(new[] { bier }));
  }

  [Test]
  public void GroupItemsWithAKnownOrderByTableName_OpenItemsAtTwoTables_PutsEachTableOnceInTableNameOrder()
  {
    var bier = At("Tisch 3", OpenItem("Bier", 400));
    var bratwurst = At("Tisch 12", OpenItem("Bratwurst", 350));
    var limonade = At("Tisch 12", OpenItem("Limonade", 250));

    IReadOnlyList<IGrouping<string, OrderItem>> tables = _service.GroupItemsWithAKnownOrderByTableName([
                                                                                                         bier,
                                                                                                         bratwurst,
                                                                                                         limonade
                                                                                                       ]);

    Assert.Multiple(() =>
                    {
                      Assert.That(tables.Select(table => table.Key),
                                  Is.EqualTo(new[]
                                             {
                                               "Tisch 12",
                                               "Tisch 3"
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
  public void GroupItemsWithAKnownOrderByTableName_ItemsOfSeveralOrders_SortsThemByOrderNumberAndThenByName()
  {
    var limonade = At("Tisch 12", OpenItem("Limonade", 250));
    var bratwurst = At("Tisch 12", OpenItem("Bratwurst", 350));
    var bier = SameOrderAs(bratwurst, OpenItem("Bier", 400));

    IReadOnlyList<IGrouping<string, OrderItem>> tables = _service.GroupItemsWithAKnownOrderByTableName([
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
  public void GroupItemsWithAKnownOrderByTableName_TwoUnitsOfOneArticleWithDifferentNotes_KeepsThemAsTwoLines()
  {
    var plainBratwurst = At("Tisch 12", OpenItem("Bratwurst", 350));
    var notedBratwurst = SameOrderAs(plainBratwurst, OpenItem("Bratwurst", 350));
    notedBratwurst.Note = "Ohne Ketchup";

    IReadOnlyList<IGrouping<string, OrderItem>> tables = _service.GroupItemsWithAKnownOrderByTableName([
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
  public void GroupItemsWithAKnownOrderByTableName_AnItemWhoseOrderCannotBeFound_LeavesItOutOfTheTables()
  {
    var stray = OpenItem("Bratwurst", 350);

    Assert.That(_service.GroupItemsWithAKnownOrderByTableName([stray]), Is.Empty);
  }

  [Test]
  public void ItemIdsWithoutAnOrder_AnItemWhoseOrderCannotBeFound_NamesThatItem()
  {
    var stray = OpenItem("Bratwurst", 350);
    var bier = At("Tisch 3", OpenItem("Bier", 400));

    Assert.That(_service.ItemIdsWithoutAnOrder([
                                                 stray,
                                                 bier
                                               ]),
                Is.EqualTo(new[] { stray.Id }));
  }

  [Test]
  public async Task ReadTableNamesAsync_NoFestivalIsRunning_ReportsNoNames()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));

    Assert.That(await _service.ReadTableNamesAsync(CancellationToken.None), Is.Empty);
  }

  [Test]
  public async Task ReadTableNamesAsync_AFestivalIsRunning_ReportsTheNamesUsedAtThatFestival()
  {
    A.CallTo(() => _repository.FindTableNamesAtFestivalAsync(_festivalId, A<CancellationToken>._))
   .Returns(Task.FromResult<IReadOnlyList<string>>([
                                                     "Tisch 12",
                                                     "Tisch 3"
                                                   ]));

    Assert.That(await _service.ReadTableNamesAsync(CancellationToken.None),
                Is.EqualTo(new[]
                           {
                             "Tisch 12",
                             "Tisch 3"
                           }));
  }

  [Test]
  public void ReadTableOrdersAsync_NullTableName_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _service.ReadTableOrdersAsync(null!, CancellationToken.None), Throws.ArgumentNullException);
  }

  [Test]
  public async Task ReadTableOrdersAsync_NoFestivalIsRunning_ReadsNoOrders()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));

    Assert.That(await _service.ReadTableOrdersAsync("Tisch 12", CancellationToken.None), Is.Empty);
  }

  [Test]
  public async Task ReadTableOrdersAsync_AWhitespaceTableName_ReadsNoOrders()
  {
    Assert.That(await _service.ReadTableOrdersAsync("   ", CancellationToken.None), Is.Empty);

    A.CallTo(() => _repository.FindTableOrdersAsync(A<Guid>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task ReadTableOrdersAsync_AnUnknownTableName_ReadsNoOrders()
  {
    Assert.That(await _service.ReadTableOrdersAsync("Tisch 99", CancellationToken.None), Is.Empty);
  }

  [Test]
  public async Task ReadTableOrdersAsync_ATableWithOrders_ReadsTheOrdersOfThatTable()
  {
    var bratwurst = At("Tisch 12", OpenItem("Bratwurst", 350));
    var order = bratwurst.StationOrder.Order;
    A.CallTo(() => _repository.FindTableOrdersAsync(_festivalId, "Tisch 12", A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Order>>([order]));

    Assert.That(await _service.ReadTableOrdersAsync("Tisch 12", CancellationToken.None), Is.EqualTo(new[] { order }));
  }

  private void GivenOpenItems(params OrderItem[] items)
  {
    A.CallTo(() => _repository.FindOpenAtFestivalAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<OrderItem>>(items.ToList()));
  }

  private OrderItem At(string tableName, OrderItem item)
  {
    _ordersPlaced++;

    Order order = new()
                  {
                    Id = Guid.NewGuid(),
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = _festivalId,
                    GlobalOrderNumber = _ordersPlaced,
                    StaffMemberId = Guid.NewGuid(),
                    TableName = tableName,
                    CreatedAtUtc = _orderedAtUtc
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
