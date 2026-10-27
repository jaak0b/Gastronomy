using ErrorOr;
using FakeItEasy;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.OpenItems;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class OpenItemsServiceTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _clock = new(DateTimeOffset.UtcNow);
    _logger = A.Fake<ILogger<OpenItemsService>>();
    _service = CreateService(_fixture.DbContext);

    _fixture.DbContext.StaffMembers.Add(new()
                                        {
                                          Id = _anotherWaiter,
                                          Name = "Berta",
                                          IsActive = true,
                                          CreatedAtUtc = _orderedAtUtc
                                        });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private readonly DateTime _orderedAtUtc = new(2026, 8, 27, 18, 30, 0, DateTimeKind.Utc);
  private readonly Guid _anotherWaiter = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
  private readonly OrderItemStatusSeeder _statusSeeder = new();

  private FakeTimeProvider _clock = null!;
  private SqliteInMemoryFixture _fixture = null!;
  private ILogger<OpenItemsService> _logger = null!;
  private SeededDomain _seeded = null!;
  private OpenItemsService _service = null!;

  [Test]
  public async Task ReadTableAsync_NoFestivalIsRunning_IsRefusedAsNoRunningFestival()
  {
    var tableId = await TableAsync("Tisch 12");
    await EndTheFestivalAsync();

    ErrorOr<TableOrderReportView> read = await _service.ReadTableAsync(tableId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(read.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(read.RefusalMessageKey(), Is.EqualTo("errors.settlement.cannotBeProcessed"));
                      Assert.That(read.RefusalDescription(), Is.EqualTo(Refusal.Settlement.NoRunningFestival().Description));
                    });
  }

  [Test]
  public async Task ReadTableAsync_ATableThatDoesNotExist_IsRefusedAsNotFound()
  {
    var tableId = Guid.NewGuid();

    ErrorOr<TableOrderReportView> read = await _service.ReadTableAsync(tableId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(read.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
                      Assert.That(read.RefusalDescription(), Does.Contain(tableId.ToString()));
                    });
  }

  [Test]
  public async Task ReadTableAsync_ATableOfAnotherFestival_IsRefusedAsNotFound()
  {
    var lastYearFestivalId = await AddFestivalAsync("Sommerfest im Vorjahr");
    await PlaceOrderAsync("Tisch 1", 1, _orderedAtUtc, lastYearFestivalId, Line("Bratwurst", 350));
    var lastYearTableId = await TableAsync("Tisch 1", lastYearFestivalId);

    ErrorOr<TableOrderReportView> read = await _service.ReadTableAsync(lastYearTableId, TestContext.CurrentContext.CancellationToken);

    Assert.That(read.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  [Test]
  public async Task ReadTableAsync_ATableNobodyHasOrderedAt_ComesBackWithoutOrdersAndWithNothingOwed()
  {
    var tableId = await TableAsync("Tisch 5");

    ErrorOr<TableOrderReportView> read = await _service.ReadTableAsync(tableId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(read.IsError, Is.False);
                      Assert.That(read.Value.TableId, Is.EqualTo(tableId));
                      Assert.That(read.Value.TableName, Is.EqualTo("Tisch 5"));
                      Assert.That(read.Value.OpenAmountCents, Is.Zero);
                      Assert.That(read.Value.Orders, Is.Empty);
                    });
  }

  [Test]
  public async Task ReadTableAsync_AHiddenTableOfTheRunningFestival_IsStillAnswered()
  {
    var tableId = await TableAsync("Tisch 5");
    var table = await _fixture.DbContext.Tables.SingleAsync(candidate => candidate.Id == tableId, TestContext.CurrentContext.CancellationToken);
    table.IsHidden = true;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<TableOrderReportView> read = await _service.ReadTableAsync(tableId, TestContext.CurrentContext.CancellationToken);

    Assert.That(read.IsError, Is.False);
  }

  [Test]
  public async Task ReadTableAsync_TwoOrdersOfOneTable_ComeBackNewestFirstWithEveryPositionAndItsState()
  {
    var olderItems = await PlaceOrderAsync("T1", 1, _orderedAtUtc, Line("Bratwurst", 350), Line("Limonade", 250));
    var newestItems = await PlaceOrderAsync("T1", 3, _orderedAtUtc.AddMinutes(15), Line("Bratwurst", 350), Line("Limonade", 250));
    var sameTimeItems = await PlaceOrderAsync("T1", 2, _orderedAtUtc.AddMinutes(15), Line("Bier", 300));
    await PlaceOrderAsync("Tisch 3", 4, _orderedAtUtc.AddMinutes(30), Line("Bratwurst", 350));
    await StampAsync(newestItems.Single(item => item.ItemName == "Bratwurst"), handedOutAtUtc: _orderedAtUtc.AddMinutes(20));
    await StampAsync(newestItems.Single(item => item.ItemName == "Limonade"), settledAtUtc: _orderedAtUtc.AddMinutes(25));
    var tableId = await TableAsync("T1");

    ErrorOr<TableOrderReportView> read = await _service.ReadTableAsync(tableId, TestContext.CurrentContext.CancellationToken);

    var orders = read.Value.Orders;
    var newestOrderItems = orders[0].Items;

    Assert.Multiple(() =>
                    {
                      Assert.That(read.Value.TableName, Is.EqualTo("T1"));
                      Assert.That(orders.Select(order => order.GlobalOrderNumber),
                                  Is.EqualTo(new[]
                                             {
                                               3,
                                               2,
                                               1
                                             }));
                      Assert.That(orders.Select(order => order.StaffMemberName), Is.All.EqualTo("Anna"));
                      Assert.That(orders[0].CreatedAtUtc, Is.EqualTo(_orderedAtUtc.AddMinutes(15)));
                      Assert.That(newestOrderItems.Select(item => item.OrderItemId), Is.EquivalentTo(newestItems.Select(item => item.Id)));
                      Assert.That(newestOrderItems.Single(item => item.ItemName == "Bratwurst").State, Is.EqualTo(OrderItemState.HandedOut));
                      Assert.That(newestOrderItems.Single(item => item.ItemName == "Bratwurst").StateSetAtUtc, Is.EqualTo(_orderedAtUtc.AddMinutes(20)));
                      Assert.That(newestOrderItems.Single(item => item.ItemName == "Limonade").State, Is.EqualTo(OrderItemState.Open));
                      Assert.That(newestOrderItems.Single(item => item.ItemName == "Limonade").SettledAtUtc, Is.EqualTo(_orderedAtUtc.AddMinutes(25)));
                      Assert.That(newestOrderItems.Single(item => item.ItemName == "Limonade").UnitPriceCents, Is.EqualTo(250));
                      Assert.That(newestOrderItems.Select(item => item.OrderId), Is.All.EqualTo(orders[0].OrderId));
                      Assert.That(newestOrderItems.Select(item => item.GlobalOrderNumber), Is.All.EqualTo(3));
                      Assert.That(orders[1].Items.Select(item => item.OrderItemId), Is.EqualTo(sameTimeItems.Select(item => item.Id)));
                      Assert.That(orders[2].Items.Select(item => item.OrderItemId), Is.EquivalentTo(olderItems.Select(item => item.Id)));
                    });
  }

  [Test]
  public async Task ReadTableAsync_ItemsSettledAndNotSettled_OwesOnlyThePricesOfTheOpenOnes()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350), Line("Bier", 400), Line("Limonade", 250));
    await StampAsync(items.Single(item => item.ItemName == "Bier"), settledAtUtc: _orderedAtUtc.AddMinutes(5));
    var tableId = await TableAsync("Tisch 12");

    ErrorOr<TableOrderReportView> read = await _service.ReadTableAsync(tableId, TestContext.CurrentContext.CancellationToken);

    Assert.That(read.Value.OpenAmountCents, Is.EqualTo(600));
  }

  [Test]
  public async Task ReadTableAsync_TwoUnitsOfOneArticleWithDifferentNotes_StayTwoLinesEachCarryingItsNote()
  {
    await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Hotdog", 400, "Ohne Ketchup"), Line("Hotdog", 400));
    var tableId = await TableAsync("Tisch 12");

    ErrorOr<TableOrderReportView> read = await _service.ReadTableAsync(tableId, TestContext.CurrentContext.CancellationToken);

    Assert.That(read.Value.Orders.Single().Items.Select(item => item.Note),
                Is.EqualTo(new string?[]
                           {
                             null,
                             "Ohne Ketchup"
                           }));
  }

  [Test]
  public async Task ReadTableAsync_ItemsOfOneOrder_ComeBackByNameThenNoteWithCapitalsIgnoredAndUmlautsAfterZ()
  {
    await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Zwiebel", 100), Line("Äpfel", 100), Line("apfel", 100), Line("Birne", 100), Line("Käsekrainer", 100, "Ohne Ketchup"), Line("Käsekrainer", 100));
    var tableId = await TableAsync("Tisch 12");

    ErrorOr<TableOrderReportView> read = await _service.ReadTableAsync(tableId, TestContext.CurrentContext.CancellationToken);

    Assert.That(read.Value.Orders.Single().Items.Select(item => item.ItemName),
                Is.EqualTo(new[]
                           {
                             "apfel",
                             "Birne",
                             "Käsekrainer",
                             "Käsekrainer",
                             "Zwiebel",
                             "Äpfel"
                           }));
  }

  [Test]
  public async Task ReadTableAsync_AnOrderSplitAcrossTwoStations_HoldsThePositionsOfBothStations()
  {
    var kitchenItems = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350));
    var barItems = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, _seeded.FestivalId, _seeded.BarStationId, 2, Line("Bier", 400));
    var tableId = await TableAsync("Tisch 12");

    ErrorOr<TableOrderReportView> read = await _service.ReadTableAsync(tableId, TestContext.CurrentContext.CancellationToken);

    Assert.That(read.Value.Orders.SelectMany(order => order.Items).Select(item => item.OrderItemId), Is.EquivalentTo(kitchenItems.Concat(barItems).Select(item => item.Id)));
  }

  [Test]
  public async Task ReadTableAsync_ATableWithOrders_ReadsInTwoStatements()
  {
    await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350), Line("Bier", 400));
    await PlaceOrderAsync("Tisch 12", 2, _orderedAtUtc.AddMinutes(5), Line("Limonade", 250));
    var tableId = await TableAsync("Tisch 12");
    QueryCounter counter = new();
    await using GastronomyAppDbContext readContext = new(new DbContextOptionsBuilder<GastronomyAppDbContext>().UseSqlite(_fixture.Connection).AddInterceptors(counter).Options);

    await CreateService(readContext).ReadTableAsync(tableId, TestContext.CurrentContext.CancellationToken);

    Assert.That(counter.QueryCount, Is.EqualTo(2));
  }

  [Test]
  public void SettleAsync_NullLines_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _service.SettleAsync(null!, PaymentMethod.Cash, _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public async Task SettleAsync_NoFestivalIsRunning_RefusesTheSettlementAndStoresNothing()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350));
    await EndTheFestivalAsync();

    ErrorOr<SettlementView> settlement = await _service.SettleAsync([Pay(items[0], 350)], PaymentMethod.Cash, _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsError, Is.True);
                      Assert.That(settlement.RefusalMessageKey(), Is.EqualTo("errors.settlement.cannotBeProcessed"));
                    });
    Assert.That((await StoredAsync(items[0])).SettledAtUtc, Is.Null);
  }

  [Test]
  public async Task SettleAsync_OnePricePerLine_StoresExactlyThePricesThePhoneSentAndAnswersWhichWereSettled()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350), Line("Bier", 400));

    ErrorOr<SettlementView> settlement = await _service.SettleAsync([
                                                                      Pay(items[0], 200, "Stammgast"),
                                                                      Pay(items[1], 150, "Stammgast")
                                                                    ],
                                                                    PaymentMethod.Cash,
                                                                    _seeded.StaffMemberId,
                                                                    TestContext.CurrentContext.CancellationToken);

    var first = await StoredAsync(items[0]);
    var second = await StoredAsync(items[1]);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsError, Is.False);
                      Assert.That(settlement.Value.SettledOrderItemIds, Is.EquivalentTo(items.Select(item => item.Id)));
                      Assert.That(settlement.Value.ReappliedOrderItemIds, Is.Empty);
                      Assert.That(settlement.Value.AlreadySettledByOthersOrderItemIds, Is.Empty);
                      Assert.That(first.ChargedPriceCents, Is.EqualTo(200));
                      Assert.That(second.ChargedPriceCents, Is.EqualTo(150));
                      Assert.That(first.PaymentNotice, Is.EqualTo("Stammgast"));
                      Assert.That(second.PaymentNotice, Is.EqualTo("Stammgast"));
                      Assert.That(first.SettledAtUtc, Is.EqualTo(_clock.GetUtcNow().UtcDateTime));
                      Assert.That(second.SettledAtUtc, Is.EqualTo(_clock.GetUtcNow().UtcDateTime));
                      Assert.That(first.SettledByStaffMemberId, Is.EqualTo(_seeded.StaffMemberId));
                      Assert.That(second.SettledByStaffMemberId, Is.EqualTo(_seeded.StaffMemberId));
                    });
  }

  [Test]
  public async Task SettleAsync_MoreThanTheItemCosts_StoresTheSentPriceAsItIs()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350));

    ErrorOr<SettlementView> settlement = await _service.SettleAsync([Pay(items[0], 500)], PaymentMethod.Cash, _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    var stored = await StoredAsync(items[0]);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsError, Is.False);
                      Assert.That(stored.ChargedPriceCents, Is.EqualTo(500));
                      Assert.That(stored.PaymentNotice, Is.Null);
                    });
  }

  [Test]
  public async Task SettleAsync_ALineTheCallerAlreadySettled_OverwritesThePriceAndTheReasonAndKeepsTimeAndCollector()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bier", 400));
    var earlier = _orderedAtUtc.AddMinutes(5);
    await SettleByAsync(items[0], _seeded.StaffMemberId, earlier, 0, "Kapelle", PaymentMethod.None);

    ErrorOr<SettlementView> settlement = await _service.SettleAsync([Pay(items[0], 400)], PaymentMethod.Cash, _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    var stored = await StoredAsync(items[0]);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.Value.SettledOrderItemIds, Is.Empty);
                      Assert.That(settlement.Value.ReappliedOrderItemIds, Is.EqualTo(new[] { items[0].Id }));
                      Assert.That(settlement.Value.AlreadySettledByOthersOrderItemIds, Is.Empty);
                      Assert.That(stored.ChargedPriceCents, Is.EqualTo(400));
                      Assert.That(stored.PaymentNotice, Is.Null);
                      Assert.That(stored.SettledAtUtc, Is.EqualTo(earlier));
                      Assert.That(stored.SettledByStaffMemberId, Is.EqualTo(_seeded.StaffMemberId));
                    });
  }

  [Test]
  public async Task SettleAsync_ALineSomebodyElseAlreadySettled_IsNeverChangedAndIsReportedBack()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bier", 400));
    var earlier = _orderedAtUtc.AddMinutes(5);
    await SettleByAsync(items[0], _anotherWaiter, earlier, 0, "Kapelle", PaymentMethod.None);

    ErrorOr<SettlementView> settlement = await _service.SettleAsync([Pay(items[0], 400)], PaymentMethod.Cash, _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    var stored = await StoredAsync(items[0]);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.Value.SettledOrderItemIds, Is.Empty);
                      Assert.That(settlement.Value.ReappliedOrderItemIds, Is.Empty);
                      Assert.That(settlement.Value.AlreadySettledByOthersOrderItemIds, Is.EqualTo(new[] { items[0].Id }));
                      Assert.That(stored.ChargedPriceCents, Is.Zero);
                      Assert.That(stored.PaymentNotice, Is.EqualTo("Kapelle"));
                      Assert.That(stored.SettledAtUtc, Is.EqualTo(earlier));
                      Assert.That(stored.SettledByStaffMemberId, Is.EqualTo(_anotherWaiter));
                    });
  }

  [Test]
  public async Task SettleAsync_AnOpenLineBesideALineSomebodyElseAlreadySettled_SettlesOnlyTheOpenLine()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bier", 400), Line("Bratwurst", 350));
    await SettleByAsync(items[0], _anotherWaiter, _orderedAtUtc.AddMinutes(5), 400, null, PaymentMethod.Cash);

    ErrorOr<SettlementView> settlement = await _service.SettleAsync([
                                                                      Pay(items[0], 400),
                                                                      Pay(items[1], 350)
                                                                    ],
                                                                    PaymentMethod.Cash,
                                                                    _seeded.StaffMemberId,
                                                                    TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.Value.SettledOrderItemIds, Is.EqualTo(new[] { items[1].Id }));
                      Assert.That(settlement.Value.AlreadySettledByOthersOrderItemIds, Is.EqualTo(new[] { items[0].Id }));
                    });
  }

  [Test]
  public async Task SettleAsync_AStaleSelectionWithOneLineTakenByAColleague_ChargesTheOpenLinesExactlyThePricesSentAndLeavesTheColleaguesLineUntouched()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bier", 350), Line("Bier", 350), Line("Hotdog", 400));
    var earlier = _orderedAtUtc.AddMinutes(5);
    await SettleByAsync(items[0], _anotherWaiter, earlier, 0, "Kapelle", PaymentMethod.None);

    ErrorOr<SettlementView> settlement = await _service.SettleAsync([
                                                                      Pay(items[0], 350),
                                                                      Pay(items[1], 100, "Der Tisch zahlt den Rest spaeter"),
                                                                      Pay(items[2], 500)
                                                                    ],
                                                                    PaymentMethod.Cash,
                                                                    _seeded.StaffMemberId,
                                                                    TestContext.CurrentContext.CancellationToken);

    var colleagues = await StoredAsync(items[0]);
    var second = await StoredAsync(items[1]);
    var third = await StoredAsync(items[2]);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.Value.SettledOrderItemIds, Has.Count.EqualTo(2));
                      Assert.That(settlement.Value.ReappliedOrderItemIds, Is.Empty);
                      Assert.That(settlement.Value.AlreadySettledByOthersOrderItemIds, Is.EqualTo(new[] { items[0].Id }));
                      Assert.That(colleagues.ChargedPriceCents, Is.Zero);
                      Assert.That(colleagues.PaymentNotice, Is.EqualTo("Kapelle"));
                      Assert.That(colleagues.SettledAtUtc, Is.EqualTo(earlier));
                      Assert.That(colleagues.SettledByStaffMemberId, Is.EqualTo(_anotherWaiter));
                      Assert.That(second.ChargedPriceCents, Is.EqualTo(100));
                      Assert.That(second.PaymentNotice, Is.EqualTo("Der Tisch zahlt den Rest spaeter"));
                      Assert.That(third.ChargedPriceCents, Is.EqualTo(500));
                    });
  }

  [Test]
  public async Task SettleAsync_TheSameRequestSentAgainAfterALostAnswer_SettlesNothingTwice()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350));
    await _service.SettleAsync([Pay(items[0], 350)], PaymentMethod.Cash, _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    var firstSettledAt = (await StoredAsync(items[0])).SettledAtUtc;
    _clock.Advance(TimeSpan.FromMinutes(3));

    ErrorOr<SettlementView> again = await _service.SettleAsync([Pay(items[0], 350)], PaymentMethod.Cash, _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(again.Value.SettledOrderItemIds, Is.Empty);
                      Assert.That(again.Value.ReappliedOrderItemIds, Is.EqualTo(new[] { items[0].Id }));
                    });
    Assert.That((await StoredAsync(items[0])).SettledAtUtc, Is.EqualTo(firstSettledAt));
  }

  [Test]
  public async Task SettleAsync_AShortLineWithoutAReason_IsRefusedNamingTheItemAndNothingIsStored()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350));

    ErrorOr<SettlementView> settlement = await _service.SettleAsync([Pay(items[0], 300, "")], PaymentMethod.Cash, _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.IsError, Is.True);
                      Assert.That(settlement.RefusalMessageKey(), Is.EqualTo("errors.settlement.cannotBeProcessed"));
                      Assert.That(settlement.RefusalDescription(), Does.Contain(items[0].Id.ToString()));
                    });
    Assert.That((await StoredAsync(items[0])).SettledAtUtc, Is.Null);
  }

  [Test]
  public async Task SettleAsync_AShortLineWithNoReasonAtAll_IsRefusedAndNothingIsStored()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350));

    ErrorOr<SettlementView> settlement = await _service.SettleAsync([Pay(items[0], 300)], PaymentMethod.Cash, _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    Assert.That(settlement.RefusalMessageKey(), Is.EqualTo("errors.settlement.cannotBeProcessed"));
    Assert.That((await StoredAsync(items[0])).SettledAtUtc, Is.Null);
  }

  [Test]
  public async Task SettleAsync_ASelectionSpanningTwoTables_IsRefusedNamingBothTablesAndNothingIsStored()
  {
    var twelve = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350));
    var three = await PlaceOrderAsync("Tisch 3", 2, _orderedAtUtc, Line("Bier", 400));
    var tableTwelveId = await TableAsync("Tisch 12");
    var tableThreeId = await TableAsync("Tisch 3");

    ErrorOr<SettlementView> settlement = await _service.SettleAsync([
                                                                      Pay(twelve[0], 350),
                                                                      Pay(three[0], 400)
                                                                    ],
                                                                    PaymentMethod.Cash,
                                                                    _seeded.StaffMemberId,
                                                                    TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.RefusalMessageKey(), Is.EqualTo("errors.settlement.cannotBeProcessed"));
                      Assert.That(settlement.RefusalDescription(), Does.Contain($"{tableTwelveId}, {tableThreeId}"));
                    });
    Assert.That((await StoredAsync(twelve[0])).SettledAtUtc, Is.Null);
    Assert.That((await StoredAsync(three[0])).SettledAtUtc, Is.Null);
  }

  [Test]
  public async Task SettleAsync_ASelectionSpanningTwoTablesWhereOneLineIsAlreadySettled_IsRefusedAsWell()
  {
    var twelve = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350));
    var three = await PlaceOrderAsync("Tisch 3", 2, _orderedAtUtc, Line("Bier", 400));
    await SettleByAsync(three[0], _anotherWaiter, _orderedAtUtc.AddMinutes(5), 400, null, PaymentMethod.Cash);

    ErrorOr<SettlementView> settlement = await _service.SettleAsync([
                                                                      Pay(twelve[0], 350),
                                                                      Pay(three[0], 400)
                                                                    ],
                                                                    PaymentMethod.Cash,
                                                                    _seeded.StaffMemberId,
                                                                    TestContext.CurrentContext.CancellationToken);

    Assert.That(settlement.RefusalMessageKey(), Is.EqualTo("errors.settlement.cannotBeProcessed"));
    Assert.That((await StoredAsync(twelve[0])).SettledAtUtc, Is.Null);
  }

  [Test]
  public async Task SettleAsync_TheSameLineNamedTwice_IsRefusedAndNothingIsStored()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350));

    ErrorOr<SettlementView> settlement = await _service.SettleAsync([
                                                                      Pay(items[0], 350),
                                                                      Pay(items[0], 350)
                                                                    ],
                                                                    PaymentMethod.Cash,
                                                                    _seeded.StaffMemberId,
                                                                    TestContext.CurrentContext.CancellationToken);

    var stored = await StoredAsync(items[0]);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.RefusalMessageKey(), Is.EqualTo("errors.settlement.cannotBeProcessed"));
                      Assert.That(settlement.RefusalDescription(), Does.Contain(items[0].Id.ToString()));
                      Assert.That(stored.SettledAtUtc, Is.Null);
                      Assert.That(stored.ChargedPriceCents, Is.Null);
                    });
  }

  [Test]
  public async Task SettleAsync_ASelectionHoldingAnUnknownId_SettlesNoneOfTheSelection()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350));
    var unknownId = Guid.NewGuid();

    ErrorOr<SettlementView> settlement = await _service.SettleAsync([
                                                                      Pay(items[0], 350),
                                                                      new()
                                                                      {
                                                                        OrderItemId = unknownId,
                                                                        PaidPriceCents = 350
                                                                      }
                                                                    ],
                                                                    PaymentMethod.Cash,
                                                                    _seeded.StaffMemberId,
                                                                    TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.FirstError.NumericType, Is.EqualTo(RefusalType.UnprocessableEntity));
                      Assert.That(settlement.RefusalMessageKey(), Is.EqualTo("errors.settlement.unknownItem"));
                      Assert.That(settlement.RefusalMetadata("orderItemId"), Is.EqualTo(unknownId.ToString()));
                    });
    Assert.That((await StoredAsync(items[0])).SettledAtUtc, Is.Null);
  }

  [Test]
  public async Task SettleAsync_WithoutNamingTheWaiter_ThrowsAndNothingIsStored()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350));

    Assert.That(async () => await _service.SettleAsync([Pay(items[0], 350)], PaymentMethod.Cash, Guid.Empty, TestContext.CurrentContext.CancellationToken), Throws.ArgumentException);

    var stored = await StoredAsync(items[0]);

    Assert.Multiple(() =>
                    {
                      Assert.That(stored.SettledAtUtc, Is.Null);
                      Assert.That(stored.ChargedPriceCents, Is.Null);
                      Assert.That(stored.SettledByStaffMemberId, Is.Null);
                    });
  }

  [Test]
  public async Task SettleAsync_ItemsNobodySelected_LeaveTheirTimeAmountAndWaiterAllUnwritten()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350), Line("Bier", 400));

    await _service.SettleAsync([Pay(items[0], 350)], PaymentMethod.Cash, _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    var untouched = await StoredAsync(items[1]);

    Assert.Multiple(() =>
                    {
                      Assert.That(untouched.SettledAtUtc, Is.Null);
                      Assert.That(untouched.ChargedPriceCents, Is.Null);
                      Assert.That(untouched.SettledByStaffMemberId, Is.Null);
                    });
  }

  [Test]
  public async Task SettleAsync_ItemsWereSettled_NamesTheirIdsAndTheirCountInTheLog()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350));

    await _service.SettleAsync([Pay(items[0], 350)], PaymentMethod.Cash, _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    A.CallTo(_logger).Where(call => call.Method.Name == nameof(ILogger.Log) && call.GetArgument<LogLevel>(0) == LogLevel.Information && Equals(ValueNamed(call.GetArgument<object>(2), "SettledItemCount"), 1) && SettledIdsIn(call.GetArgument<object>(2)).Contains(items[0].Id)).MustHaveHappened();
  }

  [Test]
  public async Task SettleAsync_AFreeArticleInsideACashSettlement_StoresCashOnThePaidItemsAndNoneOnTheFreeOne()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350), Line("Bier", 400), Line("Kaffee", 200));

    ErrorOr<SettlementView> settlement = await _service.SettleAsync([
                                                                      Pay(items[0], 350),
                                                                      Pay(items[1], 400),
                                                                      Pay(items[2], 0, "Kapelle")
                                                                    ],
                                                                    PaymentMethod.Cash,
                                                                    _seeded.StaffMemberId,
                                                                    TestContext.CurrentContext.CancellationToken);

    await Assert.MultipleAsync(async () =>
                               {
                                 Assert.That(settlement.IsError, Is.False);
                                 Assert.That((await StoredAsync(items[0])).PaymentMethod, Is.EqualTo(PaymentMethod.Cash));
                                 Assert.That((await StoredAsync(items[1])).PaymentMethod, Is.EqualTo(PaymentMethod.Cash));
                                 Assert.That((await StoredAsync(items[2])).PaymentMethod, Is.EqualTo(PaymentMethod.None));
                               });
  }

  [Test]
  public async Task SettleAsync_AFreeArticleInsideACardSettlement_StoresCardOnThePaidItemAndNoneOnTheFreeOne()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350), Line("Kaffee", 200));

    ErrorOr<SettlementView> settlement = await _service.SettleAsync([
                                                                      Pay(items[0], 350),
                                                                      Pay(items[1], 0, "Kapelle")
                                                                    ],
                                                                    PaymentMethod.Card,
                                                                    _seeded.StaffMemberId,
                                                                    TestContext.CurrentContext.CancellationToken);

    await Assert.MultipleAsync(async () =>
                               {
                                 Assert.That(settlement.IsError, Is.False);
                                 Assert.That((await StoredAsync(items[0])).PaymentMethod, Is.EqualTo(PaymentMethod.Card));
                                 Assert.That((await StoredAsync(items[1])).PaymentMethod, Is.EqualTo(PaymentMethod.None));
                               });
  }

  [Test]
  public async Task SettleAsync_NothingPaidWithNone_StoresNoneOnEveryItem()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350), Line("Bier", 400));

    ErrorOr<SettlementView> settlement = await _service.SettleAsync([
                                                                      Pay(items[0], 0, "Kapelle"),
                                                                      Pay(items[1], 0, "Kapelle")
                                                                    ],
                                                                    PaymentMethod.None,
                                                                    _seeded.StaffMemberId,
                                                                    TestContext.CurrentContext.CancellationToken);

    await Assert.MultipleAsync(async () =>
                               {
                                 Assert.That(settlement.IsError, Is.False);
                                 Assert.That((await StoredAsync(items[0])).PaymentMethod, Is.EqualTo(PaymentMethod.None));
                                 Assert.That((await StoredAsync(items[1])).PaymentMethod, Is.EqualTo(PaymentMethod.None));
                                 Assert.That((await StoredAsync(items[0])).SettledAtUtc, Is.EqualTo(_clock.GetUtcNow().UtcDateTime));
                               });
  }

  [Test]
  public async Task SettleAsync_MoneyPaidWithNone_IsRefusedAndNothingIsStored()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350), Line("Kaffee", 200));

    ErrorOr<SettlementView> settlement = await _service.SettleAsync([
                                                                      Pay(items[0], 350),
                                                                      Pay(items[1], 0, "Kapelle")
                                                                    ],
                                                                    PaymentMethod.None,
                                                                    _seeded.StaffMemberId,
                                                                    TestContext.CurrentContext.CancellationToken);

    await Assert.MultipleAsync(async () =>
                               {
                                 Assert.That(settlement.RefusalMessageKey(), Is.EqualTo("errors.settlement.cannotBeProcessed"));
                                 Assert.That(settlement.RefusalDescription(), Does.Contain("None"));
                                 Assert.That((await StoredAsync(items[0])).SettledAtUtc, Is.Null);
                                 Assert.That((await StoredAsync(items[0])).PaymentMethod, Is.Null);
                                 Assert.That((await StoredAsync(items[1])).SettledAtUtc, Is.Null);
                               });
  }

  [Test]
  public async Task SettleAsync_NothingPaidWithCash_IsRefusedAndNothingIsStored()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350));

    ErrorOr<SettlementView> settlement = await _service.SettleAsync([Pay(items[0], 0, "Kapelle")], PaymentMethod.Cash, _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    await Assert.MultipleAsync(async () =>
                               {
                                 Assert.That(settlement.RefusalMessageKey(), Is.EqualTo("errors.settlement.cannotBeProcessed"));
                                 Assert.That(settlement.RefusalDescription(), Does.Contain("Cash"));
                                 Assert.That((await StoredAsync(items[0])).SettledAtUtc, Is.Null);
                                 Assert.That((await StoredAsync(items[0])).PaymentMethod, Is.Null);
                               });
  }

  [Test]
  public async Task SettleAsync_ALineTheCallerGaveAwayAndNowChargesByCard_WritesCardWithTheNewPrice()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bier", 400));
    await SettleByAsync(items[0], _seeded.StaffMemberId, _orderedAtUtc.AddMinutes(5), 0, "Kapelle", PaymentMethod.None);

    ErrorOr<SettlementView> settlement = await _service.SettleAsync([Pay(items[0], 400)], PaymentMethod.Card, _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    var stored = await StoredAsync(items[0]);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.Value.ReappliedOrderItemIds, Has.Count.EqualTo(1));
                      Assert.That(stored.ChargedPriceCents, Is.EqualTo(400));
                      Assert.That(stored.PaymentMethod, Is.EqualTo(PaymentMethod.Card));
                    });
  }

  [Test]
  public async Task SettleAsync_AnItemThatIsNoLongerThere_IsRefusedAsUnknownAndNothingIsStored()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350));

    ErrorOr<SettlementView> settlement = await _service.SettleAsync([
                                                                      new()
                                                                      {
                                                                        OrderItemId = Guid.NewGuid(),
                                                                        PaidPriceCents = 350
                                                                      }
                                                                    ],
                                                                    PaymentMethod.Cash,
                                                                    _seeded.StaffMemberId,
                                                                    TestContext.CurrentContext.CancellationToken);

    Assert.That(settlement.RefusalMessageKey(), Is.EqualTo("errors.settlement.unknownItem"));
    Assert.That((await StoredAsync(items[0])).SettledAtUtc, Is.Null);
  }

  [Test]
  public async Task ReadTableAsync_ACancelledItem_ComesBackWithItsStateAndIsLeftOutOfTheOpenAmount()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350), Line("Bier", 400));
    await CancelStoredAsync(items.Single(item => item.ItemName == "Bier"), _orderedAtUtc.AddMinutes(4));

    ErrorOr<TableOrderReportView> read = await _service.ReadTableAsync(await TableAsync("Tisch 12"), TestContext.CurrentContext.CancellationToken);

    var bier = read.Value.Orders.Single().Items.Single(item => item.ItemName == "Bier");

    Assert.Multiple(() =>
                    {
                      Assert.That(read.Value.OpenAmountCents, Is.EqualTo(350));
                      Assert.That(bier.State, Is.EqualTo(OrderItemState.Cancelled));
                      Assert.That(bier.StateSetAtUtc, Is.EqualTo(_orderedAtUtc.AddMinutes(4)));
                    });
  }

  [Test]
  public async Task CancelAsync_AnOpenAHandedOutAndTwoSettledItems_CancelsEachForTheWaiterAndAnswersTheMoneyOfTheSettledOnes()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350), Line("Bier", 400), Line("Limonade", 250), Line("Pommes", 300));
    await StampAsync(items[1], handedOutAtUtc: _orderedAtUtc.AddMinutes(10));
    await SettleByAsync(items[2], _anotherWaiter, _orderedAtUtc.AddMinutes(11), 200, "Stammgast", PaymentMethod.Cash);
    await SettleByAsync(items[3], _seeded.StaffMemberId, _orderedAtUtc.AddMinutes(12), 300, null, PaymentMethod.Card);
    var cancelledAtUtc = _clock.GetUtcNow().UtcDateTime;

    ErrorOr<CancellationView> cancellation = await _service.CancelAsync(items.Select(item => item.Id).ToList(), _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    List<OrderItemStatus> stored = [];
    foreach (var item in items)
      stored.Add(await StoredStatusAsync(item));

    Assert.Multiple(() =>
                    {
                      Assert.That(cancellation.Value.CashToReturnCents, Is.EqualTo(500));
                      Assert.That(stored.Select(status => status.Status), Is.All.EqualTo(OrderItemState.Cancelled));
                      Assert.That(stored.Select(status => status.CreatedByStaffMemberId), Is.All.EqualTo(_seeded.StaffMemberId));
                      Assert.That(stored.Select(status => status.CreatedAtUtc), Is.All.EqualTo(cancelledAtUtc));
                    });
  }

  [Test]
  public async Task CancelAsync_NothingSettled_AnswersNoMoneyToHandBack()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350));

    ErrorOr<CancellationView> cancellation = await _service.CancelAsync([items[0].Id], _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    Assert.That(cancellation.Value.CashToReturnCents, Is.Zero);
  }

  [Test]
  public async Task CancelAsync_NoFestivalIsRunning_IsRefusedAsCannotBeProcessedAndNothingIsStored()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350));
    await EndTheFestivalAsync();
    var statusCount = await StoredStatusCountAsync();

    ErrorOr<CancellationView> cancellation = await _service.CancelAsync([items[0].Id], _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(cancellation.RefusalMessageKey(), Is.EqualTo("errors.cancellation.cannotBeProcessed"));
                      Assert.That(cancellation.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                    });
    Assert.That(await StoredStatusCountAsync(), Is.EqualTo(statusCount));
  }

  [Test]
  public async Task CancelAsync_NothingSelected_IsRefusedAsCannotBeProcessed()
  {
    ErrorOr<CancellationView> cancellation = await _service.CancelAsync([], _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(cancellation.RefusalMessageKey(), Is.EqualTo("errors.cancellation.cannotBeProcessed"));
                      Assert.That(cancellation.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                    });
  }

  [Test]
  public async Task CancelAsync_TheSameItemNamedTwice_IsRefusedNamingItAndNothingIsStored()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350));

    ErrorOr<CancellationView> cancellation = await _service.CancelAsync([
                                                                          items[0].Id,
                                                                          items[0].Id
                                                                        ],
                                                                        _seeded.StaffMemberId,
                                                                        TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(cancellation.RefusalMessageKey(), Is.EqualTo("errors.cancellation.cannotBeProcessed"));
                      Assert.That(cancellation.RefusalDescription(), Does.Contain(items[0].Id.ToString()));
                    });
    Assert.That((await StoredStatusAsync(items[0])).Status, Is.EqualTo(OrderItemState.Open));
  }

  [Test]
  public async Task CancelAsync_ASelectionHoldingAnUnknownId_IsRefusedAsUnknownAndCancelsNoneOfIt()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350));

    ErrorOr<CancellationView> cancellation = await _service.CancelAsync([
                                                                          items[0].Id,
                                                                          Guid.NewGuid()
                                                                        ],
                                                                        _seeded.StaffMemberId,
                                                                        TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(cancellation.RefusalMessageKey(), Is.EqualTo("errors.cancellation.unknownItem"));
                      Assert.That(cancellation.FirstError.NumericType, Is.EqualTo(RefusalType.UnprocessableEntity));
                    });
    Assert.That((await StoredStatusAsync(items[0])).Status, Is.EqualTo(OrderItemState.Open));
  }

  [Test]
  public async Task CancelAsync_AnItemOfAnotherFestival_IsRefusedAsUnknown()
  {
    var autumnFestivalId = await AddFestivalAsync("Herbstfest");
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, autumnFestivalId, Line("Bratwurst", 350));

    ErrorOr<CancellationView> cancellation = await _service.CancelAsync([items[0].Id], _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    Assert.That(cancellation.RefusalMessageKey(), Is.EqualTo("errors.cancellation.unknownItem"));
    Assert.That((await StoredStatusAsync(items[0])).Status, Is.EqualTo(OrderItemState.Open));
  }

  [Test]
  public async Task CancelAsync_ASelectionHoldingAnItemCancelledBefore_IsRefusedNamingItAndCancelsNothingElse()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350), Line("Bier", 400));
    await CancelStoredAsync(items[1], _orderedAtUtc.AddMinutes(3));
    var statusCount = await StoredStatusCountAsync();

    ErrorOr<CancellationView> cancellation = await _service.CancelAsync([
                                                                          items[0].Id,
                                                                          items[1].Id
                                                                        ],
                                                                        _seeded.StaffMemberId,
                                                                        TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(cancellation.RefusalMessageKey(), Is.EqualTo("errors.cancellation.alreadyCancelled"));
                      Assert.That(cancellation.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(cancellation.RefusalMetadata(Refusal.MetadataKeys.Name), Is.EqualTo("Bier"));
                    });
    Assert.That(await StoredStatusCountAsync(), Is.EqualTo(statusCount));
  }

  [Test]
  public async Task CancelAsync_TheSameRequestSentAgainAfterALostAnswer_IsRefusedAsAlreadyCancelled()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350));
    await _service.CancelAsync([items[0].Id], _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    var statusCount = await StoredStatusCountAsync();

    await using var retryContext = _fixture.CreateContext();

    ErrorOr<CancellationView> again = await CreateService(retryContext).CancelAsync([items[0].Id], _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    Assert.That(again.RefusalMessageKey(), Is.EqualTo("errors.cancellation.alreadyCancelled"));
    Assert.That(await StoredStatusCountAsync(), Is.EqualTo(statusCount));
  }

  [Test]
  public async Task CancelAsync_ASelectionSpanningTwoTables_IsRefusedNamingBothTablesAndNothingIsStored()
  {
    var first = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350));
    var second = await PlaceOrderAsync("Tisch 7", 2, _orderedAtUtc, Line("Bier", 400));
    var secondTableId = await TableAsync("Tisch 7");
    var statusCount = await StoredStatusCountAsync();

    ErrorOr<CancellationView> cancellation = await _service.CancelAsync([
                                                                          first[0].Id,
                                                                          second[0].Id
                                                                        ],
                                                                        _seeded.StaffMemberId,
                                                                        TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(cancellation.RefusalMessageKey(), Is.EqualTo("errors.cancellation.cannotBeProcessed"));
                      Assert.That(cancellation.RefusalDescription(), Does.Contain(secondTableId.ToString()));
                    });
    Assert.That(await StoredStatusCountAsync(), Is.EqualTo(statusCount));
  }

  [Test]
  public void CancelAsync_WithoutNamingTheWaiter_Throws()
  {
    Assert.That(async () => await _service.CancelAsync([Guid.NewGuid()], Guid.Empty, TestContext.CurrentContext.CancellationToken), Throws.ArgumentException);
  }

  [Test]
  public void CancelAsync_NullIds_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _service.CancelAsync(null!, _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public async Task SettleAsync_ASelectionHoldingACancelledItem_IsRefusedNamingItAndNothingIsStored()
  {
    var items = await PlaceOrderAsync("Tisch 12", 1, _orderedAtUtc, Line("Bratwurst", 350), Line("Bier", 400));
    await CancelStoredAsync(items[1], _orderedAtUtc.AddMinutes(3));

    ErrorOr<SettlementView> settlement = await _service.SettleAsync([
                                                                      Pay(items[0], 350),
                                                                      Pay(items[1], 400)
                                                                    ],
                                                                    PaymentMethod.Cash,
                                                                    _seeded.StaffMemberId,
                                                                    TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(settlement.RefusalMessageKey(), Is.EqualTo("errors.settlement.itemWasCancelled"));
                      Assert.That(settlement.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(settlement.RefusalMetadata(Refusal.MetadataKeys.Name), Is.EqualTo("Bier"));
                    });
    Assert.That((await StoredAsync(items[0])).SettledAtUtc, Is.Null);
  }

  private OpenItemsService CreateService(GastronomyAppDbContext database)
  {
    RunningFestivalLookup runningFestival = new(database, new(), _clock);

    return new(database, new CoreMappingFactory().Create(), runningFestival, new(), _clock, _logger);
  }

  private object? ValueNamed(object? state, string name)
  {
    if (state is not IReadOnlyList<KeyValuePair<string, object?>> values)
      return null;

    return values.FirstOrDefault(value => value.Key == name).Value;
  }

  private IReadOnlyList<Guid> SettledIdsIn(object? state)
  {
    return ValueNamed(state, "SettledOrderItemIds") as IReadOnlyList<Guid> ?? [];
  }

  private SettleLineRequest Pay(OrderItem item, int paidPriceCents, string? paymentNotice = null)
  {
    return new()
           {
             OrderItemId = item.Id,
             PaidPriceCents = paidPriceCents,
             PaymentNotice = paymentNotice
           };
  }

  private OrderLine Line(string itemName, int unitPriceCents, string? note = null)
  {
    return new(itemName, unitPriceCents, note);
  }

  private async Task<Guid> TableAsync(string tableName, Guid? festivalId = null)
  {
    return await new DomainSeeder().SeedTableAsync(_fixture.DbContext, festivalId ?? _seeded.FestivalId, tableName, TestContext.CurrentContext.CancellationToken);
  }

  private async Task<IReadOnlyList<OrderItem>> PlaceOrderAsync(string tableName, int orderNumber, DateTime createdAtUtc, params OrderLine[] lines)
  {
    return await PlaceOrderAsync(tableName, orderNumber, createdAtUtc, _seeded.FestivalId, lines);
  }

  private async Task<IReadOnlyList<OrderItem>> PlaceOrderAsync(string tableName, int orderNumber, DateTime createdAtUtc, Guid festivalId, params OrderLine[] lines)
  {
    return await PlaceOrderAsync(tableName, orderNumber, createdAtUtc, festivalId, _seeded.KitchenStationId, orderNumber, lines);
  }

  private async Task<IReadOnlyList<OrderItem>> PlaceOrderAsync(string tableName, int orderNumber, DateTime createdAtUtc, Guid festivalId, Guid stationId, int stationOrderNumber, params OrderLine[] lines)
  {
    var tableId = await TableAsync(tableName, festivalId);
    var existingOrder = await _fixture.DbContext.Orders.FirstOrDefaultAsync(order => order.TableId == tableId && order.GlobalOrderNumber == orderNumber, TestContext.CurrentContext.CancellationToken);
    var orderId = existingOrder?.Id ?? Guid.NewGuid();
    var stationOrderId = Guid.NewGuid();

    StationOrder stationOrder = new()
                                {
                                  Id = stationOrderId,
                                  OrderId = orderId,
                                  FestivalId = festivalId,
                                  StationId = stationId,
                                  StationOrderNumber = stationOrderNumber,
                                  DeliveryMode = DeliveryMode.Together
                                };

    List<OrderItem> items = lines.Select(line => new OrderItem
                                                 {
                                                   Id = Guid.NewGuid(),
                                                   StationOrderId = stationOrderId,
                                                   CatalogItemId = _seeded.SausageItemId,
                                                   ItemName = line.ItemName,
                                                   UnitPriceCents = line.UnitPriceCents,
                                                   Note = line.Note
                                                 })
                                 .Select(item => _statusSeeder.Open(item, _seeded.StaffMemberId, createdAtUtc))
                                 .ToList();

    foreach (var item in items)
      stationOrder.Items.Add(item);

    if (existingOrder is null)
    {
      Order order = new()
                    {
                      Id = orderId,
                      ClientOrderId = Guid.NewGuid(),
                      FestivalId = festivalId,
                      GlobalOrderNumber = orderNumber,
                      StaffMemberId = _seeded.StaffMemberId,
                      TableId = tableId,
                      CreatedAtUtc = createdAtUtc
                    };
      order.StationOrders.Add(stationOrder);
      _fixture.DbContext.Orders.Add(order);
    }
    else
    {
      _fixture.DbContext.StationOrders.Add(stationOrder);
    }

    await _statusSeeder.SaveAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    return items;
  }

  private async Task StampAsync(OrderItem item, DateTime? handedOutAtUtc = null, DateTime? settledAtUtc = null)
  {
    var tracked = await _fixture.DbContext.OrderItems.Include(candidate => candidate.StationOrder).SingleAsync(candidate => candidate.Id == item.Id, TestContext.CurrentContext.CancellationToken);

    if (handedOutAtUtc is { } handOutTime)
      _statusSeeder.HandOut(tracked, tracked.StationOrder.StationId, handOutTime);

    tracked.SettledAtUtc = settledAtUtc;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task CancelStoredAsync(OrderItem item, DateTime cancelledAtUtc)
  {
    var tracked = await _fixture.DbContext.OrderItems.SingleAsync(candidate => candidate.Id == item.Id, TestContext.CurrentContext.CancellationToken);
    _statusSeeder.Cancel(tracked, _seeded.StaffMemberId, cancelledAtUtc);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<OrderItemStatus> StoredStatusAsync(OrderItem item)
  {
    await using var readContext = _fixture.CreateContext();

    var stored = await readContext.OrderItems.AsNoTracking().SingleAsync(candidate => candidate.Id == item.Id, TestContext.CurrentContext.CancellationToken);

    return stored.CurrentStatus!;
  }

  private async Task<int> StoredStatusCountAsync()
  {
    await using var readContext = _fixture.CreateContext();

    return await readContext.OrderItemStatuses.CountAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task SettleByAsync(OrderItem item, Guid staffMemberId, DateTime settledAtUtc, int chargedPriceCents, string? paymentNotice, PaymentMethod paymentMethod)
  {
    var tracked = await _fixture.DbContext.OrderItems.SingleAsync(candidate => candidate.Id == item.Id, TestContext.CurrentContext.CancellationToken);
    tracked.SettledAtUtc = settledAtUtc;
    tracked.SettledByStaffMemberId = staffMemberId;
    tracked.ChargedPriceCents = chargedPriceCents;
    tracked.PaymentNotice = paymentNotice;
    tracked.PaymentMethod = paymentMethod;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<OrderItem> StoredAsync(OrderItem item)
  {
    await using var readContext = _fixture.CreateContext();

    return await readContext.OrderItems.AsNoTracking().SingleAsync(candidate => candidate.Id == item.Id, TestContext.CurrentContext.CancellationToken);
  }

  private async Task<Guid> AddFestivalAsync(string name)
  {
    var festivalId = Guid.NewGuid();

    _fixture.DbContext.Festivals.Add(new()
                                     {
                                       Id = festivalId,
                                       Name = name,
                                       StartsAtUtc = _orderedAtUtc.AddYears(-1),
                                       EndsAtUtc = _orderedAtUtc.AddYears(-1).AddDays(2),
                                       NextOrderNumber = 1,
                                       IsHidden = false
                                     });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return festivalId;
  }

  private async Task EndTheFestivalAsync()
  {
    var festival = await _fixture.DbContext.Festivals.SingleAsync(candidate => candidate.Id == _seeded.FestivalId, TestContext.CurrentContext.CancellationToken);
    festival.StartsAtUtc = DateTime.UtcNow.AddDays(-2);
    festival.EndsAtUtc = DateTime.UtcNow.AddHours(-1);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private sealed record OrderLine(string ItemName, int UnitPriceCents, string? Note);
}
