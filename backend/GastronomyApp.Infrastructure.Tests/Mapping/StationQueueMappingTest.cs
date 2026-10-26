using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Stations;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Mapster;

namespace GastronomyApp.Infrastructure.Tests.Mapping;

[TestFixture]
public sealed class StationQueueMappingTest
{
  [SetUp]
  public void SetUp()
  {
    _mapping = new CoreMappingFactory().Create();
  }

  private readonly DateTime _placedAtUtc = new(2026, 9, 5, 19, 5, 0, DateTimeKind.Utc);
  private readonly Guid _kitchenId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

  private TypeAdapterConfig _mapping = null!;

  [Test]
  public void Map_AStationOrderWithOpenItems_CarriesTheTableTheWaiterAndBothCounts()
  {
    var stationOrder = StationOrderWith(1, DeliveryMode.Together, Item("Bratwurst", null), Item("Limonade", null));
    stationOrder.Items[0].FulfilledAtUtc = _placedAtUtc.AddMinutes(3);

    var view = stationOrder.Adapt<StationOrderQueueView>(_mapping);

    Assert.Multiple(() =>
                    {
                      Assert.That(view.StationOrderId, Is.EqualTo(stationOrder.Id));
                      Assert.That(view.GlobalOrderNumber, Is.EqualTo(7));
                      Assert.That(view.StationOrderNumber, Is.EqualTo(1));
                      Assert.That(view.TableName, Is.EqualTo("Tisch 12"));
                      Assert.That(view.StaffMemberName, Is.EqualTo("Anna"));
                      Assert.That(view.CreatedAtUtc, Is.EqualTo(_placedAtUtc));
                      Assert.That(view.ItemCount, Is.EqualTo(2));
                      Assert.That(view.FulfilledItemCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public void Map_ItemsOfOneStationOrder_ComeBackTogetherByNameAndNote()
  {
    var stationOrder = StationOrderWith(1, DeliveryMode.Together, ItemWithId("Käsekrainer", null, "00000000-0000-0000-0000-000000000002"), ItemWithId("Schnitzel", null, "00000000-0000-0000-0000-000000000001"), ItemWithId("Käsekrainer", "Ohne Ketchup", "00000000-0000-0000-0000-000000000003"));

    var view = stationOrder.Adapt<StationOrderQueueView>(_mapping);

    Assert.Multiple(() =>
                    {
                      Assert.That(view.Items.Select(item => item.ItemName),
                                  Is.EqualTo(new[]
                                             {
                                               "Käsekrainer",
                                               "Käsekrainer",
                                               "Schnitzel"
                                             }));
                      Assert.That(view.Items.Select(item => item.Note),
                                  Is.EqualTo(new string?[]
                                             {
                                               null,
                                               "Ohne Ketchup",
                                               null
                                             }));
                    });
  }

  [Test]
  public void Map_ItemNamesInMixedCase_ComeBackWithCapitalsIgnoredAndUmlautsAfterZ()
  {
    var stationOrder = StationOrderWith(1, DeliveryMode.Together, Item("Zwiebel", null), Item("Äpfel", null), Item("apfel", null), Item("Birne", null));

    var view = stationOrder.Adapt<StationOrderQueueView>(_mapping);

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
  public void Map_AStationOrderTheStationHandedOut_CarriesEveryLineWithItsHandOverTime()
  {
    var stationOrder = StationOrderWith(1, DeliveryMode.Together, Item("Bratwurst", "Ohne Ketchup"));
    stationOrder.Items[0].FulfilledAtUtc = _placedAtUtc.AddMinutes(6);

    var view = stationOrder.Adapt<StationOrderQueueView>(_mapping);

    Assert.Multiple(() =>
                    {
                      Assert.That(view.Items[0].OrderItemId, Is.EqualTo(stationOrder.Items[0].Id));
                      Assert.That(view.Items[0].ItemName, Is.EqualTo("Bratwurst"));
                      Assert.That(view.Items[0].Note, Is.EqualTo("Ohne Ketchup"));
                      Assert.That(view.Items[0].FulfilledAtUtc, Is.EqualTo(_placedAtUtc.AddMinutes(6)));
                    });
  }

  [Test]
  public void Map_AnAsItComesOrderTheEmployeeHid_CarriesTheFlagAndTheMode()
  {
    var stationOrder = StationOrderWith(1, DeliveryMode.AsItComes, Item("Bratwurst", null));
    stationOrder.IsHiddenFromAsItComesQueue = true;

    var view = stationOrder.Adapt<StationOrderQueueView>(_mapping);

    Assert.Multiple(() =>
                    {
                      Assert.That(view.DeliveryMode, Is.EqualTo(DeliveryMode.AsItComes));
                      Assert.That(view.IsHiddenFromAsItComesQueue, Is.True);
                    });
  }

  [Test]
  public void Map_AStation_CarriesItsIdAndName()
  {
    Station station = new()
                      {
                        Id = _kitchenId,
                        Name = "Kueche",
                        SortOrder = 1,
                        IsActive = true
                      };

    var view = station.Adapt<StationSummaryView>(_mapping);

    Assert.Multiple(() =>
                    {
                      Assert.That(view.Id, Is.EqualTo(_kitchenId));
                      Assert.That(view.Name, Is.EqualTo("Kueche"));
                    });
  }

  private StationOrder StationOrderWith(int stationOrderNumber, DeliveryMode deliveryMode, params OrderItem[] items)
  {
    var stationOrderId = Guid.NewGuid();

    StationOrder stationOrder = new()
                                {
                                  Id = stationOrderId,
                                  OrderId = Guid.NewGuid(),
                                  FestivalId = Guid.NewGuid(),
                                  StationId = _kitchenId,
                                  StationOrderNumber = stationOrderNumber,
                                  DeliveryMode = deliveryMode,
                                  Order = Order()
                                };

    foreach (var item in items)
    {
      item.StationOrderId = stationOrderId;
      stationOrder.Items.Add(item);
    }

    return stationOrder;
  }

  private Order Order()
  {
    return new()
           {
             Id = Guid.NewGuid(),
             ClientOrderId = Guid.NewGuid(),
             FestivalId = Guid.NewGuid(),
             GlobalOrderNumber = 7,
             StaffMemberId = Guid.NewGuid(),
             TableId = Guid.NewGuid(),
             CreatedAtUtc = _placedAtUtc,
             Table = new()
                     {
                       Id = Guid.NewGuid(),
                       FestivalId = Guid.NewGuid(),
                       AreaId = Guid.NewGuid(),
                       Name = "Tisch 12",
                       IsHidden = false
                     },
             StaffMember = new()
                           {
                             Id = Guid.NewGuid(),
                             Name = "Anna",
                             IsActive = true,
                             CreatedAtUtc = _placedAtUtc
                           }
           };
  }

  private OrderItem Item(string itemName, string? note)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             StationOrderId = Guid.NewGuid(),
             CatalogItemId = Guid.NewGuid(),
             ItemName = itemName,
             UnitPriceCents = 350,
             Note = note
           };
  }

  private OrderItem ItemWithId(string itemName, string? note, string id)
  {
    var item = Item(itemName, note);
    item.Id = new(id);

    return item;
  }
}
