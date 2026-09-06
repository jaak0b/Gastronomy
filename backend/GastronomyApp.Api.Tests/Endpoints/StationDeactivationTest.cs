using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class StationDeactivationTest
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
  public async Task Deactivate_FreshStationThatNeverTookAnOrder_SwitchesOff()
  {
    var stationId = await CreateStationAsync();

    using var response = await _context.Client.PostAsync($"/api/admin/stations/{stationId}/deactivate",
                                                        null);

    var body = await response.Content.ReadAsStringAsync();

    Assert.That(response.StatusCode,
                Is.EqualTo(HttpStatusCode.OK),
                $"A station with no orders must switch off. Body: {body}");

    await using var database = _context.Factory.CreateContext();
    var station = await database.Stations.FirstAsync(candidate => candidate.Id == stationId);

    Assert.That(station.IsActive, Is.False);
  }

  [Test]
  public async Task Deactivate_StationWithAnItemStillToBeMade_IsRefusedAndCountsThem()
  {
    using (var placed = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid())))
    {
      Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    using var response = await _context.Client.PostAsync($"/api/admin/stations/{_context.World.KitchenStationId}/deactivate",
                                                        null);

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.stationHasUnfinishedItems"));
                      Assert.That(body.RootElement.GetProperty("parameters").GetProperty("count").GetString(), Is.EqualTo("2"));
                    });
  }

  [Test]
  public async Task Deactivate_StationWhoseItemsWouldLoseTheirOnlyStation_IsRefusedForThatReason()
  {
    using var response = await _context.Client.PostAsync($"/api/admin/stations/{_context.World.BarStationId}/deactivate",
                                                        null);

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.itemsWouldHaveNoStation"),
                                  "A station losing its items is a different refusal from one still owing food.");
                    });
  }

  [Test]
  public async Task Deactivate_StationWhoseItemsAreAllFinished_SwitchesOff()
  {
    using (var placed = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid())))
    {
      Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    await using (var database = _context.Factory.CreateContext())
    {
      List<OrderItem> items = await database.OrderItems.ToListAsync();

      foreach (var item in items)
      {
        item.ProductionStatus = ProductionStatus.Finished;
      }

      await database.SaveChangesAsync();
    }

    using (var assigned = await _context.Client.PutAsJsonAsync($"/api/admin/items/{_context.World.BratwurstItemId}",
                                                              new
                                                              {
                                                                name = "Bratwurst mit Brot",
                                                                categoryName = "Essen",
                                                                priceCents = 350,
                                                                sortOrder = 1,
                                                                stationIds = new[] { _context.World.BarStationId }
                                                              }))
    {
      Assert.That(assigned.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var response = await _context.Client.PostAsync($"/api/admin/stations/{_context.World.KitchenStationId}/deactivate",
                                                        null);

    Assert.That(response.StatusCode,
                Is.EqualTo(HttpStatusCode.OK),
                $"Finished items must not block a station. Body: {await response.Content.ReadAsStringAsync()}");
  }

  [Test]
  public async Task Activate_StationThatWasSwitchedOff_SwitchesItBackOn()
  {
    var stationId = await CreateStationAsync();

    using (var switchedOff = await _context.Client.PostAsync($"/api/admin/stations/{stationId}/deactivate",
                                                            null))
    {
      Assert.That(switchedOff.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var response = await _context.Client.PostAsync($"/api/admin/stations/{stationId}/activate",
                                                        null);

    var body = await response.Content.ReadAsStringAsync();

    Assert.That(response.StatusCode,
                Is.EqualTo(HttpStatusCode.OK),
                $"A station that was switched off must be switchable back on. Body: {body}");

    await using var database = _context.Factory.CreateContext();
    var station = await database.Stations.FirstAsync(candidate => candidate.Id == stationId);

    Assert.That(station.IsActive, Is.True);
  }

  private async Task<Guid> CreateStationAsync()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/stations",
                                                              new { name = "Zelt", sortOrder = 3 });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.GetProperty("stationId").GetGuid();
  }
}
