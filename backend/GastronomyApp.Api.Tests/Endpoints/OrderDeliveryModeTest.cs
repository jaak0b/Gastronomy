using System.Net;
using System.Text.Json;
using GastronomyApp.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

public sealed record DeliveryModeBody(Guid StationId, string DeliveryMode);

public sealed record OrderWithDeliveryModesBody(
  Guid ClientOrderId,
  string TableName,
  string? Note,
  IReadOnlyList<OrderItemBody> Items,
  IReadOnlyList<DeliveryModeBody> DeliveryModes);

[TestFixture]
public sealed class OrderDeliveryModeTest
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
  public async Task PostOrder_ADeliveryModeForOneStation_StoresItAndLeavesTheOtherStationTogether()
  {
    OrderWithDeliveryModesBody body = new(Guid.NewGuid(),
                                          "Tisch 3",
                                          null,
                                          [
                                            new(_context.World.BratwurstItemId, 350, null, null),
                                            new(_context.World.BeerItemId, 300, null, null)
                                          ],
                                          [new(_context.World.BarStationId, "asItComes")]);

    using var response = await _context.SendAsync(HttpMethod.Post, "/api/orders", body);
    var placed = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    await using var database = _context.Factory.CreateContext();
    var kitchenSlice = await database.StationOrders
                                     .SingleAsync(slice => slice.StationId == _context.World.KitchenStationId);
    var barSlice = await database.StationOrders
                                 .SingleAsync(slice => slice.StationId == _context.World.BarStationId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                      Assert.That(kitchenSlice.DeliveryMode, Is.EqualTo(DeliveryMode.Together));
                      Assert.That(barSlice.DeliveryMode, Is.EqualTo(DeliveryMode.AsItComes));
                      Assert.That(DeliveryModeOf(placed, _context.World.BarStationId), Is.EqualTo("asItComes"));
                      Assert.That(DeliveryModeOf(placed, _context.World.KitchenStationId), Is.EqualTo("together"));
                    });
  }

  [Test]
  public async Task PostOrder_TheSameSubmissionSentTwice_StillAnswersWithTheOriginalOrder()
  {
    OrderWithDeliveryModesBody body = new(Guid.NewGuid(),
                                          "Tisch 3",
                                          null,
                                          [new(_context.World.BeerItemId, 300, null, null)],
                                          [new(_context.World.BarStationId, "asItComes")]);

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

  private string DeliveryModeOf(JsonDocument placed, Guid stationId)
  {
    return placed.RootElement
                 .GetProperty("stationOrders")
                 .EnumerateArray()
                 .Single(slice => slice.GetProperty("stationId").GetGuid() == stationId)
                 .GetProperty("deliveryMode")
                 .GetString()!;
  }
}
