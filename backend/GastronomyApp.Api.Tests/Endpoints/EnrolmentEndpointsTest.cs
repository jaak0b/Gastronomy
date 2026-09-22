using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Api.Tests.TestSupport;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class EnrolmentEndpointsTest
{
  [SetUp]
  public async Task SetUp()
  {
    _factory = await new ApiTestFactoryBuilder().StartAsync();
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
    var code = await CreateInvitationCodeAsync();

    using var response = await RedeemAsync(code);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("deviceToken").GetString(), Is.Not.Empty);
                      Assert.That(body.RootElement.GetProperty("deviceId").GetGuid(), Is.Not.EqualTo(Guid.Empty));
                      Assert.That(body.RootElement.GetProperty("staffMember").GetProperty("name").GetString(), Is.EqualTo("Anna"));
                      Assert.That(body.RootElement.GetProperty("language").GetString(), Is.Not.Empty);
                    });
  }

  [Test]
  public async Task PostRedeem_TheSameCodeScannedTwiceAtTheSameMoment_SetsUpExactlyOneDevice()
  {
    var code = await CreateInvitationCodeAsync();

    Task<HttpResponseMessage> first = RedeemAsync(code);
    Task<HttpResponseMessage> second = RedeemAsync(code);

    HttpResponseMessage[] responses = await Task.WhenAll(first, second);
    var acceptedResponses = responses.Count(response => response.StatusCode == HttpStatusCode.OK);

    foreach (var response in responses)
      response.Dispose();

    await using var database = _factory.CreateContext();
    var deviceCount = await database.Devices.CountAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(acceptedResponses, Is.EqualTo(1));
                      Assert.That(deviceCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task PostRedeem_NeitherCodeForm_IsRefused()
  {
    await CreateInvitationCodeAsync();

    using var response = await RedeemAsync(null);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
  }

  [Test]
  public async Task PostRedeem_AnInvitationForNobodyAndABlankName_AsksForTheName()
  {
    var code = await CreateInvitationCodeForNobodyAsync();

    using var response = await RedeemAsync(code, "   ");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("enrolment.nameMissing"));
                    });
  }

  [Test]
  public async Task PostRedeem_AnInvitationForNobodyAndATypedName_PutsThatWaiterOnTheList()
  {
    var code = await CreateInvitationCodeForNobodyAsync();

    using var response = await RedeemAsync(code, "  Bernd  ");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("deviceKind").GetString(), Is.EqualTo("staffMember"));
                      Assert.That(body.RootElement.GetProperty("staffMember").GetProperty("name").GetString(), Is.EqualTo("Bernd"));
                      Assert.That(body.RootElement.GetProperty("deviceToken").GetString(), Is.Not.Empty);
                    });
  }

  [Test]
  public async Task PostRedeem_AlreadyConsumedInvitation_AnswersGone()
  {
    var code = await CreateInvitationCodeAsync();

    using (var first = await RedeemAsync(code))
    {
      Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var second = await RedeemAsync(code);

    Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
  }

  [Test]
  public async Task EnrolmentRoundTrip_AdminInvitesThenReplacesThePhone_RevokesTheFirstDeviceWithTheNewCode()
  {
    var firstCode = await CreateInvitationCodeOverHttpAsync(_world.StaffMemberId);

    string firstToken;
    Guid staffMemberId;

    using (var redeemed = await RedeemAsync(firstCode))
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

    using (var replayed = await RedeemAsync(firstCode))
    {
      Assert.That(replayed.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
    }

    var secondCode = await CreateInvitationCodeOverHttpAsync(staffMemberId);

    using (var whileTheCodeIsOnScreen = await GetSessionAsync(firstToken))
    {
      Assert.That(whileTheCodeIsOnScreen.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), "Asking for a new code hands the phone over, so the old one is signed out at once.");
    }

    using var secondRedemption = await RedeemAsync(secondCode);
    var secondBody = JsonDocument.Parse(await secondRedemption.Content.ReadAsStringAsync());
    var secondToken = secondBody.RootElement.GetProperty("deviceToken").GetString()!;

    using var secondPhone = await GetSessionAsync(secondToken);
    using var firstPhoneAfterTheScan = await GetSessionAsync(firstToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(secondRedemption.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(secondBody.RootElement.GetProperty("staffMember").GetProperty("id").GetGuid(), Is.EqualTo(staffMemberId), "The replacement phone belongs to the same staff member.");
                      Assert.That(secondPhone.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(firstPhoneAfterTheScan.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), "The replaced phone stays signed out.");
                    });
  }

  [Test]
  public async Task PostRedeem_TheSameBrowserScansACodeForSomebodyElse_SignsTheSetupItHeldOut()
  {
    var firstCode = await CreateInvitationCodeAsync();
    string firstToken;

    using (var firstRedemption = await RedeemAsync(firstCode))
    {
      Assert.That(firstRedemption.StatusCode, Is.EqualTo(HttpStatusCode.OK));
      var firstBody = JsonDocument.Parse(await firstRedemption.Content.ReadAsStringAsync());
      firstToken = firstBody.RootElement.GetProperty("deviceToken").GetString()!;
    }

    var secondCode = await CreateInvitationCodeForNobodyAsync();

    using var secondRedemption = await RedeemAsync(secondCode, "Bernd", firstToken);
    var secondBody = JsonDocument.Parse(await secondRedemption.Content.ReadAsStringAsync());
    var secondToken = secondBody.RootElement.GetProperty("deviceToken").GetString()!;

    using var theSetupItHeld = await GetSessionAsync(firstToken);
    using var theSetupItScanned = await GetSessionAsync(secondToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(secondRedemption.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(theSetupItHeld.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), "The setup the browser handed over is retired the moment it scans somebody else's code.");
                      Assert.That(theSetupItScanned.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    });
  }

  [Test]
  public async Task PostRedeem_ARefusedCodeWhileHoldingASetup_LeavesThatSetupWorking()
  {
    var code = await CreateInvitationCodeAsync();
    string token;

    using (var redemption = await RedeemAsync(code))
    {
      Assert.That(redemption.StatusCode, Is.EqualTo(HttpStatusCode.OK));
      var body = JsonDocument.Parse(await redemption.Content.ReadAsStringAsync());
      token = body.RootElement.GetProperty("deviceToken").GetString()!;
    }

    using var refused = await RedeemAsync(code, null, token);
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
    var code = await CreateInvitationCodeAsync();

    using var response = await RedeemAsync(code, null, previousDeviceToken);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("deviceToken").GetString(), Is.Not.Empty);
                    });
  }

  [TestCase("de-AT", "de")]
  [TestCase("de", "de")]
  [TestCase("en-US", "en")]
  [TestCase("fr-FR", "en")]
  [TestCase("", "en")]
  public async Task PostRedeem_TheBrowserLanguage_BecomesTheDeviceLanguage(string acceptLanguage, string expectedLanguage)
  {
    var code = await CreateInvitationCodeAsync();

    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/enrolment/redeem") { Content = JsonContent.Create(new RedeemBody(code, null, "NUnit")) };
    request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);

    using var response = await _factory.Client.SendAsync(request);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("language").GetString(), Is.EqualTo(expectedLanguage));
                    });
  }

  private Task<HttpResponseMessage> RedeemAsync(string? code, string? name = null, string? previousDeviceToken = null)
  {
    return _factory.Client.PostAsJsonAsync("/api/enrolment/redeem", new RedeemBody(code, name, "NUnit", previousDeviceToken));
  }

  private Task<HttpResponseMessage> GetSessionAsync(string deviceToken)
  {
    HttpRequestMessage request = new(HttpMethod.Get, "/api/session");
    request.Headers.Authorization = new("Bearer", deviceToken);

    return _factory.Client.SendAsync(request);
  }

  private async Task<string> CreateInvitationCodeAsync()
  {
    using var scope = _factory.Services.CreateScope();
    var owner = await scope.ServiceProvider.GetRequiredService<IDeviceOwnerStore>().FindAsync(DeviceOwnerKind.StaffMember, _world.StaffMemberId, CancellationToken.None);

    return (await scope.ServiceProvider.GetRequiredService<IEnrolmentInvitationStore>().CreateAsync(owner, CancellationToken.None)).QRCodeValue;
  }

  private async Task<string> CreateInvitationCodeForNobodyAsync()
  {
    using var scope = _factory.Services.CreateScope();

    return (await scope.ServiceProvider.GetRequiredService<IEnrolmentInvitationStore>().CreateAsync(null, CancellationToken.None)).QRCodeValue;
  }

  private async Task<string> CreateInvitationCodeOverHttpAsync(Guid staffMemberId)
  {
    using var response = await _factory.Client.PostAsJsonAsync("/api/admin/enrolment/invitations", new { staffMemberId });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var qrUrl = body.RootElement.GetProperty("qrUrl").GetString()!;

    return qrUrl[(qrUrl.LastIndexOf('/') + 1)..];
  }
}
