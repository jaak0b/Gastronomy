using System.Net;
using System.Text.Json;
using GastronomyApp.Api.Tests.TestSupport;
using GastronomyApp.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class OrderDeliveryModeTest
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
  public async Task PostOrder_ADeliveryModeForOneStation_StoresItAndLeavesTheOtherStationTogether()
  {
    OrderWithDeliveryModesBody body = new(Guid.NewGuid(),
                                          "Tisch 3",
                                          [
                                            new(_context.World.BratwurstItemId, 350, null, null),
                                            new(_context.World.BeerItemId, 300, null, null)
                                          ],
                                          [new(_context.World.BarStationId, "asItComes")]);

    using var response = await _context.SendAsync(HttpMethod.Post, "/api/orders", body);
    var placed = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    await using var database = _context.Factory.CreateContext();
    var kitchenStationOrder = await database.StationOrders.SingleAsync(stationOrder => stationOrder.StationId == _context.World.KitchenStationId);
    var barStationOrder = await database.StationOrders.SingleAsync(stationOrder => stationOrder.StationId == _context.World.BarStationId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                      Assert.That(kitchenStationOrder.DeliveryMode, Is.EqualTo(DeliveryMode.Together));
                      Assert.That(barStationOrder.DeliveryMode, Is.EqualTo(DeliveryMode.AsItComes));
                      Assert.That(ReadDeliveryMode(placed, _context.World.BarStationId), Is.EqualTo("asItComes"));
                      Assert.That(ReadDeliveryMode(placed, _context.World.KitchenStationId), Is.EqualTo("together"));
                    });
  }

  [Test]
  public async Task PostOrder_TheSameSubmissionSentTwice_StillAnswersWithTheOriginalOrder()
  {
    OrderWithDeliveryModesBody body = new(Guid.NewGuid(), "Tisch 3", [new(_context.World.BeerItemId, 300, null, null)], [new(_context.World.BarStationId, "asItComes")]);

    string firstBody;
    using (var first = await _context.SendAsync(HttpMethod.Post, "/api/orders", body))
    {
      Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));
      firstBody = await first.Content.ReadAsStringAsync();
    }

    using var second = await _context.SendAsync(HttpMethod.Post, "/api/orders", body);
    var secondBody = await second.Content.ReadAsStringAsync();

    await using var database = _context.Factory.CreateContext();

    Assert.Multiple(() =>
                    {
                      Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(secondBody, Is.EqualTo(firstBody));
                      Assert.That(database.Orders.Count(), Is.EqualTo(1));
                    });
  }

  private string ReadDeliveryMode(JsonDocument placed, Guid stationId)
  {
    return placed.RootElement.GetProperty("stationOrders").EnumerateArray().Single(stationOrder => stationOrder.GetProperty("stationId").GetGuid() == stationId).GetProperty("deliveryMode").GetString()!;
  }
}
