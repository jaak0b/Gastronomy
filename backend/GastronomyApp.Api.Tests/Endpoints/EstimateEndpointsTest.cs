using System.Net;
using System.Text.Json;
using GastronomyApp.Api.Tests.TestSupport;
using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class EstimateEndpointsTest
{
  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContextBuilder().StartAsync();

    await using var database = _context.Factory.CreateContext();
    var bratwurst = await database.CatalogItems.FirstAsync(item => item.Id == _context.World.BratwurstItemId);
    bratwurst.ProductionMinutes = 4;
    await database.SaveChangesAsync();
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
  }

  private OrderTestContext _context = null!;

  [Test]
  public async Task GetEstimates_TwoOpenBratwursts_ReportsTheNextBratwurstAfterThemWithTheWireNames()
  {
    await PlaceAsync(_context.BuildOrder(Guid.NewGuid()));

    var estimates = await ReadEstimatesAsync();
    var bratwurst = estimates.EnumerateArray().Single();

    Assert.Multiple(() =>
                    {
                      Assert.That(bratwurst.EnumerateObject().Select(property => property.Name),
                                  Is.EquivalentTo(new[]
                                                  {
                                                    "catalogItemId",
                                                    "stationId",
                                                    "readyInMinutes"
                                                  }));
                      Assert.That(bratwurst.GetProperty("catalogItemId").GetGuid(), Is.EqualTo(_context.World.BratwurstItemId));
                      Assert.That(bratwurst.GetProperty("stationId").GetGuid(), Is.EqualTo(_context.World.KitchenStationId));
                      Assert.That(bratwurst.GetProperty("readyInMinutes").GetDouble(), Is.EqualTo(12));
                    });
  }

  [Test]
  public async Task GetEstimates_HandedOutItems_LeavesThemOutOfTheQueue()
  {
    await PlaceAsync(_context.BuildOrder(Guid.NewGuid()));

    await using (var database = _context.Factory.CreateContext())
    {
      List<OrderItem> items = await database.OrderItems.ToListAsync();

      foreach (var item in items)
        item.FulfilledAtUtc = DateTime.UtcNow;

      await database.SaveChangesAsync();
    }

    Assert.That(ReadyInMinutesOf(await ReadEstimatesAsync(), _context.World.BratwurstItemId), Is.EqualTo(4));
  }

  [Test]
  public async Task GetEstimates_AnIndependentArticle_CountsOnlyWhatIsStillOpenOfItself()
  {
    await MakeBeerIndependentAsync(3);

    await PlaceAsync(new(Guid.NewGuid(),
                         "Tisch 12",
                         [
                           new(_context.World.BeerItemId, 300, null, null),
                           new(_context.World.BratwurstItemId, 350, null, null)
                         ]));

    var estimates = await ReadEstimatesAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(ReadyInMinutesOf(estimates, _context.World.BeerItemId), Is.EqualTo(6));
                      Assert.That(ReadyInMinutesOf(estimates, _context.World.BratwurstItemId), Is.EqualTo(8));
                    });
  }

  [Test]
  public async Task GetEstimates_HalfMinutesInTheQueue_ReportsTheFraction()
  {
    await using (var database = _context.Factory.CreateContext())
    {
      var bratwurst = await database.CatalogItems.FirstAsync(item => item.Id == _context.World.BratwurstItemId);
      bratwurst.ProductionMinutes = 1.5;
      await database.SaveChangesAsync();
    }

    await PlaceAsync(_context.BuildOrder(Guid.NewGuid()));

    Assert.That(ReadyInMinutesOf(await ReadEstimatesAsync(), _context.World.BratwurstItemId), Is.EqualTo(4.5));
  }

  [Test]
  public async Task PostQuote_TwoMoreBratwurstsBehindTwoOpenOnes_AnswersWithTheMinutesOfTheKitchen()
  {
    await PlaceAsync(_context.BuildOrder(Guid.NewGuid()));

    using var response = await _context.SendAsync(HttpMethod.Post, "/api/estimates/quote", new { lines = new[] { new { catalogItemId = _context.World.BratwurstItemId, stationId = _context.World.KitchenStationId, units = 2 } } });
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var kitchen = body.RootElement.GetProperty("stations").EnumerateArray().Single();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(kitchen.GetProperty("stationId").GetGuid(), Is.EqualTo(_context.World.KitchenStationId));
                      Assert.That(kitchen.GetProperty("readyInMinutes").GetDouble(), Is.EqualTo(16));
                    });
  }

  [Test]
  public async Task PostQuote_AnUnknownArticle_IsRefusedWithTheErrorEnvelope()
  {
    using var response = await _context.SendAsync(HttpMethod.Post, "/api/estimates/quote", new { lines = new[] { new { catalogItemId = Guid.NewGuid(), stationId = _context.World.KitchenStationId, units = 1 } } });
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("code").GetString(), Is.EqualTo("ValidationFailed"));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("order.cannotBeProcessed"));
                    });
  }

  private async Task MakeBeerIndependentAsync(double productionMinutes)
  {
    await using var database = _context.Factory.CreateContext();
    var beer = await database.CatalogItems.FirstAsync(item => item.Id == _context.World.BeerItemId);
    beer.ProductionMinutes = productionMinutes;
    beer.IsQueueIndependent = true;
    await database.SaveChangesAsync();
  }

  private async Task PlaceAsync(OrderBody order)
  {
    using var placed = await _context.PostOrderAsync(order);

    Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
  }

  private double ReadyInMinutesOf(JsonElement estimates, Guid catalogItemId)
  {
    return estimates.EnumerateArray().Single(estimate => estimate.GetProperty("catalogItemId").GetGuid() == catalogItemId).GetProperty("readyInMinutes").GetDouble();
  }

  private async Task<JsonElement> ReadEstimatesAsync()
  {
    using var response = await _context.SendAsync(HttpMethod.Get, "/api/estimates");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.Clone();
  }
}
