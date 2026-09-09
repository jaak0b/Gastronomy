using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class EnrolmentEndpointsTest
{

  [SetUp]
  public async Task SetUp()
  {
    _factory = await new ApiTestFactory.Builder().StartAsync();
    await using var context = _factory.CreateContext();
    _world = await new ApiSeeder().SeedAsync(context, CancellationToken.None);
  }

  [TearDown]
  public async Task TearDown()
  {
    await _factory.DisposeAsync();
  }

  private ApiTestFactory _factory = null!;
  private SeededWorld _world = null!;

  [Test]
  public async Task PostRedeem_QrCodeForm_ReturnsTheDeviceTokenOnce()
  {
    var invitation = await CreateInvitationAsync();

    using var response = await RedeemAsync(invitation.QrCodeValue);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("deviceToken").GetString(), Is.Not.Empty);
                      Assert.That(body.RootElement.GetProperty("deviceId").GetGuid(), Is.Not.EqualTo(Guid.Empty));
                      Assert.That(body.RootElement.GetProperty("staffMember").GetProperty("name").GetString(),
                                  Is.EqualTo("Anna"));
                      Assert.That(body.RootElement.GetProperty("language").GetString(), Is.Not.Empty);
                    });
  }

  [Test]
  public async Task PostRedeem_NeitherCodeForm_IsRefused()
  {
    await CreateInvitationAsync();

    using var response = await RedeemAsync(null);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
  }

  [Test]
  public async Task PostRedeem_AnInvitationForNobodyAndABlankName_AsksForTheName()
  {
    var invitation = await CreateInvitationForNobodyAsync();

    using var response = await RedeemAsync(invitation.QrCodeValue, "   ");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("enrolment.nameMissing"));
                    });
  }

  [Test]
  public async Task PostRedeem_AnInvitationForNobodyAndATypedName_PutsThatWaiterOnTheList()
  {
    var invitation = await CreateInvitationForNobodyAsync();

    using var response = await RedeemAsync(invitation.QrCodeValue, "  Bernd  ");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("deviceKind").GetString(), Is.EqualTo("staffMember"));
                      Assert.That(body.RootElement.GetProperty("staffMember").GetProperty("name").GetString(),
                                  Is.EqualTo("Bernd"));
                      Assert.That(body.RootElement.GetProperty("deviceToken").GetString(), Is.Not.Empty);
                    });
  }

  [Test]
  public async Task PostRedeem_AlreadyConsumedInvitation_AnswersGone()
  {
    var invitation = await CreateInvitationAsync();

    using (var first = await RedeemAsync(invitation.QrCodeValue))
    {
      Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var second = await RedeemAsync(invitation.QrCodeValue);

    Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
  }

  [Test]
  public async Task EnrolmentRoundTrip_AdminInvitesThenReplacesThePhone_RevokesTheFirstDeviceWithTheNewCode()
  {
    var firstInvitation = await CreateInvitationOverHttpAsync(_world.StaffMemberId);

    string firstToken;
    Guid staffMemberId;

    using (var redeemed = await RedeemAsync(firstInvitation.QrCodeValue))
    {
      Assert.That(redeemed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
      var body = JsonDocument.Parse(await redeemed.Content.ReadAsStringAsync());
      firstToken = body.RootElement.GetProperty("deviceToken").GetString()!;
      staffMemberId = body.RootElement.GetProperty("staffMember").GetProperty("id").GetGuid();
    }

    using (var authenticated = await GetSessionAsync(firstToken))
    {
      Assert.That(authenticated.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using (var replayed = await RedeemAsync(firstInvitation.QrCodeValue))
    {
      Assert.That(replayed.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
    }

    var secondInvitation = await CreateInvitationOverHttpAsync(staffMemberId);

    using (var whileTheCodeIsOnScreen = await GetSessionAsync(firstToken))
    {
      Assert.That(whileTheCodeIsOnScreen.StatusCode,
                  Is.EqualTo(HttpStatusCode.Unauthorized),
                  "Asking for a new code hands the phone over, so the old one is signed out at once.");
    }

    using var secondRedemption = await RedeemAsync(secondInvitation.QrCodeValue);
    var secondBody = JsonDocument.Parse(await secondRedemption.Content.ReadAsStringAsync());
    var secondToken = secondBody.RootElement.GetProperty("deviceToken").GetString()!;

    using var secondPhone = await GetSessionAsync(secondToken);
    using var firstPhoneAfterTheScan = await GetSessionAsync(firstToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(secondRedemption.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(secondBody.RootElement.GetProperty("staffMember").GetProperty("id").GetGuid(),
                                  Is.EqualTo(staffMemberId),
                                  "The replacement phone belongs to the same staff member.");
                      Assert.That(secondPhone.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(firstPhoneAfterTheScan.StatusCode,
                                  Is.EqualTo(HttpStatusCode.Unauthorized),
                                  "The replaced phone stays signed out.");
                    });
  }

  [Test]
  public async Task PostRedeem_TheSameBrowserScansACodeForSomebodyElse_SignsTheSetupItHeldOut()
  {
    var firstInvitation = await CreateInvitationAsync();
    string firstToken;

    using (var firstRedemption = await RedeemAsync(firstInvitation.QrCodeValue))
    {
      Assert.That(firstRedemption.StatusCode, Is.EqualTo(HttpStatusCode.OK));
      var firstBody = JsonDocument.Parse(await firstRedemption.Content.ReadAsStringAsync());
      firstToken = firstBody.RootElement.GetProperty("deviceToken").GetString()!;
    }

    var secondInvitation = await CreateInvitationForNobodyAsync();

    using var secondRedemption = await RedeemAsync(secondInvitation.QrCodeValue, "Bernd", firstToken);
    var secondBody = JsonDocument.Parse(await secondRedemption.Content.ReadAsStringAsync());
    var secondToken = secondBody.RootElement.GetProperty("deviceToken").GetString()!;

    using var theSetupItHeld = await GetSessionAsync(firstToken);
    using var theSetupItScanned = await GetSessionAsync(secondToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(secondRedemption.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(theSetupItHeld.StatusCode,
                                  Is.EqualTo(HttpStatusCode.Unauthorized),
                                  "The setup the browser handed over is retired the moment it scans somebody else's code.");
                      Assert.That(theSetupItScanned.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    });
  }

  [Test]
  public async Task PostRedeem_ARefusedCodeWhileHoldingASetup_LeavesThatSetupWorking()
  {
    var invitation = await CreateInvitationAsync();
    string token;

    using (var redemption = await RedeemAsync(invitation.QrCodeValue))
    {
      Assert.That(redemption.StatusCode, Is.EqualTo(HttpStatusCode.OK));
      var body = JsonDocument.Parse(await redemption.Content.ReadAsStringAsync());
      token = body.RootElement.GetProperty("deviceToken").GetString()!;
    }

    using var refused = await RedeemAsync(invitation.QrCodeValue, null, token);
    using var stillSetUp = await GetSessionAsync(token);

    Assert.Multiple(() =>
                    {
                      Assert.That(refused.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
                      Assert.That(stillSetUp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    });
  }

  [TestCase("nodothere")]
  [TestCase("unknownlookup.secret")]
  [TestCase("")]
  public async Task PostRedeem_APreviousTokenThatNamesNoDevice_StillSetsThePhoneUp(string previousDeviceToken)
  {
    var invitation = await CreateInvitationAsync();

    using var response = await RedeemAsync(invitation.QrCodeValue, null, previousDeviceToken);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("deviceToken").GetString(), Is.Not.Empty);
                    });
  }

  private Task<HttpResponseMessage> RedeemAsync(string? code,
                                                string? name = null,
                                                string? previousDeviceToken = null)
  {
    return _factory.Client.PostAsJsonAsync("/api/enrolment/redeem",
                                          new RedeemBody(code, name, "NUnit", previousDeviceToken));
  }

  private Task<HttpResponseMessage> GetSessionAsync(string deviceToken)
  {
    HttpRequestMessage request = new(HttpMethod.Get, "/api/session");
    request.Headers.Authorization = new("Bearer", deviceToken);

    return _factory.Client.SendAsync(request);
  }

  private async Task<EnrolmentInvitationCreated> CreateInvitationAsync()
  {
    using var scope = _factory.Services.CreateScope();

    return await scope.ServiceProvider.GetRequiredService<IEnrolmentInvitationStore>()
                      .CreateAsync(new(DeviceOwnerKind.StaffMember, _world.StaffMemberId), CancellationToken.None);
  }

  private async Task<EnrolmentInvitationCreated> CreateInvitationForNobodyAsync()
  {
    using var scope = _factory.Services.CreateScope();

    return await scope.ServiceProvider.GetRequiredService<IEnrolmentInvitationStore>()
                      .CreateAsync(null, CancellationToken.None);
  }

  private async Task<EnrolmentInvitationCreated> CreateInvitationOverHttpAsync(Guid staffMemberId)
  {
    using var response = await _factory.Client.PostAsJsonAsync("/api/admin/enrolment/invitations",
                                                              new { staffMemberId });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var qrUrl = body.RootElement.GetProperty("qrUrl").GetString()!;

    return new(body.RootElement.GetProperty("invitationId").GetGuid(),
               qrUrl[(qrUrl.LastIndexOf('/') + 1)..],
               body.RootElement.GetProperty("expiresAtUtc").GetDateTime());
  }
}

public sealed record RedeemBody(string? Code, string? Name, string UserAgent, string? PreviousDeviceToken = null);
