using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class ItemsLeftWithoutAStationTest
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

  private OrderTestContext _context = null!;

  [Test]
  public async Task PutOnTheMenu_WithOnlyAStationThatIsSwitchedOff_IsRefusedAndLeavesTheItemOrderable()
  {
    var switchedOffStationId = await StationSwitchedOffAtTheFestivalAsync();

    using (var response = await _context.Client
                                        .PutAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/items/{_context.World.BratwurstItemId}",
                                                        new
                                                        {
                                                          priceCents = 350,
                                                          stationIds = new[] { switchedOffStationId }
                                                        }))
    {
      var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

      Assert.Multiple(() =>
                      {
                        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                        Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                    Is.EqualTo("admin.itemNeedsAStation"));
                      });
    }

    using var placed = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid()));

    Assert.That(placed.StatusCode,
                Is.EqualTo(HttpStatusCode.Created),
                $"The refused menu change must leave the item at its station. Body: {await placed.Content.ReadAsStringAsync()}");
  }

  [Test]
  public async Task RemoveStationFromFestival_WhenTheOtherStationIsSwitchedOff_IsRefusedAndLeavesTheItemOrderable()
  {
    await StationSwitchedOffAtTheFestivalAsync(_context.World.KitchenStationId);

    using (var response = await _context.Client
                                        .DeleteAsync($"/api/admin/festivals/{_context.World.FestivalId}/stations/{_context.World.KitchenStationId}"))
    {
      var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

      Assert.Multiple(() =>
                      {
                        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                        Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                    Is.EqualTo("admin.itemsWouldHaveNoStation"));
                        Assert.That(body.RootElement.GetProperty("parameters").GetProperty("count").GetString(),
                                    Is.EqualTo("1"));
                      });
    }

    using var placed = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid()));

    Assert.That(placed.StatusCode,
                Is.EqualTo(HttpStatusCode.Created),
                $"The refused removal must leave the item at its station. Body: {await placed.Content.ReadAsStringAsync()}");
  }

  private async Task<Guid> StationSwitchedOffAtTheFestivalAsync(Guid? alsoPreparingWith = null)
  {
    Guid stationId;

    using (var created = await _context.Client.PostAsJsonAsync("/api/admin/stations",
                                                               new { name = "Zelt", sortOrder = 3 }))
    {
      Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
      stationId = JsonDocument.Parse(await created.Content.ReadAsStringAsync())
                              .RootElement.GetProperty("stationId")
                              .GetGuid();
    }

    using (var added = await _context.Client
                                     .PutAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/stations/{stationId}",
                                                     new { }))
    {
      Assert.That(added.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    if (alsoPreparingWith is { } partnerStationId)
    {
      using var assigned = await _context.Client
                                         .PutAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/items/{_context.World.BratwurstItemId}",
                                                         new
                                                         {
                                                           priceCents = 350,
                                                           stationIds = new[] { partnerStationId, stationId }
                                                         });

      Assert.That(assigned.StatusCode,
                  Is.EqualTo(HttpStatusCode.OK),
                  await assigned.Content.ReadAsStringAsync());
    }

    using (var switchedOff = await _context.Client.PostAsync($"/api/admin/stations/{stationId}/deactivate", null))
    {
      Assert.That(switchedOff.StatusCode,
                  Is.EqualTo(HttpStatusCode.OK),
                  await switchedOff.Content.ReadAsStringAsync());
    }

    return stationId;
  }
}
