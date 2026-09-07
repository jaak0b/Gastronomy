using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Api.Tests.Endpoints;
using Microsoft.AspNetCore.SignalR.Client;

namespace GastronomyApp.Api.Tests.Hub;

[TestFixture]
public sealed class StationChangeAnnouncementTest
{
  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContext.Builder().StartAsync();
    _kitchenToken = await _context.IssueStationTokenAsync(_context.World.KitchenStationId);
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
  }

  private readonly TimeSpan _patience = TimeSpan.FromSeconds(10);

  private OrderTestContext _context = null!;
  private string _kitchenToken = null!;

  [Test]
  public async Task Rename_AStationTheWaiterCanOrderFrom_TellsThePhones()
  {
    TaskCompletionSource heard = new(TaskCreationOptions.RunContinuationsAsynchronously);

    await using var phone = Connect(_context.DeviceToken);
    phone.On<JsonElement>("StationsChanged", _ => heard.TrySetResult());

    await phone.StartAsync();
    await RenameTheKitchenAsync("Kueche am Zelt");

    var received = await Task.WhenAny(heard.Task, Task.Delay(_patience));

    Assert.That(received,
                Is.SameAs(heard.Task),
                "A phone must be told that a production location was renamed, or it keeps offering the old name.");
  }

  [Test]
  public async Task Rename_AStationWithATabletStandingAtIt_TellsThatTablet()
  {
    TaskCompletionSource heard = new(TaskCreationOptions.RunContinuationsAsynchronously);

    await using var tablet = Connect(_kitchenToken);
    tablet.On<JsonElement>("StationsChanged", _ => heard.TrySetResult());

    await tablet.StartAsync();
    await RenameTheKitchenAsync("Kueche am Zelt");

    var received = await Task.WhenAny(heard.Task, Task.Delay(_patience));

    Assert.That(received,
                Is.SameAs(heard.Task),
                "The tablet standing at a production location must be told when that location is renamed.");
  }

  [Test]
  public async Task Rename_AStationSomewhereElse_LeavesTheTabletOfThisStationAlone()
  {
    TaskCompletionSource heardByTheKitchen = new(TaskCreationOptions.RunContinuationsAsynchronously);
    TaskCompletionSource heardByThePhone = new(TaskCreationOptions.RunContinuationsAsynchronously);

    await using var tablet = Connect(_kitchenToken);
    tablet.On<JsonElement>("StationsChanged", _ => heardByTheKitchen.TrySetResult());

    await using var phone = Connect(_context.DeviceToken);
    phone.On<JsonElement>("StationsChanged", _ => heardByThePhone.TrySetResult());

    await tablet.StartAsync();
    await phone.StartAsync();

    using (var response = await _context.Client.PutAsJsonAsync($"/api/admin/stations/{_context.World.BarStationId}",
                                                               new { name = "Bar im Hof", sortOrder = 2 }))
    {
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    var received = await Task.WhenAny(heardByThePhone.Task, Task.Delay(_patience));

    Assert.That(received, Is.SameAs(heardByThePhone.Task), "Every phone offers every production location.");

    Assert.That(heardByTheKitchen.Task.IsCompleted,
                Is.False,
                "A tablet has no use for a change to a production location it does not stand at.");
  }

  [Test]
  public async Task Deactivate_AStationNoItemNeeds_TellsThePhones()
  {
    TaskCompletionSource heard = new(TaskCreationOptions.RunContinuationsAsynchronously);
    var stationId = await CreateAStationNoItemNeedsAsync();

    await using var phone = Connect(_context.DeviceToken);
    phone.On<JsonElement>("StationsChanged", _ => heard.TrySetResult());

    await phone.StartAsync();

    using (var deactivation = await _context.Client.PostAsync($"/api/admin/stations/{stationId}/deactivate", null))
    {
      Assert.That(deactivation.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    var received = await Task.WhenAny(heard.Task, Task.Delay(_patience));

    Assert.That(received,
                Is.SameAs(heard.Task),
                "A phone must be told that a production location was switched off, or it keeps offering it.");
  }

  private async Task RenameTheKitchenAsync(string newName)
  {
    using var response = await _context.Client
                                       .PutAsJsonAsync($"/api/admin/stations/{_context.World.KitchenStationId}",
                                                       new { name = newName, sortOrder = 1 });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
  }

  private async Task<Guid> CreateAStationNoItemNeedsAsync()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/stations",
                                                               new { name = "Kuchenbuffet", sortOrder = 3 });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.GetProperty("stationId").GetGuid();
  }

  private HubConnection Connect(string deviceToken)
  {
    return new HubConnectionBuilder()
          .WithUrl(new Uri(_context.Factory.BaseAddress, $"hub?access_token={deviceToken}"))
          .Build();
  }
}
