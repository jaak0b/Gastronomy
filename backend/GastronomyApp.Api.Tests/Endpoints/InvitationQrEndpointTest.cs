using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class InvitationQrEndpointTest
{
    private const string QrPath = "/api/admin/enrolment/invitations/current/qr.svg";
    private const int PixelsPerModule = 8;

    private OrderTestContext context = null!;

    [SetUp]
    public async Task SetUp()
    {
        context = await new OrderTestContext.Builder().StartAsync(withRunningPrinters: false);
    }

    [TearDown]
    public async Task TearDown()
    {
        await context.DisposeAsync();
    }

    [Test]
    public async Task GetQr_NoOutstandingInvitation_AnswersNotFound()
    {
        using HttpResponseMessage response = await context.Client.GetAsync(QrPath);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetQr_OutstandingInvitation_RendersScalableVectorGraphicsForItsUrl()
    {
        string qrUrl = await CreateInvitationAsync();

        using HttpResponseMessage response = await context.Client.GetAsync(QrPath);
        string svg = await response.Content.ReadAsStringAsync();

        int side = ReadDeclaredSide(svg);
        int smallestSideThatHolds = SmallestModuleCountFor(qrUrl.Length) * PixelsPerModule;

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("image/svg+xml"));
            Assert.That(svg, Does.Contain("<svg"));
            Assert.That(
                side,
                Is.GreaterThanOrEqualTo(smallestSideThatHolds),
                $"A QR carrying {qrUrl.Length} characters cannot be smaller than {smallestSideThatHolds} pixels a side.");
            Assert.That(side % PixelsPerModule, Is.EqualTo(0), "The QR must be drawn in whole modules.");
        });
    }

    [Test]
    public async Task GetQr_SecondInvitation_EncodesTheNewUrlRatherThanTheOld()
    {
        await CreateInvitationAsync();

        string firstSvg;
        using (HttpResponseMessage first = await context.Client.GetAsync(QrPath))
        {
            firstSvg = await first.Content.ReadAsStringAsync();
        }

        await CreateInvitationAsync();

        using HttpResponseMessage second = await context.Client.GetAsync(QrPath);
        string secondSvg = await second.Content.ReadAsStringAsync();

        Assert.That(
            secondSvg,
            Is.Not.EqualTo(firstSvg),
            "The rendered QR must encode the current invitation, not a stale or placeholder image.");
    }

    [Test]
    public async Task GetQr_OutstandingInvitation_IsNeverCached()
    {
        await CreateInvitationAsync();

        using HttpResponseMessage response = await context.Client.GetAsync(QrPath);

        Assert.Multiple(() =>
        {
            Assert.That(response.Headers.CacheControl!.NoStore, Is.True);
            Assert.That(response.Headers.CacheControl.NoCache, Is.True);
        });
    }

    [Test]
    public async Task GetQr_InvitationAlreadyRedeemed_AnswersNotFound()
    {
        string qrUrl = await CreateInvitationAsync();
        string code = qrUrl[(qrUrl.LastIndexOf('/') + 1)..];

        using (HttpResponseMessage redeemed = await context.Client.PostAsJsonAsync(
            "/api/enrolment/redeem",
            new RedeemBody(code, null, "Anna", "NUnit")))
        {
            Assert.That(redeemed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        using HttpResponseMessage response = await context.Client.GetAsync(QrPath);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private async Task<string> CreateInvitationAsync()
    {
        using HttpResponseMessage response = await context.Client.PostAsJsonAsync(
            "/api/admin/enrolment/invitations",
            new { serverPersonId = (Guid?)null });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return body.RootElement.GetProperty("qrUrl").GetString()!;
    }

    private int ReadDeclaredSide(string svg)
    {
        Match width = Regex.Match(svg, @"width=""(\d+)""");

        Assert.That(width.Success, Is.True, "The rendered QR must declare its width.");

        return int.Parse(width.Groups[1].Value);
    }

    private int SmallestModuleCountFor(int payloadCharacters)
    {
        return payloadCharacters <= 32 ? 21 : 25;
    }
}
