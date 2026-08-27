using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Infrastructure;
using GastronomyApp.Infrastructure.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class EnrolmentEndpointsTest
{
    private ApiTestFactory factory = null!;

    [SetUp]
    public async Task SetUp()
    {
        factory = await new ApiTestFactory.Builder().StartAsync();
        await using GastronomyAppDbContext context = factory.CreateContext();
        await new ApiSeeder().SeedAsync(context, CancellationToken.None);
    }

    [TearDown]
    public async Task TearDown()
    {
        await factory.DisposeAsync();
    }

    [Test]
    public async Task PostRedeem_QrCodeForm_ReturnsTheDeviceTokenOnce()
    {
        EnrolmentInvitationCreated invitation = await CreateInvitationAsync();

        using HttpResponseMessage response = await RedeemAsync(invitation.QrCodeValue, null, "Anna");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body.RootElement.GetProperty("deviceToken").GetString(), Is.Not.Empty);
            Assert.That(body.RootElement.GetProperty("deviceId").GetGuid(), Is.Not.EqualTo(Guid.Empty));
            Assert.That(
                body.RootElement.GetProperty("serverPerson").GetProperty("name").GetString(),
                Is.EqualTo("Anna"));
            Assert.That(body.RootElement.GetProperty("language").GetString(), Is.Not.Empty);
        });
    }

    [Test]
    public async Task PostRedeem_SixDigitForm_IsAccepted()
    {
        EnrolmentInvitationCreated invitation = await CreateInvitationAsync();

        using HttpResponseMessage response = await RedeemAsync(null, invitation.SixDigitCode, "Bea");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task PostRedeem_NeitherCodeForm_IsRefused()
    {
        await CreateInvitationAsync();

        using HttpResponseMessage response = await RedeemAsync(null, null, "Anna");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PostRedeem_BothCodeForms_IsRefused()
    {
        EnrolmentInvitationCreated invitation = await CreateInvitationAsync();

        using HttpResponseMessage response = await RedeemAsync(
            invitation.QrCodeValue,
            invitation.SixDigitCode,
            "Anna");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PostRedeem_EmptyName_IsRefused()
    {
        EnrolmentInvitationCreated invitation = await CreateInvitationAsync();

        using HttpResponseMessage response = await RedeemAsync(invitation.QrCodeValue, null, "   ");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PostRedeem_WrongSixDigits_AnswersNotFound()
    {
        await CreateInvitationAsync();

        using HttpResponseMessage response = await RedeemAsync(null, "000000", "Anna");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task PostRedeem_AlreadyConsumedInvitation_AnswersGone()
    {
        EnrolmentInvitationCreated invitation = await CreateInvitationAsync();

        using (HttpResponseMessage first = await RedeemAsync(invitation.QrCodeValue, null, "Anna"))
        {
            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        using HttpResponseMessage second = await RedeemAsync(invitation.QrCodeValue, null, "Anna");

        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
    }

    [Test]
    public async Task PostRedeem_TenWrongSixDigitAttempts_RetiresTheDigits()
    {
        await CreateInvitationAsync();

        for (int attempt = 0; attempt < 10; attempt++)
        {
            using HttpResponseMessage wrong = await RedeemAsync(null, "000000", "Anna");

            Assert.That(wrong.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        using HttpResponseMessage retired = await RedeemAsync(null, "000000", "Anna");

        Assert.That(retired.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
    }

    [Test]
    public async Task EnrolmentRoundTrip_AdminInvitesThenReplacesThePhone_RevokesTheFirstDevice()
    {
        EnrolmentInvitationCreated firstInvitation = await CreateInvitationOverHttpAsync(null);

        string firstToken;
        Guid serverPersonId;

        using (HttpResponseMessage redeemed = await RedeemAsync(firstInvitation.QrCodeValue, null, "Anna"))
        {
            Assert.That(redeemed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            JsonDocument body = JsonDocument.Parse(await redeemed.Content.ReadAsStringAsync());
            firstToken = body.RootElement.GetProperty("deviceToken").GetString()!;
            serverPersonId = body.RootElement.GetProperty("serverPerson").GetProperty("id").GetGuid();
        }

        using (HttpResponseMessage authenticated = await GetSessionAsync(firstToken))
        {
            Assert.That(authenticated.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        using (HttpResponseMessage replayed = await RedeemAsync(firstInvitation.QrCodeValue, null, "Anna"))
        {
            Assert.That(replayed.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
        }

        EnrolmentInvitationCreated secondInvitation = await CreateInvitationOverHttpAsync(serverPersonId);

        using (HttpResponseMessage afterReplacement = await GetSessionAsync(firstToken))
        {
            Assert.That(
                afterReplacement.StatusCode,
                Is.EqualTo(HttpStatusCode.Unauthorized),
                "Issuing a new invitation must revoke the phone it replaces.");
        }

        using HttpResponseMessage secondRedemption = await RedeemAsync(secondInvitation.QrCodeValue, null, "Anna");
        JsonDocument secondBody = JsonDocument.Parse(await secondRedemption.Content.ReadAsStringAsync());
        string secondToken = secondBody.RootElement.GetProperty("deviceToken").GetString()!;

        using HttpResponseMessage secondPhone = await GetSessionAsync(secondToken);

        Assert.Multiple(() =>
        {
            Assert.That(secondRedemption.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(
                secondBody.RootElement.GetProperty("serverPerson").GetProperty("id").GetGuid(),
                Is.EqualTo(serverPersonId),
                "The replacement phone belongs to the same person.");
            Assert.That(secondPhone.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    private Task<HttpResponseMessage> RedeemAsync(string? code, string? sixDigitCode, string name)
    {
        return factory.Client.PostAsJsonAsync(
            "/api/enrolment/redeem",
            new RedeemBody(code, sixDigitCode, name, "NUnit"));
    }

    private Task<HttpResponseMessage> GetSessionAsync(string deviceToken)
    {
        HttpRequestMessage request = new(HttpMethod.Get, "/api/session");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", deviceToken);

        return factory.Client.SendAsync(request);
    }

    private async Task<EnrolmentInvitationCreated> CreateInvitationAsync()
    {
        using IServiceScope scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IEnrolmentInvitationStore>()
            .CreateAsync(null, CancellationToken.None);
    }

    private async Task<EnrolmentInvitationCreated> CreateInvitationOverHttpAsync(Guid? serverPersonId)
    {
        using HttpResponseMessage response = await factory.Client.PostAsJsonAsync(
            "/api/admin/enrolment/invitations",
            new { serverPersonId });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string qrUrl = body.RootElement.GetProperty("qrUrl").GetString()!;

        return new EnrolmentInvitationCreated(
            body.RootElement.GetProperty("invitationId").GetGuid(),
            qrUrl[(qrUrl.LastIndexOf('/') + 1)..],
            body.RootElement.GetProperty("sixDigitCode").GetString()!,
            body.RootElement.GetProperty("expiresAtUtc").GetDateTime());
    }
}

public sealed record RedeemBody(string? Code, string? SixDigitCode, string Name, string UserAgent);
