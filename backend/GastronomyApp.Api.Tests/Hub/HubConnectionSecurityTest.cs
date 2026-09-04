using System.Net;
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
    _context = await new OrderTestContext.Builder().StartAsync(false);
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
  }

  private readonly TimeSpan _patience = TimeSpan.FromSeconds(10);
  private readonly TimeSpan _silenceWindow = TimeSpan.FromSeconds(2);

  private OrderTestContext _context = null!;

  [Test]
  public async Task Connect_ValidStationAccessKey_JoinsTheSiteWideStationGroup()
  {
    TaskCompletionSource<Guid> heard = new(TaskCreationOptions.RunContinuationsAsynchronously);
    var accessKey = _context.World.KitchenStationId.ToString("N");

    await using var connection = Connect($"hub?stationAccessKey={accessKey}");
    connection.On<JsonElement>("OrderAccepted",
                               payload => heard.TrySetResult(payload.GetProperty("orderId").GetGuid()));

    await connection.StartAsync();

    var orderId = await PlaceAnOrderAsync();
    var received = await Task.WhenAny(heard.Task, Task.Delay(_patience));

    Assert.Multiple(() =>
                    {
                      Assert.That(received, Is.SameAs(heard.Task), "A valid station key must join the stations group.");
                      Assert.That(connection.State, Is.EqualTo(HubConnectionState.Connected));
                    });

    Assert.That(await heard.Task, Is.EqualTo(orderId));
  }

  [Test]
  public async Task Deactivate_ConnectedPhone_IsRemovedFromEveryGroupAndClosed()
  {
    TaskCompletionSource<Guid> heardBeforeRevocation = new(TaskCreationOptions.RunContinuationsAsynchronously);
    TaskCompletionSource<Guid> heardAfterRevocation = new(TaskCreationOptions.RunContinuationsAsynchronously);
    var revoked = false;

    await using var connection = Connect($"hub?access_token={_context.DeviceToken}");
    connection.On<JsonElement>("OrderAccepted",
                               payload =>
                               {
                                 var orderId = payload.GetProperty("orderId").GetGuid();

                                 if (revoked)
                                 {
                                   heardAfterRevocation.TrySetResult(orderId);
                                   return;
                                 }

                                 heardBeforeRevocation.TrySetResult(orderId);
                               });

    await connection.StartAsync();
    await PlaceAnOrderAsync();

    var beforeRevocation = await Task.WhenAny(heardBeforeRevocation.Task, Task.Delay(_patience));

    Assert.That(beforeRevocation,
                Is.SameAs(heardBeforeRevocation.Task),
                "The phone must receive its own order events before it is revoked.");

    revoked = true;

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

  private async Task<Guid> PlaceAnOrderAsync()
  {
    using var response = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid()));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.GetProperty("orderId").GetGuid();
  }

  private async Task StartIgnoringRefusalAsync(HubConnection connection)
  {
    try
    {
      await connection.StartAsync();
    }
    catch (Exception exception) when (exception is not AssertionException)
    {
      TestContext.Out.WriteLine($"The refused connection reported: {exception.Message}");
    }
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
