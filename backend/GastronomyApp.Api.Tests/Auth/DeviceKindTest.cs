using System.Net;
using System.Text.Json;
using GastronomyApp.Api.Tests.Endpoints;

namespace GastronomyApp.Api.Tests.Auth;

[TestFixture]
public sealed class DeviceKindTest
{
  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContext.Builder().StartAsync();
    _stationToken = await _context.IssueStationTokenAsync(_context.World.KitchenStationId);
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
  }

  private OrderTestContext _context = null!;
  private string _stationToken = null!;

  [TestCase("/api/catalog")]
  [TestCase("/api/open-items")]
  [TestCase("/api/estimates")]
  public async Task Get_WaiterRouteWithAStationTablet_IsRefusedAsTheWrongKindOfDevice(string path)
  {
    using var response = await _context.SendAsAsync(_stationToken, HttpMethod.Get, path);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("auth.wrongDeviceKind"));
                    });
  }

  [Test]
  public async Task Get_StationRouteWithAWaiterPhone_IsRefusedAsTheWrongKindOfDevice()
  {
    using var response = await _context.SendAsync(HttpMethod.Get, "/api/station/orders");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("auth.wrongDeviceKind"));
                    });
  }

  [Test]
  public async Task Post_OrderPlacementWithAStationTablet_IsRefusedAsTheWrongKindOfDevice()
  {
    using var response = await _context.SendAsAsync(_stationToken,
                                                    HttpMethod.Post,
                                                    "/api/orders",
                                                    _context.BuildOrder(Guid.NewGuid()));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
  }

  [Test]
  public async Task Get_SessionWithAStationTablet_NamesTheStationItBelongsTo()
  {
    using var response = await _context.SendAsAsync(_stationToken, HttpMethod.Get, "/api/session");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("deviceKind").GetString(), Is.EqualTo("station"));
                      Assert.That(body.RootElement.GetProperty("station").GetProperty("name").GetString(),
                                  Is.EqualTo("Kueche"));
                    });
  }
}
