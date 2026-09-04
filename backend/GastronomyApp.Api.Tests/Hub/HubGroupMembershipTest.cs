using System.Net;
using System.Text.Json;
using GastronomyApp.Api.Tests.Endpoints;
using GastronomyApp.Infrastructure.Ports;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Hub;

[TestFixture]
public sealed class HubGroupMembershipTest
{

  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContext.Builder().StartAsync(false);

    var otherStaffMemberId = Guid.NewGuid();

    await using (var database = _context.Factory.CreateContext())
    {
      database.StaffMembers.Add(new()
                                {
                                  Id = otherStaffMemberId,
                                  Name = "Bea",
                                  IsActive = true,
                                  CreatedAtUtc = new(2026, 8, 26, 19, 40, 0, DateTimeKind.Utc)
                                });

      await database.SaveChangesAsync();
    }

    using var scope = _context.Factory.Services.CreateScope();
    var issued = await scope.ServiceProvider.GetRequiredService<IDeviceTokenStore>()
                            .IssueAsync(otherStaffMemberId, "de", "NUnit", CancellationToken.None);
    _otherStaffMemberToken = issued.PlaintextToken;
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
  }

  private readonly TimeSpan _patience = TimeSpan.FromSeconds(10);
  private readonly TimeSpan _silenceWindow = TimeSpan.FromSeconds(2);

  private OrderTestContext _context = null!;
  private string _otherStaffMemberToken = null!;

  [Test]
  public async Task OrderAccepted_TwoStaffMembersConnected_ReachesOnlyThePlacingStaffMembersGroup()
  {
    TaskCompletionSource<Guid> placingStaffMemberHeard = new(TaskCreationOptions.RunContinuationsAsynchronously);
    TaskCompletionSource<Guid> otherStaffMemberHeard = new(TaskCreationOptions.RunContinuationsAsynchronously);

    await using var placingStaffMember = Connect(_context.DeviceToken);
    await using var otherStaffMember = Connect(_otherStaffMemberToken);

    placingStaffMember.On<JsonElement>("OrderAccepted",
                                       payload => placingStaffMemberHeard.TrySetResult(payload.GetProperty("orderId").GetGuid()));

    otherStaffMember.On<JsonElement>("OrderAccepted",
                                     payload => otherStaffMemberHeard.TrySetResult(payload.GetProperty("orderId").GetGuid()));

    await placingStaffMember.StartAsync();
    await otherStaffMember.StartAsync();

    Guid orderId;

    using (var response = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid())))
    {
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
      var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
      orderId = body.RootElement.GetProperty("orderId").GetGuid();
    }

    var heard = await Task.WhenAny(placingStaffMemberHeard.Task, Task.Delay(_patience));

    Assert.That(heard, Is.SameAs(placingStaffMemberHeard.Task), "The staff member who placed the order never heard OrderAccepted.");
    Assert.That(await placingStaffMemberHeard.Task, Is.EqualTo(orderId));

    var silence = await Task.WhenAny(otherStaffMemberHeard.Task, Task.Delay(_silenceWindow));

    Assert.That(silence,
                Is.Not.SameAs(otherStaffMemberHeard.Task),
                "A second staff member received an order event that was not theirs.");
  }

  [Test]
  public async Task Connect_ValidDeviceTokenAsAccessTokenQuery_IsAccepted()
  {
    await using var connection = Connect(_context.DeviceToken);

    await connection.StartAsync();

    Assert.That(connection.State, Is.EqualTo(HubConnectionState.Connected));
  }

  private HubConnection Connect(string deviceToken)
  {
    return new HubConnectionBuilder()
          .WithUrl(new Uri(_context.Factory.BaseAddress, $"hub?access_token={deviceToken}"))
          .Build();
  }
}
