using System.Net;
using System.Text.Json;
using GastronomyApp.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class EstimateEndpointsTest
{
  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContext.Builder().StartAsync();

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
  public async Task GetEstimates_NoOrdersYet_ReportsEveryActiveStationAsEmpty()
  {
    var stations = await ReadEstimatesAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(stations.GetArrayLength(), Is.EqualTo(2));
                      Assert.That(stations.EnumerateArray().Select(station => station.GetProperty("queuedMinutes").GetInt32()),
                                  Is.All.Zero);
                    });
  }

  [Test]
  public async Task GetEstimates_TwoUnfinishedItems_SumsTheMinutesOfTheStationThatHasToMakeThem()
  {
    using (var placed = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid())))
    {
      Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    var stations = await ReadEstimatesAsync();

    Assert.That(QueuedMinutesOf(stations, _context.World.KitchenStationId), Is.EqualTo(8));
  }

  [Test]
  public async Task GetEstimates_ItemsAlreadyFinished_LeavesThemOutOfTheSum()
  {
    using (var placed = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid())))
    {
      Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    await using (var database = _context.Factory.CreateContext())
    {
      var items = await database.OrderItems.ToListAsync();

      foreach (var item in items)
      {
        item.ProductionStatus = ProductionStatus.Finished;
      }

      await database.SaveChangesAsync();
    }

    var stations = await ReadEstimatesAsync();

    Assert.That(QueuedMinutesOf(stations, _context.World.KitchenStationId), Is.Zero);
  }

  [Test]
  public async Task GetEstimates_AnItemWithoutAStatedDuration_CountsAsNoTimeAtAll()
  {
    OrderBody beerOnly = new(Guid.NewGuid(),
                             "Tisch 12",
                             null,
                             [new(_context.World.BeerItemId, 300, null, null)]);

    using (var placed = await _context.PostOrderAsync(beerOnly))
    {
      Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    var stations = await ReadEstimatesAsync();

    Assert.That(QueuedMinutesOf(stations, _context.World.BarStationId), Is.Zero);
  }

  private int QueuedMinutesOf(JsonElement stations, Guid stationId)
  {
    return stations.EnumerateArray()
                   .Single(station => station.GetProperty("stationId").GetGuid() == stationId)
                   .GetProperty("queuedMinutes")
                   .GetInt32();
  }

  private async Task<JsonElement> ReadEstimatesAsync()
  {
    using var response = await _context.SendAsync(HttpMethod.Get, "/api/estimates");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.GetProperty("stations").Clone();
  }
}
