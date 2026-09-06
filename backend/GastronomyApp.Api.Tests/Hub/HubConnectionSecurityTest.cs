using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Api.Tests.Endpoints;
using Microsoft.AspNetCore.SignalR.Client;

namespace GastronomyApp.Api.Tests.Hub;

[TestFixture]
public sealed class HubConnectionSecurityTest
{

  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContext.Builder().StartAsync();
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
  }

  private readonly TimeSpan _patience = TimeSpan.FromSeconds(10);

  private OrderTestContext _context = null!;

  [Test]
  public async Task Connect_LoopbackCallerWithoutDeviceToken_JoinsTheAdminGroupAndHearsOrderEvents()
  {
    TaskCompletionSource<Guid> heard = new(TaskCreationOptions.RunContinuationsAsynchronously);

    await using var connection = Connect("hub");
    connection.On<JsonElement>("OrderAccepted",
                               payload => heard.TrySetResult(payload.GetProperty("orderId").GetGuid()));

    await connection.StartAsync();

    var orderId = await PlaceAnOrderAsync();
    var received = await Task.WhenAny(heard.Task, Task.Delay(_patience));

    Assert.Multiple(() =>
                    {
                      Assert.That(received, Is.SameAs(heard.Task), "A loopback caller must join the admin group.");
                      Assert.That(connection.State, Is.EqualTo(HubConnectionState.Connected));
                    });

    Assert.That(await heard.Task, Is.EqualTo(orderId));
  }

  [Test]
  public async Task Deactivate_ConnectedPhone_IsRemovedFromEveryGroupAndClosed()
  {
    TaskCompletionSource<string> heardBeforeRevocation = new(TaskCreationOptions.RunContinuationsAsynchronously);

    await using var connection = Connect($"hub?access_token={_context.DeviceToken}");
    connection.On<JsonElement>("CatalogChanged",
                               payload => heardBeforeRevocation.TrySetResult(payload.GetProperty("version").GetString()!));

    await connection.StartAsync();
    await ChangeTheCatalogAsync();

    var beforeRevocation = await Task.WhenAny(heardBeforeRevocation.Task, Task.Delay(_patience));

    Assert.That(beforeRevocation,
                Is.SameAs(heardBeforeRevocation.Task),
                "The phone must receive live pushes before it is revoked.");

    using (var revocation = await _context.Client.PostAsync($"/api/admin/staff-members/{_context.World.StaffMemberId}/deactivate",
                                                           null))
    {
      Assert.That(revocation.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    var closed = await WaitUntilAsync(() => connection.State != HubConnectionState.Connected);

    Assert.That(closed, Is.True, "Revoking a device must abort its live connection.");

    using var afterRevocation = await _context.SendAsync(HttpMethod.Get, "/api/session");

    Assert.That(afterRevocation.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
  }

  private async Task ChangeTheCatalogAsync()
  {
    using var response = await _context.Client.PostAsJsonAsync($"/api/admin/items/{_context.World.BratwurstItemId}/availability",
                                                               new { isAvailable = false });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
  }

  private async Task<Guid> PlaceAnOrderAsync()
  {
    using var response = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid()));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.GetProperty("orderId").GetGuid();
  }

  private async Task<bool> WaitUntilAsync(Func<bool> condition)
  {
    var deadline = DateTime.UtcNow.Add(_patience);

    while (DateTime.UtcNow < deadline)
    {
      if (condition())
      {
        return true;
      }

      await Task.Delay(100);
    }

    return condition();
  }

  private HubConnection Connect(string relativeUrl)
  {
    return new HubConnectionBuilder()
          .WithUrl(new Uri(_context.Factory.BaseAddress, relativeUrl))
          .Build();
  }
}

