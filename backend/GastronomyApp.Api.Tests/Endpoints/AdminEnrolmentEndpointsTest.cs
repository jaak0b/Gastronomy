using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class AdminEnrolmentEndpointsTest
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
  public async Task PostInvitation_NeitherAPersonNorAStation_IsRefused()
  {
    using var response = await CreateInvitationAsync(new { });
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("enrolment.exactlyOneOwnerRequired"));
                    });
  }

  [Test]
  public async Task PostInvitation_BothAPersonAndAStation_IsRefused()
  {
    using var response = await CreateInvitationAsync(new
                                                     {
                                                       staffMemberId = _context.World.StaffMemberId,
                                                       stationId = _context.World.KitchenStationId
                                                     });
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("enrolment.exactlyOneOwnerRequired"));
                    });
  }

  [Test]
  public async Task PostInvitation_ForAStation_NamesTheStationItBelongsTo()
  {
    using var response = await CreateInvitationAsync(new { stationId = _context.World.KitchenStationId });
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                      Assert.That(body.RootElement.GetProperty("ownerKind").GetString(), Is.EqualTo("station"));
                      Assert.That(body.RootElement.GetProperty("station").GetProperty("name").GetString(),
                                  Is.EqualTo("Kueche"));
                    });
  }

  [Test]
  public async Task PostRedeem_AStationInvitation_HandsOutATabletThatBelongsToThatStation()
  {
    var code = await CodeOfNewInvitationAsync(new { stationId = _context.World.KitchenStationId });

    using var response = await RedeemAsync(code);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    await using var database = _context.Factory.CreateContext();
    var station = await database.Stations.SingleAsync(candidate => candidate.Id == _context.World.KitchenStationId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("deviceKind").GetString(), Is.EqualTo("station"));
                      Assert.That(body.RootElement.GetProperty("station").GetProperty("id").GetGuid(),
                                  Is.EqualTo(_context.World.KitchenStationId));
                      Assert.That(body.RootElement.TryGetProperty("staffMember", out var staffMember), Is.True);
                      Assert.That(staffMember.ValueKind, Is.EqualTo(JsonValueKind.Null));
                      Assert.That(station.DeviceId,
                                  Is.EqualTo(body.RootElement.GetProperty("deviceId").GetGuid()));
                    });
  }

  [Test]
  public async Task PostRedeem_AStationThatWasSwitchedOffMeanwhile_IsRefusedAndSaysSo()
  {
    var code = await CodeOfNewInvitationAsync(new { stationId = _context.World.BarStationId });

    await using (var database = _context.Factory.CreateContext())
    {
      var bar = await database.Stations.SingleAsync(candidate => candidate.Id == _context.World.BarStationId);
      bar.IsActive = false;
      await database.SaveChangesAsync();
    }

    using var response = await RedeemAsync(code);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("enrolment.stationIsOffTheList"));
                    });
  }

  [Test]
  public async Task PostInvitation_ForAStationThatAlreadyHasATablet_RevokesTheOldTabletImmediately()
  {
    var firstToken = await _context.IssueStationTokenAsync(_context.World.KitchenStationId);

    using (var invitation = await CreateInvitationAsync(new { stationId = _context.World.KitchenStationId }))
    {
      Assert.That(invitation.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    using var response = await _context.SendAsAsync(firstToken, HttpMethod.Get, "/api/session");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
  }

  [Test]
  public async Task PostStaffMember_ANewName_PutsThemOnTheListAsActive()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/staff-members", new { name = "Bernd" });
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    await using var database = _context.Factory.CreateContext();
    var created = await database.StaffMembers
                                .SingleAsync(candidate => candidate.Id == body.RootElement.GetProperty("id").GetGuid());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                      Assert.That(created.Name, Is.EqualTo("Bernd"));
                      Assert.That(created.IsActive, Is.True);
                    });
  }

  [Test]
  public async Task PostStaffMember_ABlankName_IsRefused()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/staff-members", new { name = "   " });
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.personNameMissing"));
                    });
  }

  private Task<HttpResponseMessage> CreateInvitationAsync(object body)
  {
    return _context.Client.PostAsJsonAsync("/api/admin/enrolment/invitations", body);
  }

  private Task<HttpResponseMessage> RedeemAsync(string code)
  {
    return _context.Client.PostAsJsonAsync("/api/enrolment/redeem", new RedeemBody(code, "NUnit"));
  }

  private async Task<string> CodeOfNewInvitationAsync(object body)
  {
    using var response = await CreateInvitationAsync(body);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var created = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var qrUrl = created.RootElement.GetProperty("qrUrl").GetString()!;

    return qrUrl[(qrUrl.LastIndexOf('/') + 1)..];
  }
}
