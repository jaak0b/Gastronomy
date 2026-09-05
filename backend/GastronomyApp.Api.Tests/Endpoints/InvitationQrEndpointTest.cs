using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class InvitationQrEndpointTest
{

  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContext.Builder().StartAsync(false);
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
  }

  private const int PixelsPerModule = 8;

  private OrderTestContext _context = null!;

  private sealed record CreatedInvitation(Guid InvitationId, string QrUrl);

  private string QrPathFor(Guid invitationId)
  {
    return $"/api/admin/enrolment/invitations/{invitationId}/qr.svg";
  }

  [Test]
  public async Task GetQr_AnInvitationThatNeverExisted_AnswersNotFoundWithWordingForTheAdmin()
  {
    using var response = await _context.Client.GetAsync(QrPathFor(Guid.NewGuid()));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.enrol.qrUnavailable"));
                    });
  }

  [Test]
  public async Task GetQr_OutstandingInvitation_RendersScalableVectorGraphicsForItsUrl()
  {
    var invitation = await CreateInvitationAsync();

    using var response = await _context.Client.GetAsync(QrPathFor(invitation.InvitationId));
    var svg = await response.Content.ReadAsStringAsync();

    var side = ReadDeclaredSide(svg);
    var smallestSideThatHolds = SmallestModuleCountFor(invitation.QrUrl.Length) * PixelsPerModule;

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("image/svg+xml"));
                      Assert.That(svg, Does.Contain("<svg"));
                      Assert.That(side,
                                  Is.GreaterThanOrEqualTo(smallestSideThatHolds),
                                  $"A QR carrying {invitation.QrUrl.Length} characters cannot be smaller than {smallestSideThatHolds} pixels a side.");
                      Assert.That(side % PixelsPerModule, Is.EqualTo(0), "The QR must be drawn in whole modules.");
                    });
  }

  [Test]
  public async Task GetQr_SecondInvitation_HasAnAddressOfItsOwnAndEncodesTheNewUrl()
  {
    var first = await CreateInvitationAsync();

    string firstSvg;
    using (var firstResponse = await _context.Client.GetAsync(QrPathFor(first.InvitationId)))
    {
      firstSvg = await firstResponse.Content.ReadAsStringAsync();
    }

    var second = await CreateInvitationAsync();

    using var secondResponse = await _context.Client.GetAsync(QrPathFor(second.InvitationId));
    var secondSvg = await secondResponse.Content.ReadAsStringAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(second.InvitationId,
                                  Is.Not.EqualTo(first.InvitationId),
                                  "Each invitation must be addressable on its own, so the picture and the printed URL cannot drift apart.");
                      Assert.That(secondSvg,
                                  Is.Not.EqualTo(firstSvg),
                                  "The rendered QR must encode the invitation it was asked for.");
                    });
  }

  [Test]
  public async Task GetQr_TheInvitationReplacedByANewerOne_SaysSoRatherThanRenderingTheNewOne()
  {
    var first = await CreateInvitationAsync();
    await CreateInvitationAsync();

    using var response = await _context.Client.GetAsync(QrPathFor(first.InvitationId));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.enrol.qrReplaced"));
                    });
  }

  [Test]
  public async Task GetQr_OutstandingInvitation_IsNeverCached()
  {
    var invitation = await CreateInvitationAsync();

    using var response = await _context.Client.GetAsync(QrPathFor(invitation.InvitationId));

    Assert.Multiple(() =>
                    {
                      Assert.That(response.Headers.CacheControl!.NoStore, Is.True);
                      Assert.That(response.Headers.CacheControl.NoCache, Is.True);
                    });
  }

  [Test]
  public async Task GetQr_InvitationAlreadyRedeemed_SaysAPhoneHasUsedIt()
  {
    var invitation = await CreateInvitationAsync();
    var code = invitation.QrUrl[(invitation.QrUrl.LastIndexOf('/') + 1)..];

    using (var redeemed = await _context.Client.PostAsJsonAsync("/api/enrolment/redeem",
                                                               new RedeemBody(code, "Anna", "NUnit")))
    {
      Assert.That(redeemed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var response = await _context.Client.GetAsync(QrPathFor(invitation.InvitationId));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.enrol.qrAlreadyUsed"));
                    });
  }

  [Test]
  public async Task GetQr_InvitationPastItsFiveMinutes_SaysItExpiredRatherThanRenderingABrokenPicture()
  {
    var invitation = await CreateInvitationAsync();

    await using (var database = _context.Factory.CreateContext())
    {
      var row = await database.EnrolmentInvitations
                              .SingleAsync(candidate => candidate.Id == invitation.InvitationId);
      row.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
      await database.SaveChangesAsync();
    }

    using var response = await _context.Client.GetAsync(QrPathFor(invitation.InvitationId));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.enrol.expired"));
                    });
  }

  private async Task<CreatedInvitation> CreateInvitationAsync()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/enrolment/invitations",
                                                              new { staffMemberId = (Guid?)null });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return new(body.RootElement.GetProperty("invitationId").GetGuid(),
               body.RootElement.GetProperty("qrUrl").GetString()!);
  }

  private int ReadDeclaredSide(string svg)
  {
    var width = Regex.Match(svg, @"width=""(\d+)""");

    Assert.That(width.Success, Is.True, "The rendered QR must declare its width.");

    return int.Parse(width.Groups[1].Value);
  }

  private int SmallestModuleCountFor(int payloadCharacters)
  {
    return payloadCharacters <= 32 ? 21 : 25;
  }
}
