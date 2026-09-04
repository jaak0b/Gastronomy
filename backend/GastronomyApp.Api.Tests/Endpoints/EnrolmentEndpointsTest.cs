using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    await new ApiSeeder().SeedAsync(context, CancellationToken.None);
  }

  [TearDown]
  public async Task TearDown()
  {
    await _factory.DisposeAsync();
  }

  private ApiTestFactory _factory = null!;

  [Test]
  public async Task PostRedeem_QrCodeForm_ReturnsTheDeviceTokenOnce()
  {
    var invitation = await CreateInvitationAsync();

    using var response = await RedeemAsync(invitation.QrCodeValue, "Anna");
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

    using var response = await RedeemAsync(null, "Anna");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
  }

  [Test]
  public async Task PostRedeem_EmptyName_IsRefused()
  {
    var invitation = await CreateInvitationAsync();

    using var response = await RedeemAsync(invitation.QrCodeValue, "   ");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
  }

  [Test]
  public async Task PostRedeem_AlreadyConsumedInvitation_AnswersGone()
  {
    var invitation = await CreateInvitationAsync();

    using (var first = await RedeemAsync(invitation.QrCodeValue, "Anna"))
    {
      Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var second = await RedeemAsync(invitation.QrCodeValue, "Anna");

    Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
  }

  [Test]
  public async Task EnrolmentRoundTrip_AdminInvitesThenReplacesThePhone_RevokesTheFirstDevice()
  {
    var firstInvitation = await CreateInvitationOverHttpAsync(null);

    string firstToken;
    Guid staffMemberId;

    using (var redeemed = await RedeemAsync(firstInvitation.QrCodeValue, "Anna"))
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

    using (var replayed = await RedeemAsync(firstInvitation.QrCodeValue, "Anna"))
    {
      Assert.That(replayed.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
    }

    var secondInvitation = await CreateInvitationOverHttpAsync(staffMemberId);

    using (var afterReplacement = await GetSessionAsync(firstToken))
    {
      Assert.That(afterReplacement.StatusCode,
                  Is.EqualTo(HttpStatusCode.Unauthorized),
                  "Issuing a new invitation must revoke the phone it replaces.");
    }

    using var secondRedemption = await RedeemAsync(secondInvitation.QrCodeValue, "Anna");
    var secondBody = JsonDocument.Parse(await secondRedemption.Content.ReadAsStringAsync());
    var secondToken = secondBody.RootElement.GetProperty("deviceToken").GetString()!;

    using var secondPhone = await GetSessionAsync(secondToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(secondRedemption.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(secondBody.RootElement.GetProperty("staffMember").GetProperty("id").GetGuid(),
                                  Is.EqualTo(staffMemberId),
                                  "The replacement phone belongs to the same staff member.");
                      Assert.That(secondPhone.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    });
  }

  private Task<HttpResponseMessage> RedeemAsync(string? code, string? name)
  {
    return _factory.Client.PostAsJsonAsync("/api/enrolment/redeem",
                                          new RedeemBody(code, name, "NUnit"));
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
                      .CreateAsync(null, CancellationToken.None);
  }

  private async Task<EnrolmentInvitationCreated> CreateInvitationOverHttpAsync(Guid? staffMemberId)
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

public sealed record RedeemBody(string? Code, string? Name, string UserAgent);
