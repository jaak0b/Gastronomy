using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.OpenItems;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Mapster;

namespace GastronomyApp.Infrastructure.Tests.Mapping;

[TestFixture]
public sealed class OpenItemMappingTest
{
  [SetUp]
  public void SetUp()
  {
    _mapping = new CoreMappingFactory().Create();
    _ordersPlaced = 0;
  }

  private readonly DateTime _orderedAtUtc = new(2026, 9, 5, 19, 5, 0, DateTimeKind.Utc);

  private readonly OrderItemStatusSeeder _statusSeeder = new();

  private int _ordersPlaced;

  private TypeAdapterConfig _mapping = null!;

  [Test]
  public void Map_APositionOfATable_CarriesItsStateWithItsTimeAndWhenAndForWhatItWasSettled()
  {
    var limonade = Item("Limonade", 250);
    _statusSeeder.HandOut(limonade, Guid.NewGuid(), _orderedAtUtc.AddMinutes(20));
    limonade.SettledAtUtc = _orderedAtUtc.AddMinutes(25);
    limonade.ChargedPriceCents = 200;
    var order = OrderAt("Tisch 12", limonade);

    var view = limonade.Adapt<TableOrderRecordItemView>(_mapping);

    Assert.Multiple(() =>
                    {
                      Assert.That(view.OrderItemId, Is.EqualTo(limonade.Id));
                      Assert.That(view.OrderId, Is.EqualTo(order.Id));
                      Assert.That(view.GlobalOrderNumber, Is.EqualTo(order.GlobalOrderNumber));
                      Assert.That(view.State, Is.EqualTo(OrderItemState.HandedOut));
                      Assert.That(view.StateSetAtUtc, Is.EqualTo(_orderedAtUtc.AddMinutes(20)));
                      Assert.That(view.SettledAtUtc, Is.EqualTo(_orderedAtUtc.AddMinutes(25)));
                      Assert.That(view.ChargedPriceCents, Is.EqualTo(200));
                    });
  }

  [Test]
  public void Map_AnOrderOfATable_NamesTheWaiterWhoTookIt()
  {
    var order = OrderAt("Tisch 12", Item("Bratwurst", 350));

    var view = Searched(order).Adapt<TableOrderRecordView>(_mapping);

    Assert.Multiple(() =>
                    {
                      Assert.That(view.OrderId, Is.EqualTo(order.Id));
                      Assert.That(view.GlobalOrderNumber, Is.EqualTo(order.GlobalOrderNumber));
                      Assert.That(view.CreatedAtUtc, Is.EqualTo(order.CreatedAtUtc));
                      Assert.That(view.StaffMemberName, Is.EqualTo("Anna"));
                    });
  }

  [Test]
  public void Map_ThePositionsOfAnOrder_ComeBackByNameThenNoteThenId()
  {
    var schnitzel = Item("Schnitzel", 400);
    schnitzel.Id = new("00000000-0000-0000-0000-000000000003");
    var notedBratwurst = Item("Bratwurst", 350);
    notedBratwurst.Id = new("00000000-0000-0000-0000-000000000001");
    notedBratwurst.Note = "Ohne Ketchup";
    var plainBratwurst = Item("Bratwurst", 350);
    plainBratwurst.Id = new("00000000-0000-0000-0000-000000000002");

    var order = OrderAt("Tisch 12", schnitzel, notedBratwurst, plainBratwurst);

    var view = Searched(order).Adapt<TableOrderRecordView>(_mapping);

    Assert.That(view.Items.Select(item => item.OrderItemId),
                Is.EqualTo(new[]
                           {
                             plainBratwurst.Id,
                             notedBratwurst.Id,
                             schnitzel.Id
                           }));
  }

  [Test]
  public void Map_ThePositionsOfAnOrderInMixedCase_ComeBackWithCapitalsIgnoredAndUmlautsAfterZ()
  {
    var order = OrderAt("Tisch 12", Item("Zwiebel", 100), Item("Äpfel", 100), Item("apfel", 100), Item("Birne", 100));

    var view = Searched(order).Adapt<TableOrderRecordView>(_mapping);

    Assert.That(view.Items.Select(item => item.ItemName),
                Is.EqualTo(new[]
                           {
                             "apfel",
                             "Birne",
                             "Zwiebel",
                             "Äpfel"
                           }));
  }

  [Test]
  public void Map_AnOrderSplitAcrossTwoStations_HoldsThePositionsOfBothStations()
  {
    var bratwurst = Item("Bratwurst", 350);
    var bier = Item("Bier", 400);
    var order = OrderAt("Tisch 12", bratwurst);
    AddStationOrder(order, bier);

    var view = Searched(order).Adapt<TableOrderRecordView>(_mapping);

    Assert.That(view.Items.Select(item => item.OrderItemId),
                Is.EqualTo(new[]
                           {
                             bier.Id,
                             bratwurst.Id
                           }));
  }

