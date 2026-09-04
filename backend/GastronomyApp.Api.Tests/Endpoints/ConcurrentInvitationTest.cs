using System.Net;
using System.Net.Http.Json;
using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class ConcurrentInvitationTest
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

  private OrderTestContext _context = null!;

  [Test]
  public async Task PostInvitation_TwoAtTheSameMoment_NeverCrashesAndLeavesOneOutstanding()
  {
    Task<HttpResponseMessage> first = CreateInvitationAsync();
    Task<HttpResponseMessage> second = CreateInvitationAsync();

    HttpResponseMessage[] responses = await Task.WhenAll(first, second);
    IReadOnlyList<HttpStatusCode> statuses = [.. responses.Select(response => response.StatusCode)];

    foreach (var response in responses)
    {
      response.Dispose();
    }

    await using var database = _context.Factory.CreateContext();
    var stillOutstanding = await database.EnrolmentInvitations
                                         .CountAsync(invitation => invitation.ConsumedAtUtc == null);

    Assert.Multiple(() =>
                    {
                      Assert.That(statuses,
                                  Has.None.EqualTo(HttpStatusCode.InternalServerError),
                                  "A unique collision must never reach the caller as a crash.");
                      Assert.That(statuses, Has.Some.EqualTo(HttpStatusCode.Created));
                      Assert.That(stillOutstanding, Is.EqualTo(1), "Exactly one invitation may be outstanding.");
                    });
  }

  [Test]
  public async Task PostInvitation_RepeatedlyOverTheSameOutstandingRow_KeepsSucceeding()
  {
    for (var attempt = 0; attempt < 5; attempt++)
    {
      using var response = await CreateInvitationAsync();

      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    await using var database = _context.Factory.CreateContext();
    List<EnrolmentInvitation> invitations = await database.EnrolmentInvitations.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(invitations, Has.Count.EqualTo(5));
                      Assert.That(invitations.Count(invitation => invitation.ConsumedAtUtc == null), Is.EqualTo(1));
                    });
  }

  private Task<HttpResponseMessage> CreateInvitationAsync()
  {
    return _context.Client.PostAsJsonAsync("/api/admin/enrolment/invitations",
                                          new { staffMemberId = (Guid?)null });
  }
}
