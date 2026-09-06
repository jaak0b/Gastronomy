using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Api.Tests.Logging;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class EnrolmentLoggingTest
{

  [SetUp]
  public async Task SetUp()
  {
    _log = new();
    _context = await new OrderTestContext.Builder().StartAsync();
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
    _log.Dispose();
  }

  private OrderTestContext _context = null!;
  private RecordedLog _log = null!;

  private sealed record CreatedInvitation(Guid InvitationId, string Code);

  private async Task<CreatedInvitation> CreateInvitationAsync()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/enrolment/invitations",
                                                              new { staffMemberId = _context.World.StaffMemberId });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var qrUrl = body.RootElement.GetProperty("qrUrl").GetString()!;

    return new(body.RootElement.GetProperty("invitationId").GetGuid(),
               qrUrl[(qrUrl.LastIndexOf('/') + 1)..]);
  }

  private Task<HttpResponseMessage> RedeemAsync(string code)
  {
    return _context.Client.PostAsJsonAsync("/api/enrolment/redeem", new RedeemBody(code, null, "NUnit"));
  }

  private async Task AgeTheInvitationAsync(Guid invitationId)
  {
    await using var database = _context.Factory.CreateContext();
    var invitation = await database.EnrolmentInvitations
                                   .SingleAsync(candidate => candidate.Id == invitationId);
    invitation.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
    await database.SaveChangesAsync();
  }

  [Test]
  public async Task PostInvitation_Created_WritesTheInvitationIdAndTheAddressToTheLog()
  {
    var invitation = await CreateInvitationAsync();

    Assert.That(_log.RenderedMessages,
                Has.Some.Contains("Enrolment invitation").And.Some.Contains(invitation.InvitationId.ToString()));
  }

  [Test]
  public async Task PostRedeem_Redeemed_WritesTheInvitationTheDeviceAndThePersonToTheLog()
  {
    var invitation = await CreateInvitationAsync();

    using var response = await RedeemAsync(invitation.Code);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var deviceId = body.RootElement.GetProperty("deviceId").GetGuid();
    var staffMemberId = body.RootElement.GetProperty("staffMember").GetProperty("id").GetGuid();

    Assert.That(_log.RenderedMessages,
                Has.Some.Contains(invitation.InvitationId.ToString())
                        .And.Contains(deviceId.ToString())
                        .And.Contains(staffMemberId.ToString()));
  }

  [Test]
  public async Task PostRedeem_Redeemed_NeverWritesTheDeviceTokenOrTheCodeToTheLog()
  {
    var invitation = await CreateInvitationAsync();

    using var response = await RedeemAsync(invitation.Code);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var deviceToken = body.RootElement.GetProperty("deviceToken").GetString()!;

    Assert.Multiple(() =>
                    {
                      Assert.That(deviceToken, Is.Not.Empty);
                      Assert.That(_log.RenderedMessages,
                                  Has.None.Contains(deviceToken),
                                  "A device token is a credential and must never reach the log file.");
                      Assert.That(_log.RenderedMessages,
                                  Has.None.Contains(invitation.Code),
                                  "The enrolment code is a credential and must never reach the log file.");
                    });
  }

  [Test]
  public async Task PostRedeem_CodeThatMatchesNoInvitation_WritesTheReasonToTheLog()
  {
    await CreateInvitationAsync();

    using var response = await RedeemAsync("00000000000000000000000000000000");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                      Assert.That(_log.RenderedMessages, Has.Some.Contains("does not match the invitation"));
                    });
  }

  [Test]
  public async Task PostRedeem_CodeThatWasAlreadyUsed_WritesTheReasonToTheLog()
  {
    var invitation = await CreateInvitationAsync();

    using (var first = await RedeemAsync(invitation.Code))
    {
      Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var second = await RedeemAsync(invitation.Code);

    Assert.Multiple(() =>
                    {
                      Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
                      Assert.That(_log.RenderedMessages, Has.Some.Contains("no invitation is outstanding"));
                    });
  }

  [Test]
  public async Task PostRedeem_CodePastItsFiveMinutes_WritesThatItExpiredToTheLog()
  {
    var invitation = await CreateInvitationAsync();
    await AgeTheInvitationAsync(invitation.InvitationId);

    using var response = await RedeemAsync(invitation.Code);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
                      Assert.That(_log.RenderedMessages, Has.Some.Contains("had already expired"));
                    });
  }
}