  [Test]
  public void Map_ACancelledPosition_CarriesTheCancellationWithItsTime()
  {
    var bier = Item("Bier", 400);
    _statusSeeder.Cancel(bier, Guid.NewGuid(), _orderedAtUtc.AddMinutes(7));
    OrderAt("Tisch 12", bier);

    var view = bier.Adapt<TableOrderRecordItemView>(_mapping);

    Assert.Multiple(() =>
                    {
                      Assert.That(view.State, Is.EqualTo(OrderItemState.Cancelled));
                      Assert.That(view.StateSetAtUtc, Is.EqualTo(_orderedAtUtc.AddMinutes(7)));
                      Assert.That(view.ChargedPriceCents, Is.Null);
                    });
  }

  [Test]
  public void Map_AnOrderWithFewerItemsShown_CountsEveryItemThatIsNotCancelledAndShowsOnlyTheShownOnes()
  {
    var bier = Item("Bier", 400);
    var wasser = Item("Wasser", 200);
    _statusSeeder.HandOut(wasser, Guid.NewGuid(), _orderedAtUtc.AddMinutes(5));
    var cola = Item("Cola", 300);
    _statusSeeder.Cancel(cola, Guid.NewGuid(), _orderedAtUtc.AddMinutes(6));
    var order = OrderAt("Tisch 12", bier, wasser, cola);

    var view = new SearchedOrder
               {
                 Order = order,
                 ShownItems = [wasser]
               }.Adapt<TableOrderRecordView>(_mapping);

    Assert.Multiple(() =>
                    {
                      Assert.That(view.Items.Select(item => item.OrderItemId), Is.EqualTo(new[] { wasser.Id }));
                      Assert.That(view.NotCancelledItemCount, Is.EqualTo(2));
                      Assert.That(view.HandedOutItemCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public void Map_APositionCancelledAfterItWasHandedOut_SaysItWasHandedOutWhenCancelled()
  {
    var bier = Item("Bier", 400);
    _statusSeeder.HandOut(bier, Guid.NewGuid(), _orderedAtUtc.AddMinutes(5));
    _statusSeeder.Cancel(bier, Guid.NewGuid(), _orderedAtUtc.AddMinutes(6));
    OrderAt("Tisch 12", bier);

    var view = bier.Adapt<TableOrderRecordItemView>(_mapping);

    Assert.That(view.WasHandedOutWhenCancelled, Is.True);
  }

  [Test]
  public void Map_ASettlementResult_ListsTheIdsOfEachGroup()
  {
    var settled = Item("Bratwurst", 350);
    var reapplied = Item("Bier", 400);
    var takenByOthers = Item("Limonade", 250);
    SettlementResult result = new()
                              {
                                NewlySettled = [settled],
                                Reapplied = [reapplied],
                                AlreadySettledByOthers = [takenByOthers]
                              };

    var view = result.Adapt<SettlementView>(_mapping);

    Assert.Multiple(() =>
                    {
                      Assert.That(view.SettledOrderItemIds, Is.EqualTo(new[] { settled.Id }));
                      Assert.That(view.ReappliedOrderItemIds, Is.EqualTo(new[] { reapplied.Id }));
                      Assert.That(view.AlreadySettledByOthersOrderItemIds, Is.EqualTo(new[] { takenByOthers.Id }));
                    });
  }

  private SearchedOrder Searched(Order order)
  {
    return new()
           {
             Order = order,
             ShownItems = order.StationOrders.SelectMany(stationOrder => stationOrder.Items).ToList()
           };
  }

  private Order OrderAt(string tableName, params OrderItem[] items)
  {
    _ordersPlaced++;

    Order order = new()
                  {
                    Id = Guid.NewGuid(),
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = Guid.NewGuid(),
                    GlobalOrderNumber = _ordersPlaced,
                    StaffMemberId = Guid.NewGuid(),
                    TableId = Guid.NewGuid(),
                    CreatedAtUtc = _orderedAtUtc
                  };

    order.Table = new()
                  {
                    Id = order.TableId,
                    FestivalId = order.FestivalId,
                    AreaId = Guid.NewGuid(),
                    Name = tableName,
                    IsHidden = false
                  };

    order.StaffMember = new()
                        {
                          Id = order.StaffMemberId,
                          Name = "Anna",
                          IsActive = true,
                          CreatedAtUtc = _orderedAtUtc
                        };

    AddStationOrder(order, items);

    return order;
  }

  private void AddStationOrder(Order order, params OrderItem[] items)
  {
    StationOrder stationOrder = new()
                                {
                                  Id = Guid.NewGuid(),
                                  OrderId = order.Id,
                                  FestivalId = order.FestivalId,
                                  StationId = Guid.NewGuid(),
                                  StationOrderNumber = order.StationOrders.Count + 1,
                                  DeliveryMode = DeliveryMode.Together,
                                  Order = order
                                };

    foreach (var item in items)
    {
      item.StationOrderId = stationOrder.Id;
      item.StationOrder = stationOrder;
      stationOrder.Items.Add(item);
    }

    order.StationOrders.Add(stationOrder);
  }

  private OrderItem Item(string itemName, int unitPriceCents)
  {
    OrderItem item = new()
                     {
                       Id = Guid.NewGuid(),
                       StationOrderId = Guid.NewGuid(),
                       CatalogItemId = Guid.NewGuid(),
                       ItemName = itemName,
                       UnitPriceCents = unitPriceCents
                     };

    return _statusSeeder.OpenAndMakeCurrent(item, Guid.NewGuid(), _orderedAtUtc);
  }
}
