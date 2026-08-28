using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class StationDeactivationTest
{

  [SetUp]
  public async Task SetUp()
  {
    context = await new OrderTestContext.Builder().StartAsync(false);
  }

  [TearDown]
  public async Task TearDown()
  {
    await context.DisposeAsync();
  }

  private OrderTestContext context = null!;

  [Test]
  public async Task Deactivate_FreshStationThatOnlyEverTestPrinted_ReportsNoOpenSlipsAndSwitchesOff()
  {
    var printerId = await CreatePrinterAsync();
    var stationId = await CreateStationAsync(printerId);

    using (var testPrint = await context.Client.PostAsync($"/api/admin/printers/{printerId}/test-print",
                                                          null))
    {
      Assert.That(testPrint.StatusCode,
                  Is.EqualTo(HttpStatusCode.Accepted),
                  "A test print on a fresh station must be accepted.");
    }

    using var response = await context.Client.PostAsync($"/api/admin/stations/{stationId}/deactivate",
                                                        null);

    var body = await response.Content.ReadAsStringAsync();

    Assert.That(response.StatusCode,
                Is.EqualTo(HttpStatusCode.OK),
                $"A station with no orders must switch off. Body: {body}");

    await using var database = context.Factory.CreateContext();
    var station = await database.Stations.FirstAsync(candidate => candidate.Id == stationId);

    Assert.That(station.IsActive, Is.False);
  }

  [Test]
  public async Task Deactivate_StationWithAGenuinelyOpenTicket_IsStillRefused()
  {
    using (var placed = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid())))
    {
      Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    using var response = await context.Client.PostAsync($"/api/admin/stations/{context.World.KitchenStationId}/deactivate",
                                                        null);

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.stationHasOpenTickets"));
                      Assert.That(body.RootElement.GetProperty("parameters").GetProperty("count").GetString(), Is.EqualTo("1"));
                    });
  }

  [Test]
  public async Task Deactivate_StationWhoseItemsWouldLoseTheirOnlyStation_IsRefusedForThatReason()
  {
    using var response = await context.Client.PostAsync($"/api/admin/stations/{context.World.BarStationId}/deactivate",
                                                        null);

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.itemsWouldHaveNoStation"),
                                  "A station losing its items is a different refusal from one holding open slips.");
                    });
  }

  [Test]
  public async Task Deactivate_StationWhoseTicketsAreAllSettled_SwitchesOff()
  {
    using (var placed = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid())))
    {
      Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    await using (var database = context.Factory.CreateContext())
    {
      List<PrintJob> jobs = await database.PrintJobs.ToListAsync();

      foreach (var job in jobs)
      {
        job.Status = PrintJobStatus.HandledOnPaper;
      }

      await database.SaveChangesAsync();
    }

    using (var assigned = await context.Client.PutAsJsonAsync($"/api/admin/items/{context.World.BratwurstItemId}",
                                                              new
                                                              {
                                                                name = "Bratwurst mit Brot",
                                                                categoryName = "Essen",
                                                                priceCents = 350,
                                                                sortOrder = 1,
                                                                stationIds = new[] { context.World.BarStationId }
                                                              }))
    {
      Assert.That(assigned.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var response = await context.Client.PostAsync($"/api/admin/stations/{context.World.KitchenStationId}/deactivate",
                                                        null);

    Assert.That(response.StatusCode,
                Is.EqualTo(HttpStatusCode.OK),
                $"Settled slips must not block a station. Body: {await response.Content.ReadAsStringAsync()}");
  }

  [Test]
  public async Task Activate_StationThatWasSwitchedOff_SwitchesItBackOn()
  {
    var stationId = await CreateStationAsync();

    using (var switchedOff = await context.Client.PostAsync($"/api/admin/stations/{stationId}/deactivate",
                                                            null))
    {
      Assert.That(switchedOff.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var response = await context.Client.PostAsync($"/api/admin/stations/{stationId}/activate",
                                                        null);

    var body = await response.Content.ReadAsStringAsync();

    Assert.That(response.StatusCode,
                Is.EqualTo(HttpStatusCode.OK),
                $"A station that was switched off must be switchable back on. Body: {body}");

    await using var database = context.Factory.CreateContext();
    var station = await database.Stations.FirstAsync(candidate => candidate.Id == stationId);

    Assert.That(station.IsActive, Is.True);
  }

  private async Task<Guid> CreatePrinterAsync()
  {
    using var response = await context.Client.PostAsJsonAsync("/api/admin/printers",
                                                              new { printerType = "TestPrinter", name = "Drucker Zelt" });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.GetProperty("printerId").GetGuid();
  }

  private async Task<Guid> CreateStationAsync(Guid? printerId = null)
  {
    using var response = await context.Client.PostAsJsonAsync("/api/admin/stations",
                                                              new { name = "Zelt", sortOrder = 3, printerId });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.GetProperty("stationId").GetGuid();
  }
}
