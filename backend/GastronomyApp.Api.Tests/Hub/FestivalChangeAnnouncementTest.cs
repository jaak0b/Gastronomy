using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Api.Tests.Endpoints;
using Microsoft.AspNetCore.SignalR.Client;

namespace GastronomyApp.Api.Tests.Hub;

[TestFixture]
public sealed class FestivalChangeAnnouncementTest
{
  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContextBuilder().StartAsync();
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
  public async Task Show_AHiddenFestival_TellsTheStationTablet()
  {
    TaskCompletionSource heard = new(TaskCreationOptions.RunContinuationsAsynchronously);
    var festivalId = await AHiddenFestivalAsync();

    await using var tablet = Connect(_kitchenToken);
    tablet.On<JsonElement>("FestivalChanged", _ => heard.TrySetResult());

    await tablet.StartAsync();
    await ShowTheFestivalAsync(festivalId);

    var received = await Task.WhenAny(heard.Task, Task.Delay(_patience));

    Assert.That(received,
                Is.SameAs(heard.Task),
                "The tablet standing at a production location must be told when a festival is shown, or it keeps saying that no festival is running.");
  }

  [Test]
  public async Task Show_AHiddenFestival_TellsThePhones()
  {
    TaskCompletionSource heard = new(TaskCreationOptions.RunContinuationsAsynchronously);
    var festivalId = await AHiddenFestivalAsync();

    await using var phone = Connect(_context.DeviceToken);
    phone.On<JsonElement>("FestivalChanged", _ => heard.TrySetResult());

    await phone.StartAsync();
    await ShowTheFestivalAsync(festivalId);

    var received = await Task.WhenAny(heard.Task, Task.Delay(_patience));

    Assert.That(received,
                Is.SameAs(heard.Task),
                "A phone must be told when a festival is shown, or it keeps showing the menu it loaded before.");
  }

  [Test]
  public async Task Show_AHiddenFestival_DoesNotTellThePhonesTheCatalogChanged()
  {
    TaskCompletionSource heardTheFestivalChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
    TaskCompletionSource heardTheCatalogChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
    var festivalId = await AHiddenFestivalAsync();

    await using var phone = Connect(_context.DeviceToken);
    phone.On<JsonElement>("FestivalChanged", _ => heardTheFestivalChanged.TrySetResult());
    phone.On<JsonElement>("CatalogChanged", _ => heardTheCatalogChanged.TrySetResult());

    await phone.StartAsync();
    await ShowTheFestivalAsync(festivalId);

    var received = await Task.WhenAny(heardTheFestivalChanged.Task, Task.Delay(_patience));

    Assert.That(received,
                Is.SameAs(heardTheFestivalChanged.Task),
                "A phone must be told when a festival is shown, or it keeps showing the menu it loaded before.");

    Assert.That(heardTheCatalogChanged.Task.IsCompleted,
                Is.False,
                "Showing a festival changes no item and no price, so a phone has no reason to fetch the catalog again.");
  }

  [Test]
  public async Task Hide_AFestivalInTheFuture_TellsTheStationTablet()
  {
    TaskCompletionSource heard = new(TaskCreationOptions.RunContinuationsAsynchronously);
    var festivalId = await AFestivalInTheFutureAsync();

    await using var tablet = Connect(_kitchenToken);
    tablet.On<JsonElement>("FestivalChanged", _ => heard.TrySetResult());

    await tablet.StartAsync();
    await HideTheFestivalAsync(festivalId);

    var received = await Task.WhenAny(heard.Task, Task.Delay(_patience));

    Assert.That(received,
                Is.SameAs(heard.Task),
                "The tablet standing at a production location must be told when a festival is hidden, or it keeps taking orders for an event that is not running.");
  }

  private async Task<Guid> AFestivalInTheFutureAsync()
  {
    var startsAtUtc = DateTime.UtcNow.AddYears(2);
    Guid festivalId;

    using (var created = await _context.Client.PostAsJsonAsync("/api/admin/festivals",
                                                               new
                                                               {
                                                                 name = "Herbstfest",
                                                                 startsAtUtc = startsAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                                                                 endsAtUtc = startsAtUtc.AddHours(24)
                                                                                        .ToString("yyyy-MM-ddTHH:mm:ssZ")
                                                               }))
    {
      Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));

      var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
      festivalId = body.RootElement.GetProperty("festivalId").GetGuid();
    }

    return festivalId;
  }

  private async Task<Guid> AHiddenFestivalAsync()
  {
    var festivalId = await AFestivalInTheFutureAsync();

    await HideTheFestivalAsync(festivalId);

    return festivalId;
  }

  private async Task HideTheFestivalAsync(Guid festivalId)
  {
    using var response = await _context.Client.PostAsync($"/api/admin/festivals/{festivalId}/hide", null);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
  }

  private async Task ShowTheFestivalAsync(Guid festivalId)
  {
    using var response = await _context.Client.PostAsync($"/api/admin/festivals/{festivalId}/show", null);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
  }

  private HubConnection Connect(string deviceToken)
  {
    return new HubConnectionBuilder()
          .WithUrl(new Uri(_context.Factory.BaseAddress, $"hub?access_token={deviceToken}"))
          .Build();
  }
}
