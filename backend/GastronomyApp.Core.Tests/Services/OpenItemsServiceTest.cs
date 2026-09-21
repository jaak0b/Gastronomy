using FakeItEasy;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OpenItemsServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<IOpenItemRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _clock = A.Fake<IClock>();

    A.CallTo(() => _clock.UtcNow).Returns(_now);
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(RunningFestival()));
    A.CallTo(() => _repository.FindOpenAtFestivalAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<OrderItem>>([]));
    A.CallTo(() => _repository.FindTableNamesAtFestivalAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<string>>([]));
    A.CallTo(() => _repository.FindTableOrdersAsync(A<Guid>._, A<string>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<TableOrderRecord>>([]));

    RunningFestivalLookup runningFestival = new(_festivalRepository, new(), _clock);

    _service = new(_repository, runningFestival, new(_repository, runningFestival, A.Fake<ITransactionRunner>(), _clock));
  }

  private readonly DateTime _now = new(2026, 9, 5, 20, 15, 0, DateTimeKind.Utc);
  private readonly DateTime _orderedAtUtc = new(2026, 9, 5, 19, 5, 0, DateTimeKind.Utc);
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");

  private int _ordersPlaced;

  private IClock _clock = null!;
  private IFestivalRepository _festivalRepository = null!;
  private IOpenItemRepository _repository = null!;
  private OpenItemsService _service = null!;

  [Test]
  public async Task ReadAsync_NoFestivalIsRunning_ReportsNoTables()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));

    var report = await _service.ReadAsync(CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(report.Tables, Is.Empty);
                      Assert.That(report.OrderItemIdsWithoutAnOrder, Is.Empty);
                    });
  }

  [Test]
  public async Task ReadAsync_OpenItemsAtTwoTables_ReportsEachTableWithWhatItStillOwes()
  {
    GivenOpenItems(At("Tisch 3", OpenItem("Bier", 400)), At("Tisch 12", OpenItem("Bratwurst", 350)), At("Tisch 12", OpenItem("Limonade", 250)));

    var report = await _service.ReadAsync(CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(report.Tables.Select(table => table.TableName),
                                  Is.EqualTo(new[]
                                             {
                                               "Tisch 12",
                                               "Tisch 3"
                                             }));
                      Assert.That(report.Tables[0].OpenAmountCents, Is.EqualTo(600));
                      Assert.That(report.Tables[1].OpenAmountCents, Is.EqualTo(400));
                    });
  }

  [Test]
  public async Task ReadAsync_AnItemWhoseOrderCannotBeFound_LeavesItOutOfTheTablesAndNamesIt()
  {
    var stray = OpenItem("Bratwurst", 350);
    GivenOpenItems(stray);

    var report = await _service.ReadAsync(CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(report.Tables, Is.Empty);
                      Assert.That(report.OrderItemIdsWithoutAnOrder, Is.EqualTo(new[] { stray.Id }));
                    });
  }

  [Test]
  public async Task ReadAsync_ItemsOfOneOrder_CarryTheNoteTheWaiterTypedForThem()
  {
    var bratwurst = At("Tisch 12", OpenItem("Bratwurst", 350));
    bratwurst.Note = "Ohne Ketchup";
    GivenOpenItems(bratwurst);

    var report = await _service.ReadAsync(CancellationToken.None);

    Assert.That(report.Tables[0].Items[0].Note, Is.EqualTo("Ohne Ketchup"));
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
  public async Task ReadTableAsync_NoFestivalIsRunning_ReportsTheTableWithNothingOpen()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));

    var report = await _service.ReadTableAsync("Tisch 12", CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(report.TableName, Is.EqualTo("Tisch 12"));
                      Assert.That(report.OpenAmountCents, Is.Zero);
                      Assert.That(report.Orders, Is.Empty);
                    });
  }

  [Test]
  public async Task ReadTableAsync_AnUnknownTableName_ReportsNoOrdersAndNothingOpen()
  {
    var report = await _service.ReadTableAsync("Tisch 99", CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(report.TableName, Is.EqualTo("Tisch 99"));
                      Assert.That(report.OpenAmountCents, Is.Zero);
                      Assert.That(report.Orders, Is.Empty);
                    });
  }

  [Test]
  public async Task ReadTableAsync_AWhitespaceTableName_ReportsNoOrdersAndNothingOpen()
  {
    var report = await _service.ReadTableAsync("   ", CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(report.TableName, Is.EqualTo("   "));
                      Assert.That(report.OpenAmountCents, Is.Zero);
                      Assert.That(report.Orders, Is.Empty);
                    });
  }

  [Test]
  public async Task ReadTableAsync_AnOrderWithAnOpenAndASettledPosition_AddsUpOnlyWhatIsStillOpen()
  {
    var order = new TableOrderRecord
                {
                  OrderId = Guid.NewGuid(),
                  GlobalOrderNumber = 1,
                  CreatedAtUtc = _orderedAtUtc,
                  StaffMemberName = "Anna",
                  Items =
                  [
                    Position("Bratwurst", 350),
                    Position("Limonade", 250, _now)
                  ]
                };
    A.CallTo(() => _repository.FindTableOrdersAsync(_festivalId, "Tisch 12", A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<TableOrderRecord>>([order]));

    var report = await _service.ReadTableAsync("Tisch 12", CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(report.TableName, Is.EqualTo("Tisch 12"));
                      Assert.That(report.OpenAmountCents, Is.EqualTo(350));
                      Assert.That(report.Orders, Has.Count.EqualTo(1));
                      Assert.That(report.Orders[0].StaffMemberName, Is.EqualTo("Anna"));
                      Assert.That(report.Orders[0].Items.Select(item => item.ItemName),
                                  Is.EqualTo(new[]
                                             {
                                               "Bratwurst",
                                               "Limonade"
                                             }));
                    });
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

  private TableOrderRecordItem Position(string itemName, int unitPriceCents, DateTime? settledAtUtc = null)
  {
    return new()
           {
             OrderItemId = Guid.NewGuid(),
             OrderId = Guid.NewGuid(),
             GlobalOrderNumber = 1,
             ItemName = itemName,
             Note = null,
             UnitPriceCents = unitPriceCents,
             OrderedAtUtc = _orderedAtUtc,
             FulfilledAtUtc = null,
             SettledAtUtc = settledAtUtc
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
