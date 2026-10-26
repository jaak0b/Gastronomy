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

  private int _ordersPlaced;

  private TypeAdapterConfig _mapping = null!;

  [Test]
  public void Map_APositionOfATable_CarriesWhenItWasHandedOutAndWhenItWasSettled()
  {
    var limonade = Item("Limonade", 250);
    limonade.FulfilledAtUtc = _orderedAtUtc.AddMinutes(20);
    limonade.SettledAtUtc = _orderedAtUtc.AddMinutes(25);
    var order = OrderAt("Tisch 12", limonade);

    var view = limonade.Adapt<TableOrderRecordItemView>(_mapping);

    Assert.Multiple(() =>
                    {
                      Assert.That(view.OrderItemId, Is.EqualTo(limonade.Id));
                      Assert.That(view.OrderId, Is.EqualTo(order.Id));
                      Assert.That(view.GlobalOrderNumber, Is.EqualTo(order.GlobalOrderNumber));
                      Assert.That(view.OrderedAtUtc, Is.EqualTo(order.CreatedAtUtc));
                      Assert.That(view.FulfilledAtUtc, Is.EqualTo(_orderedAtUtc.AddMinutes(20)));
                      Assert.That(view.SettledAtUtc, Is.EqualTo(_orderedAtUtc.AddMinutes(25)));
                    });
  }

  [Test]
  public void Map_AnOrderOfATable_NamesTheWaiterWhoTookIt()
  {
    var order = OrderAt("Tisch 12", Item("Bratwurst", 350));

    var view = order.Adapt<TableOrderRecordView>(_mapping);

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

    var view = order.Adapt<TableOrderRecordView>(_mapping);

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

    var view = order.Adapt<TableOrderRecordView>(_mapping);

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

    var view = order.Adapt<TableOrderRecordView>(_mapping);

    Assert.That(view.Items.Select(item => item.OrderItemId),
                Is.EqualTo(new[]
                           {
                             bier.Id,
                             bratwurst.Id
                           }));
  }

  [Test]
  public void Map_ATableWithOpenAndSettledPositions_NamesTheTableAndOwesOnlyTheOpenOnes()
  {
    var bratwurst = Item("Bratwurst", 350);
    var bier = Item("Bier", 400);
    bier.SettledAtUtc = _orderedAtUtc.AddMinutes(5);
    var order = OrderAt("Tisch 12", bratwurst, bier);
    order.Table.Orders.Add(order);

    var view = order.Table.Adapt<TableOrderReportView>(_mapping);

    Assert.Multiple(() =>
                    {
                      Assert.That(view.TableId, Is.EqualTo(order.TableId));
                      Assert.That(view.TableName, Is.EqualTo("Tisch 12"));
                      Assert.That(view.OpenAmountCents, Is.EqualTo(350));
                      Assert.That(view.Orders.Select(placed => placed.OrderId), Is.EqualTo(new[] { order.Id }));
                    });
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
    return new()
           {
             Id = Guid.NewGuid(),
             StationOrderId = Guid.NewGuid(),
             CatalogItemId = Guid.NewGuid(),
             ItemName = itemName,
             UnitPriceCents = unitPriceCents
           };
  }
}
