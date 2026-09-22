using GastronomyApp.Api.Mapping;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.OpenItems;
using GastronomyApp.Core.Entities;
using MapsterMapper;

namespace GastronomyApp.Api.Tests.Mapping;

[TestFixture]
public sealed class OpenItemMappingTest
{
  [SetUp]
  public void SetUp()
  {
    _mapper = new Mapper(new MappingConfiguration(new(), new()).Build());
    _ordersPlaced = 0;
  }

  private readonly DateTime _orderedAtUtc = new(2026, 9, 5, 19, 5, 0, DateTimeKind.Utc);

  private int _ordersPlaced;

  private IMapper _mapper = null!;

  [Test]
  public void Map_AnOpenItem_CarriesTheArticleItsNoteAndTheOrderItBelongsTo()
  {
    var bratwurst = Item("Bratwurst", 350);
    bratwurst.Note = "Ohne Ketchup";
    var order = OrderAt("Tisch 12", bratwurst);

    var view = _mapper.Map<OpenOrderItemView>(bratwurst);

    Assert.Multiple(() =>
                    {
                      Assert.That(view.OrderItemId, Is.EqualTo(bratwurst.Id));
                      Assert.That(view.OrderId, Is.EqualTo(order.Id));
                      Assert.That(view.GlobalOrderNumber, Is.EqualTo(order.GlobalOrderNumber));
                      Assert.That(view.ItemName, Is.EqualTo("Bratwurst"));
                      Assert.That(view.Note, Is.EqualTo("Ohne Ketchup"));
                      Assert.That(view.UnitPriceCents, Is.EqualTo(350));
                      Assert.That(view.OrderedAtUtc, Is.EqualTo(order.CreatedAtUtc));
                    });
  }

  [Test]
  public void Map_APositionOfATable_CarriesWhenItWasHandedOutAndWhenItWasSettled()
  {
    var limonade = Item("Limonade", 250);
    limonade.FulfilledAtUtc = _orderedAtUtc.AddMinutes(20);
    limonade.SettledAtUtc = _orderedAtUtc.AddMinutes(25);
    var order = OrderAt("Tisch 12", limonade);

    var view = _mapper.Map<TableOrderRecordItemView>(limonade);

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

    var view = _mapper.Map<TableOrderRecordView>(order);

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

    var view = _mapper.Map<TableOrderRecordView>(order);

    Assert.That(view.Items.Select(item => item.OrderItemId),
                Is.EqualTo(new[]
                           {
                             plainBratwurst.Id,
                             notedBratwurst.Id,
                             schnitzel.Id
                           }));
  }

  [Test]
  public void Map_AnOrderSplitAcrossTwoStations_HoldsThePositionsOfBothStations()
  {
    var bratwurst = Item("Bratwurst", 350);
    var bier = Item("Bier", 400);
    var order = OrderAt("Tisch 12", bratwurst);
    AddStationOrder(order, bier);

    var view = _mapper.Map<TableOrderRecordView>(order);

    Assert.That(view.Items.Select(item => item.OrderItemId),
                Is.EqualTo(new[]
                           {
                             bier.Id,
                             bratwurst.Id
                           }));
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
                    TableName = tableName,
                    CreatedAtUtc = _orderedAtUtc
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
