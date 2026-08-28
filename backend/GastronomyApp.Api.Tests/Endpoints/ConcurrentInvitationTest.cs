using System.Net;
using System.Net.Http.Json;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class ConcurrentInvitationTest
{
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
  public async Task PostInvitation_TwoAtTheSameMoment_NeverCrashesAndLeavesOneOutstanding()
  {
    Task<HttpResponseMessage> first = CreateInvitationAsync();
    Task<HttpResponseMessage> second = CreateInvitationAsync();

    HttpResponseMessage[] responses = await Task.WhenAll(first, second);
    IReadOnlyList<HttpStatusCode> statuses = [.. responses.Select(response => response.StatusCode)];

    foreach (HttpResponseMessage response in responses)
    {
      response.Dispose();
    }

    await using GastronomyAppDbContext database = context.Factory.CreateContext();
    int stillOutstanding = await database.EnrolmentInvitations
        .CountAsync(invitation => invitation.ConsumedAtUtc == null);

    Assert.Multiple(() =>
    {
      Assert.That(
              statuses,
              Has.None.EqualTo(HttpStatusCode.InternalServerError),
              "A unique collision must never reach the caller as a crash.");
      Assert.That(statuses, Has.Some.EqualTo(HttpStatusCode.Created));
      Assert.That(stillOutstanding, Is.EqualTo(1), "Exactly one invitation may be outstanding.");
    });
  }

  [Test]
  public async Task PostInvitation_RepeatedlyOverTheSameOutstandingRow_KeepsSucceeding()
  {
    for (int attempt = 0; attempt < 5; attempt++)
    {
      using HttpResponseMessage response = await CreateInvitationAsync();

      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    await using GastronomyAppDbContext database = context.Factory.CreateContext();
    List<EnrolmentInvitation> invitations = await database.EnrolmentInvitations.ToListAsync();

    Assert.Multiple(() =>
    {
      Assert.That(invitations, Has.Count.EqualTo(5));
      Assert.That(invitations.Count(invitation => invitation.ConsumedAtUtc == null), Is.EqualTo(1));
    });
  }

  private Task<HttpResponseMessage> CreateInvitationAsync()
  {
    return context.Client.PostAsJsonAsync(
        "/api/admin/enrolment/invitations",
        new { staffMemberId = (Guid?)null });
  }
}
