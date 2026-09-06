using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class AdminDeviceEndpointsTest
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

  [Test]
  public async Task GetDevices_APhoneAndATablet_NamesTheOwnerAndTheKindOfEach()
  {
    using var response = await _context.Client.GetAsync("/api/admin/devices");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var devices = body.RootElement.GetProperty("devices");

    var phone = devices.EnumerateArray().Single(device => device.GetProperty("deviceKind").GetString() == "staffMember");
    var tablet = devices.EnumerateArray().Single(device => device.GetProperty("deviceKind").GetString() == "station");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(devices.GetArrayLength(), Is.EqualTo(2));
                      Assert.That(phone.GetProperty("ownerName").GetString(), Is.EqualTo("Anna"));
                      Assert.That(phone.GetProperty("ownerId").GetGuid(), Is.EqualTo(_context.World.StaffMemberId));
                      Assert.That(tablet.GetProperty("ownerName").GetString(), Is.EqualTo("Kueche"));
                      Assert.That(tablet.GetProperty("ownerId").GetGuid(), Is.EqualTo(_context.World.KitchenStationId));
                    });
  }

  [Test]
  public async Task PostRevoke_AnEnrolledTablet_InvalidatesItsTokenAndClearsTheStationPointer()
  {
    Guid tabletDeviceId;

    await using (var database = _context.Factory.CreateContext())
    {
      var station = await database.Stations.SingleAsync(candidate => candidate.Id == _context.World.KitchenStationId);
      tabletDeviceId = station.DeviceId!.Value;
    }

    using (var revocation = await _context.Client.PostAsync($"/api/admin/devices/{tabletDeviceId}/revoke", null))
    {
      Assert.That(revocation.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var afterRevocation = await _context.SendAsAsync(_stationToken, HttpMethod.Get, "/api/session");

    await using var check = _context.Factory.CreateContext();
    var kitchen = await check.Stations.SingleAsync(candidate => candidate.Id == _context.World.KitchenStationId);

    Assert.Multiple(() =>
                    {
                      Assert.That(afterRevocation.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
                      Assert.That(kitchen.DeviceId, Is.Null);
                    });
  }

  [Test]
  public async Task PostRevoke_ADeviceThatNeverExisted_AnswersNotFound()
  {
    using var response = await _context.Client.PostAsync($"/api/admin/devices/{Guid.NewGuid()}/revoke", null);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
  }
}
