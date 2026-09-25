using System.Net;
using System.Net.Http.Json;
using GastronomyApp.Api.Tests.TestSupport;

namespace GastronomyApp.Api.Tests.Announcers;

[TestFixture]
public sealed class ConfigurationChangedAnnouncementTest
{
  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContextBuilder().StartAsync();
    _kitchenToken = await _context.IssueStationTokenAsync(_context.World.KitchenStationId);
    _phone = new(new(_context.Factory.BaseAddress, $"hub?access_token={_context.DeviceToken}"), "ConfigurationChanged");
    _admin = new(new(_context.Factory.BaseAddress, "hub"), "ConfigurationChanged");
    _tablet = new(new(_context.Factory.BaseAddress, $"hub?access_token={_kitchenToken}"), "ConfigurationChanged");
  }

  [TearDown]
  public async Task TearDown()
  {
    await _phone.DisposeAsync();
    await _admin.DisposeAsync();
    await _tablet.DisposeAsync();
    await _context.DisposeAsync();
  }

  private readonly TimeSpan _patience = TimeSpan.FromSeconds(10);
  private readonly TimeSpan _settleTime = TimeSpan.FromMilliseconds(500);

  private HubEventListener _admin = null!;
  private OrderTestContext _context = null!;
  private string _kitchenToken = null!;
  private HubEventListener _phone = null!;
  private HubEventListener _tablet = null!;

  [Test]
  public async Task PutCategory_ARenamedCategory_TellsEveryScreenOnce()
  {
    await ListenAsync();

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/categories/{_context.World.FoodCategoryId}",
                                                              new
                                                              {
                                                                name = "Speisen",
                                                                colourHex = "#2E7D32"
                                                              });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    await AssertEveryScreenHeardOnceAsync();
  }

  [Test]
  public async Task PutItem_ARenamedArticle_TellsEveryScreenOnce()
  {
    await ListenAsync();

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/items/{_context.World.BeerItemId}",
                                                              new
                                                              {
                                                                name = "Bier vom Fass",
                                                                categoryId = _context.World.DrinkCategoryId,
                                                                sortOrder = 2
                                                              });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    await AssertEveryScreenHeardOnceAsync();
  }

  [Test]
  public async Task PostFestival_ANewFestival_TellsEveryScreenOnce()
  {
    await ListenAsync();

    var startsAtUtc = DateTime.UtcNow.AddYears(2);

    using var response = await _context.Client.PostAsJsonAsync("/api/admin/festivals",
                                                               new
                                                               {
                                                                 name = "Herbstfest",
                                                                 startsAtUtc = startsAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                                                                 endsAtUtc = startsAtUtc.AddHours(24).ToString("yyyy-MM-ddTHH:mm:ssZ")
                                                               });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    await AssertEveryScreenHeardOnceAsync();
  }

  [Test]
  public async Task PutStation_ARenamedStation_TellsEveryScreenOnce()
  {
    await ListenAsync();

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/stations/{_context.World.KitchenStationId}",
                                                              new
                                                              {
                                                                name = "Kueche am Zelt",
                                                                sortOrder = 1
                                                              });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    await AssertEveryScreenHeardOnceAsync();
  }

  [Test]
  public async Task PutStaffMember_ARenamedWaiter_TellsEveryScreenOnce()
  {
    await ListenAsync();

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/staff-members/{_context.World.StaffMemberId}", new { name = "Anna Maria" });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    await AssertEveryScreenHeardOnceAsync();
  }

  [Test]
  public async Task DeleteFestivalStation_AStationWithArticlesAssignedToIt_TellsEveryScreenOnceForAllRowsItRemoves()
  {
    using (var assigned = await _context.Client.PutAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/items/{_context.World.BeerItemId}",
                                                               new
                                                               {
                                                                 priceCents = 300,
                                                                 stationIds = new[] { _context.World.BarStationId, _context.World.KitchenStationId }
                                                               }))
    {
      Assert.That(assigned.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    await ListenAsync();

    using var response = await _context.Client.DeleteAsync($"/api/admin/festivals/{_context.World.FestivalId}/stations/{_context.World.BarStationId}");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent), await response.Content.ReadAsStringAsync());
    await AssertEveryScreenHeardOnceAsync();
  }

  [Test]
  public async Task PostInvitation_AWaiterWhosePhoneIsReplaced_TellsTheAdminOnceForAllItsSaves()
  {
    await ListenAsync();

    using var response = await _context.Client.PostAsJsonAsync("/api/admin/enrolment/invitations", new { staffMemberId = _context.World.StaffMemberId });

    Assert.That(response.IsSuccessStatusCode, Is.True, await response.Content.ReadAsStringAsync());
    Assert.That(await Task.WhenAny(_admin.FirstHeard, Task.Delay(_patience)), Is.SameAs(_admin.FirstHeard));

    await Task.Delay(_settleTime);

    Assert.That(_admin.HeardCount, Is.EqualTo(1));
  }

  private async Task ListenAsync()
  {
    await _phone.StartAsync();
    await _admin.StartAsync();
    await _tablet.StartAsync();
  }

  private async Task AssertEveryScreenHeardOnceAsync()
  {
    Task heardByAll = Task.WhenAll(_phone.FirstHeard, _admin.FirstHeard, _tablet.FirstHeard);

    Assert.That(await Task.WhenAny(heardByAll, Task.Delay(_patience)), Is.SameAs(heardByAll), "The phone, the laptop and the station tablet must each be told that the configuration changed.");

    await Task.Delay(_settleTime);

    Assert.Multiple(() =>
                    {
                      Assert.That(_phone.HeardCount, Is.EqualTo(1), "phone");
                      Assert.That(_admin.HeardCount, Is.EqualTo(1), "admin");
                      Assert.That(_tablet.HeardCount, Is.EqualTo(1), "station tablet");
                    });
  }
}
