using System.Net;
using System.Text.Json;
using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class OrderEndpointsTest
{

  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContext.Builder().StartAsync();
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
                      Assert.That(body.RootElement.GetProperty("totalCents").GetInt32(), Is.EqualTo(700));
                      Assert.That(body.RootElement.GetProperty("totalCents").GetInt32(), Is.EqualTo(700));
                      Assert.That(body.RootElement.GetProperty("stationOrders").GetArrayLength(), Is.EqualTo(1));
                    });

    var ticket = body.RootElement.GetProperty("stationOrders")[0];

    Assert.Multiple(() =>
                    {
                      Assert.That(ticket.GetProperty("stationId").GetGuid(), Is.EqualTo(_context.World.KitchenStationId));
                      Assert.That(ticket.GetProperty("stationName").GetString(), Is.EqualTo("Kueche"));
                      Assert.That(ticket.GetProperty("stationOrderNumber").GetInt32(), Is.EqualTo(1));
                      Assert.That(ticket.GetProperty("itemIds").GetArrayLength(), Is.EqualTo(2));
                    });

    await using var database = _context.Factory.CreateContext();
    var stored = await database.Orders.SingleAsync();

    Assert.That(stored.GlobalOrderNumber, Is.EqualTo(1));
  }

  [Test]
  public async Task PostOrder_TwoStations_CreatesOnePrintJobPerStationOrder()
  {
    OrderBody twoStations = new(Guid.NewGuid(),
                                "Tisch 12",
                                null,
                                [
                                  new(_context.World.BratwurstItemId, 350, null, null), new(_context.World.BratwurstItemId, 350, null, null),
                                  new(_context.World.BeerItemId, 350, null, null)
                                ]);

    using var response = await _context.PostOrderAsync(twoStations);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    await using var database = _context.Factory.CreateContext();
    var printJobCount = await database.PrintJobs.CountAsync(job => job.CopyNumber == 0);

    Assert.That(printJobCount, Is.EqualTo(2));
  }

  [Test]
  public async Task PostOrder_UnknownItemId_IsRefusedAsUnprocessable()
  {
    OrderBody unknownItem = new(Guid.NewGuid(),
                                "Tisch 12",
                                null,
                                [new(Guid.NewGuid(), 350, null, null)]);

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
    OrderBody empty = new(Guid.NewGuid(), "Tisch 12", null, []);

    using var response = await _context.PostOrderAsync(empty);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
  }

  [Test]
  public async Task PostOrder_SoldOutItem_IsStillAccepted()
  {
    await using (var database = _context.Factory.CreateContext())
    {
      var item = await database.CatalogItems.FirstAsync(candidate => candidate.Id == _context.World.BratwurstItemId);
      item.IsAvailable = false;
      await database.SaveChangesAsync();
    }

    using var response = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid()));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
  }

  [Test]
  public async Task PostOrder_ItemPrices_AreStoredAsThePhoneSentThem()
  {
    OrderBody body = new(Guid.NewGuid(),
                         "Tisch 12",
                         null,
                         [new(_context.World.BratwurstItemId, 399, null, null), new(_context.World.BratwurstItemId, 399, null, null)]);

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

  [Test]
  public async Task GetPrinterStatus_StationWithNoPrinter_IsOnlineBecauseNothingAboutItHasFailed()
  {
    await using (var database = _context.Factory.CreateContext())
    {
      var kitchen = await database.Stations.FirstAsync(station => station.Id == _context.World.KitchenStationId);
      kitchen.PrinterId = null;
      await database.SaveChangesAsync();
    }

    using var response = await _context.SendAsync(HttpMethod.Get, "/api/printers/status");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    var kitchenStatus = body.RootElement
                            .GetProperty("stations")
                            .EnumerateArray()
                            .Single(station => station.GetProperty("stationId").GetGuid() == _context.World.KitchenStationId);

    Assert.Multiple(() =>
                    {
                      Assert.That(kitchenStatus.GetProperty("isOnline").GetBoolean(), Is.True);
                      Assert.That(kitchenStatus.GetProperty("isFaulty").GetBoolean(), Is.False);
                    });
  }

  [Test]
  public async Task GetPrinterStatus_SeededStations_ReportsEveryStation()
  {
    using var response = await _context.SendAsync(HttpMethod.Get, "/api/printers/status");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("stations").GetArrayLength(), Is.EqualTo(2));
                    });
  }
}
