using System.Net;
using System.Text;
using System.Text.Json;
using GastronomyApp.Api.Tests.TestSupport;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class OrderEndpointsTest
{
  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContextBuilder().StartAsync();
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
  }

  private OrderTestContext _context = null!;

  [Test]
  public async Task PostOrder_FirstSubmission_IsAcceptedNumberedAndProjected()
  {
    using var response = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid()));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                      Assert.That(body.RootElement.GetProperty("globalOrderNumber").GetInt32(), Is.EqualTo(1));
                      Assert.That(body.RootElement.GetProperty("status").GetString(), Is.EqualTo("open"));
                      Assert.That(body.RootElement.GetProperty("totalCents").GetInt32(), Is.EqualTo(700));
                      Assert.That(body.RootElement.GetProperty("stationOrders").GetArrayLength(), Is.EqualTo(1));
                    });

    var stationOrder = body.RootElement.GetProperty("stationOrders")[0];

    Assert.Multiple(() =>
                    {
                      Assert.That(stationOrder.GetProperty("stationId").GetGuid(), Is.EqualTo(_context.World.KitchenStationId));
                      Assert.That(stationOrder.GetProperty("stationName").GetString(), Is.EqualTo("Kueche"));
                      Assert.That(stationOrder.GetProperty("stationOrderNumber").GetInt32(), Is.EqualTo(1));
                      Assert.That(stationOrder.GetProperty("itemIds").GetArrayLength(), Is.EqualTo(2));
                    });

    await using var database = _context.Factory.CreateContext();
    var stored = await database.Orders.SingleAsync();

    Assert.That(stored.GlobalOrderNumber, Is.EqualTo(1));
  }

  [Test]
  public async Task PostOrder_ATableOfTheRunningFestival_StoresTheOrderAtThatTable()
  {
    using (var response = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid())))
    {
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    await using var database = _context.Factory.CreateContext();
    var stored = await database.Orders.SingleAsync();

    Assert.That(stored.TableId, Is.EqualTo(_context.World.TableId));
  }

  [Test]
  public async Task PostOrder_AHiddenTable_IsAccepted()
  {
    await using (var hiding = _context.Factory.CreateContext())
    {
      var table = await hiding.Tables.SingleAsync(candidate => candidate.Id == _context.World.TableId);
      table.IsHidden = true;
      await hiding.SaveChangesAsync();
    }

    using var response = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid()));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
  }

  [Test]
  public async Task PostOrder_ATableOfAnotherFestival_IsRefusedWithTheGenericMessageAndStoresNothing()
  {
    var otherFestivalId = Guid.NewGuid();
    await using (var seeding = _context.Factory.CreateContext())
    {
      seeding.Festivals.Add(new()
                            {
                              Id = otherFestivalId,
                              Name = "Herbstfest",
                              StartsAtUtc = DateTime.UtcNow.AddYears(2),
                              EndsAtUtc = DateTime.UtcNow.AddYears(2).AddDays(1),
                              NextOrderNumber = 1,
                              IsHidden = false
                            });
      await seeding.SaveChangesAsync();
    }

    Guid otherFestivalsTableId;
    await using (var seeding = _context.Factory.CreateContext())
    {
      otherFestivalsTableId = await new ApiSeeder().SeedTableAsync(seeding, otherFestivalId, "Tisch 12", CancellationToken.None);
    }

    using var response = await _context.PostOrderAsync(new(Guid.NewGuid(), otherFestivalsTableId, [new(_context.World.BratwurstItemId, 350, null, null)]));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    await using var database = _context.Factory.CreateContext();
    var orderCount = await database.Orders.CountAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(body.RootElement.GetProperty("code").GetString(), Is.EqualTo("UnprocessableEntity"));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("errors.order.cannotBeProcessed"));
                      Assert.That(orderCount, Is.Zero);
                    });
  }

  [Test]
  public async Task PostOrder_FirstSubmission_LeavesEveryItemOpen()
  {
    using (var response = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid())))
    {
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    await using var database = _context.Factory.CreateContext();

    Assert.That(await database.OrderItems.CountAsync(item => item.CurrentStatus!.Status == OrderItemState.Open), Is.EqualTo(2));
  }

  [Test]
  public async Task PostOrder_FirstSubmission_LeavesNoItemWithoutACurrentStatusOfItsOwn()
  {
    using (var response = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid())))
    {
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    await using var database = _context.Factory.CreateContext();
    List<OrderItem> items = await database.OrderItems.Include(item => item.CurrentStatus).ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(items, Has.Count.EqualTo(2));
                      Assert.That(items.Select(item => item.CurrentStatus), Is.All.Not.Null);
                      Assert.That(items.Select(item => item.CurrentStatus!.OrderItemId), Is.EqualTo(items.Select(item => item.Id)));
                    });
  }

  [Test]
  public async Task PostOrder_TheSaveThatPointsEachItemAtItsFirstStatusFails_StoresNothingOfTheOrder()
  {
    await using (var database = _context.Factory.CreateContext())
    {
      await database.Database.ExecuteSqlRawAsync("""
                                                 CREATE TRIGGER "RefuseCurrentStatus" BEFORE UPDATE OF "CurrentStatusId" ON "OrderItems"
                                                 BEGIN SELECT RAISE(ABORT, 'second save refused by the test'); END;
                                                 """);
    }

    using var response = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid()));

    await using var afterwards = _context.Factory.CreateContext();

    Assert.Multiple(async () =>
                    {
                      Assert.That(response.IsSuccessStatusCode, Is.False);
                      Assert.That(await afterwards.Orders.CountAsync(), Is.Zero);
                      Assert.That(await afterwards.StationOrders.CountAsync(), Is.Zero);
                      Assert.That(await afterwards.OrderItems.CountAsync(), Is.Zero);
                      Assert.That(await afterwards.OrderItemStatuses.CountAsync(), Is.Zero);
                    });
  }

  [Test]
  public async Task PostOrder_TwoStations_CreatesOneStationOrderPerStation()
  {
    OrderBody twoStations = new(Guid.NewGuid(),
                                _context.World.TableId,
                                [
                                  new(_context.World.BratwurstItemId, 350, null, null),
                                  new(_context.World.BratwurstItemId, 350, null, null),
                                  new(_context.World.BeerItemId, 350, null, null)
                                ]);

    using var response = await _context.PostOrderAsync(twoStations);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    await using var database = _context.Factory.CreateContext();
    var stationOrderCount = await database.StationOrders.CountAsync();

    Assert.That(stationOrderCount, Is.EqualTo(2));
  }

  [Test]
  public async Task PostOrder_UnknownItemId_IsRefusedAsUnprocessable()
  {
    OrderBody unknownItem = new(Guid.NewGuid(), _context.World.TableId, [new(Guid.NewGuid(), 350, null, null)]);

    using var response = await _context.PostOrderAsync(unknownItem);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(body.RootElement.GetProperty("code").GetString(), Is.EqualTo("UnprocessableEntity"));
                    });
  }

  [Test]
  public async Task PostOrder_NoItems_IsRefusedAsAValidationFailure()
  {
    OrderBody empty = new(Guid.NewGuid(), _context.World.TableId, []);

    using var response = await _context.PostOrderAsync(empty);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
  }

  [Test]
  public async Task PostOrder_SoldOutItem_IsRefusedWithTheWordingThePhoneShows()
  {
    await using (var seeding = _context.Factory.CreateContext())
    {
      var item = await seeding.FestivalCatalogItems.FirstAsync(menuRow => menuRow.CatalogItemId == _context.World.BratwurstItemId);
      item.IsAvailable = false;
      await seeding.SaveChangesAsync();
    }

    using var response = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid()));
    var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    await using var database = _context.Factory.CreateContext();
    var storedOrders = await database.Orders.CountAsync();
    var storedItems = await database.OrderItems.CountAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(error.RootElement.GetProperty("code").GetString(), Is.EqualTo("UnprocessableEntity"));
                      Assert.That(error.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("errors.order.itemSoldOut"));
                      Assert.That(error.RootElement.GetProperty("parameters").GetProperty("name").GetString(), Is.EqualTo("Bratwurst mit Brot"));
                      Assert.That(error.RootElement.GetProperty("parameters").GetProperty("catalogItemId").GetGuid(), Is.EqualTo(_context.World.BratwurstItemId));
                      Assert.That(storedOrders, Is.Zero);
                      Assert.That(storedItems, Is.Zero);
                    });
  }

  [Test]
  public async Task PostOrder_AnItemSwitchedOffGlobally_IsRefusedWithTheWordingThePhoneShows()
  {
    await using (var seeding = _context.Factory.CreateContext())
    {
      var item = await seeding.CatalogItems.FirstAsync(catalogItem => catalogItem.Id == _context.World.BratwurstItemId);
      item.IsActive = false;
      await seeding.SaveChangesAsync();
    }

    using var response = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid()));
    var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    await using var database = _context.Factory.CreateContext();
    var storedOrders = await database.Orders.CountAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(error.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("errors.order.itemSoldOut"));
                      Assert.That(error.RootElement.GetProperty("parameters").GetProperty("name").GetString(), Is.EqualTo("Bratwurst mit Brot"));
                      Assert.That(error.RootElement.GetProperty("parameters").GetProperty("catalogItemId").GetGuid(), Is.EqualTo(_context.World.BratwurstItemId));
                      Assert.That(storedOrders, Is.Zero);
                    });
  }

  [Test]
  public async Task PostOrder_ChosenStationSwitchedOff_IsRefusedWithTheWordingThePhoneShows()
  {
    await using (var seeding = _context.Factory.CreateContext())
    {
      seeding.ItemStationAssignments.Add(new()
                                         {
                                           Id = Guid.NewGuid(),
                                           FestivalId = _context.World.FestivalId,
                                           CatalogItemId = _context.World.BratwurstItemId,
                                           StationId = _context.World.BarStationId
                                         });
      var kitchen = await seeding.Stations.FirstAsync(station => station.Id == _context.World.KitchenStationId);
      kitchen.IsActive = false;
      await seeding.SaveChangesAsync();
    }

    OrderBody body = new(Guid.NewGuid(), _context.World.TableId, [new(_context.World.BratwurstItemId, 350, null, _context.World.KitchenStationId)]);

    using var response = await _context.PostOrderAsync(body);
    var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    await using var database = _context.Factory.CreateContext();
    var storedOrders = await database.Orders.CountAsync();
    var storedItems = await database.OrderItems.CountAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(error.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("errors.order.itemSoldOut"));
                      Assert.That(error.RootElement.GetProperty("parameters").GetProperty("name").GetString(), Is.EqualTo("Bratwurst mit Brot"));
                      Assert.That(error.RootElement.GetProperty("parameters").GetProperty("catalogItemId").GetGuid(), Is.EqualTo(_context.World.BratwurstItemId));
                      Assert.That(storedOrders, Is.Zero);
                      Assert.That(storedItems, Is.Zero);
                    });
  }

  [Test]
  public async Task PostOrder_ItemPrices_AreStoredAsThePhoneSentThem()
  {
    OrderBody body = new(Guid.NewGuid(),
                         _context.World.TableId,
                         [
                           new(_context.World.BratwurstItemId, 399, null, null),
                           new(_context.World.BratwurstItemId, 399, null, null)
                         ]);

    using var response = await _context.PostOrderAsync(body);
    var placed = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(stored.Select(item => item.UnitPriceCents), Is.All.EqualTo(399));
                      Assert.That(stored, Has.Count.EqualTo(2));
                      Assert.That(placed.RootElement.GetProperty("totalCents").GetInt32(), Is.EqualTo(798));
                    });
  }

  [TestCase("/api/orders")]
  [TestCase("/api/open-items/settle")]
  public async Task PostWithoutARequestBody_IsRefusedBeforeTheHandlerRuns(string route)
  {
    using HttpRequestMessage request = new(HttpMethod.Post, route);
    request.Headers.Authorization = new("Bearer", _context.DeviceToken);
    request.Content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

    using var response = await _context.Client.SendAsync(request);

    await using var database = _context.Factory.CreateContext();
    var orderCount = await database.Orders.CountAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(orderCount, Is.EqualTo(0));
                    });
  }
}
