using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class SessionEndpointsTest
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
  public async Task GetSession_AWaiterPhone_NamesTheWaiterHoldingIt()
  {
    using var response = await _context.SendAsync(HttpMethod.Get, "/api/session");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("deviceId").GetGuid(),
                                  Is.EqualTo(_context.DeviceId));
                      Assert.That(body.RootElement.GetProperty("staffMember").GetProperty("id").GetGuid(),
                                  Is.EqualTo(_context.World.StaffMemberId));
                    });
  }

  [Test]
  public async Task PutLanguage_ALanguageTheAppSpeaks_StoresItOnTheDevice()
  {
    using var response = await _context.SendAsync(HttpMethod.Put, "/api/session/language", new { language = "en" });

    await using var database = _context.Factory.CreateContext();
    var device = await database.Devices.FirstAsync(candidate => candidate.Id == _context.DeviceId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
                      Assert.That(device.Language, Is.EqualTo("en"));
                    });
  }

  [Test]
  public async Task PutLanguage_ALanguageTheAppDoesNotSpeak_IsRefusedAndLeavesTheDeviceAsItWas()
  {
    using var response = await _context.SendAsync(HttpMethod.Put, "/api/session/language", new { language = "fr" });
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    await using var database = _context.Factory.CreateContext();
    var device = await database.Devices.FirstAsync(candidate => candidate.Id == _context.DeviceId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("session.unsupportedLanguage"));
                      Assert.That(device.Language, Is.EqualTo("de"));
                    });
  }
}
