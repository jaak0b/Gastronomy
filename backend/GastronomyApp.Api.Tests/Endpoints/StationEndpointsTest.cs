using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class StationEndpointsTest
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
  public async Task GetStations_NoDeviceToken_IsRefused()
  {
    using var response = await _context.Client.GetAsync("/api/stations");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
  }

  [Test]
  public async Task GetStations_EnrolledDevice_ListsEveryActiveStationInTheOrderTheAdminChose()
  {
    using var response = await _context.SendAsync(HttpMethod.Get, "/api/stations");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var stations = body.RootElement.GetProperty("stations");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(stations.GetArrayLength(), Is.EqualTo(2));
                      Assert.That(stations[0].GetProperty("stationId").GetGuid(),
                                  Is.EqualTo(_context.World.KitchenStationId));
                      Assert.That(stations[0].GetProperty("name").GetString(), Is.EqualTo("Kueche"));
                    });
  }

  [Test]
  public async Task GetStations_StationSwitchedOff_LeavesItOutOfTheList()
  {
    await using (var database = _context.Factory.CreateContext())
    {
      var bar = await database.Stations.FirstAsync(station => station.Id == _context.World.BarStationId);
      bar.IsActive = false;
      await database.SaveChangesAsync();
    }

    using var response = await _context.SendAsync(HttpMethod.Get, "/api/stations");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var stations = body.RootElement.GetProperty("stations");

    Assert.Multiple(() =>
                    {
                      Assert.That(stations.GetArrayLength(), Is.EqualTo(1));
                      Assert.That(stations[0].GetProperty("stationId").GetGuid(),
                                  Is.EqualTo(_context.World.KitchenStationId));
                    });
  }
}
