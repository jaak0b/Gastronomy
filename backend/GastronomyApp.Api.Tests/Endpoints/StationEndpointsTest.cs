using System.Net;
using System.Text.Json;
using GastronomyApp.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class StationEndpointsTest
{

  [SetUp]
  public async Task SetUp()
  {
    context = await new OrderTestContext.Builder().StartAsync(false);

    using var created = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid()));
    var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
    orderId = body.RootElement.GetProperty("orderId").GetGuid();
    ticketId = body.RootElement.GetProperty("stationOrders")[0].GetProperty("stationOrderId").GetGuid();
  }

  [TearDown]
  public async Task TearDown()
  {
    await context.DisposeAsync();
  }

  private OrderTestContext context = null!;
  private Guid ticketId;
  private Guid orderId;

  [Test]
  public async Task GetStations_NoDeviceToken_IsRefused()
  {
    using var response = await context.Client.GetAsync("/api/stations");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
  }

  [Test]
  public async Task GetStations_EnrolledDevice_ListsEveryActiveStationWithItsCanPrintFlag()
  {
    using var response = await context.SendAsync(HttpMethod.Get, "/api/stations");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("stations").GetArrayLength(), Is.EqualTo(2));
                      Assert.That(body.RootElement.GetProperty("stations")[0].GetProperty("canPrintRightNow").GetBoolean(),
                                  Is.True);
                    });
  }

  [Test]
  public async Task GetStationOrders_HealthyStation_ListsTheOpenTicketWithItsAcknowledgeDecision()
  {
    using var response = await context.SendAsync(HttpMethod.Get, $"/api/stations/{context.World.KitchenStationId}/station-orders");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var tickets = body.RootElement.GetProperty("stationOrders");

    Assert.Multiple(() =>
                    {
                      Assert.That(tickets.GetArrayLength(), Is.EqualTo(1));
                      Assert.That(tickets[0].GetProperty("stationOrderId").GetGuid(), Is.EqualTo(ticketId));
                      Assert.That(tickets[0].GetProperty("globalOrderNumber").GetInt32(), Is.EqualTo(1));
                      Assert.That(tickets[0].GetProperty("stationOrderNumber").GetInt32(), Is.EqualTo(1));
                      Assert.That(tickets[0].GetProperty("tableName").GetString(), Is.EqualTo("Tisch 12"));
                      Assert.That(tickets[0].GetProperty("copyNumber").GetInt32(), Is.EqualTo(0));
                      Assert.That(tickets[0].GetProperty("items").GetArrayLength(), Is.EqualTo(1));
                      Assert.That(tickets[0].GetProperty("canHandleOnPaper").GetBoolean(), Is.False);
                    });
  }

  [Test]
  public async Task GetStationOrders_ClosedTicket_LeavesItOutOfTheList()
  {
    await SetPrintJobStatusAsync(PrintJobStatus.Printed);

    using var response = await context.SendAsync(HttpMethod.Get, $"/api/stations/{context.World.KitchenStationId}/station-orders");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.That(body.RootElement.GetProperty("stationOrders").GetArrayLength(), Is.EqualTo(0));
  }

  [Test]
  public async Task GetStationOrders_AnotherStation_ListsThatStationsBacklog()
  {
    using var response = await context.SendAsync(HttpMethod.Get,
                                                 $"/api/stations/{context.World.BarStationId}/station-orders");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.That(body.RootElement.GetProperty("stationOrders").GetArrayLength(), Is.EqualTo(0));
  }

  [Test]
  public async Task HandOnPaper_HealthyStationPrinter_IsRefused()
  {
    using var response = await context.SendAsync(HttpMethod.Post, $"/api/stations/{context.World.KitchenStationId}/station-orders/{ticketId}/hand-on-paper");

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("station.takeRefused"));
                    });
  }

  [TestCase("IsFaulty")]
  [TestCase("IsOffline")]
  [TestCase("IsPaperEnd")]
  [TestCase("IsCoverOpen")]
  [TestCase("IsInErrorState")]
  [TestCase("IsDisabled")]
  public async Task HandOnPaper_PrintingTicketUnderEveryStationCondition_IsRefused(string condition)
  {
    await SetPrintJobStatusAsync(PrintJobStatus.Sending);
    await ApplyStationConditionAsync(condition);

    using var response = await context.SendAsync(HttpMethod.Post, $"/api/stations/{context.World.KitchenStationId}/station-orders/{ticketId}/hand-on-paper");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
  }

  [TestCase("IsFaulty")]
  [TestCase("IsOffline")]
  [TestCase("IsPaperEnd")]
  [TestCase("IsCoverOpen")]
  [TestCase("IsInErrorState")]
  [TestCase("IsDisabled")]
  public async Task HandOnPaper_QueuedTicketAtAStationThatCannotPrint_MovesItToHandledOnPaper(string condition)
  {
    await ApplyStationConditionAsync(condition);

    using var response = await context.SendAsync(HttpMethod.Post, $"/api/stations/{context.World.KitchenStationId}/station-orders/{ticketId}/hand-on-paper");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    await using var database = context.Factory.CreateContext();
    var job = await database.PrintJobs.FirstAsync(candidate => candidate.StationOrderId == ticketId);

    Assert.That(job.Status, Is.EqualTo(PrintJobStatus.HandledOnPaper));
  }

  [Test]
  public async Task HandOnPaper_SecondAttempt_IsRefusedAsAlreadyTaken()
  {
    await ApplyStationConditionAsync("IsPaperEnd");

    using (var first = await context.SendAsync(HttpMethod.Post, $"/api/stations/{context.World.KitchenStationId}/station-orders/{ticketId}/hand-on-paper"))
    {
      Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var second = await context.SendAsync(HttpMethod.Post, $"/api/stations/{context.World.KitchenStationId}/station-orders/{ticketId}/hand-on-paper");

    var body = JsonDocument.Parse(await second.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("station.alreadyTaken"));
                    });
  }

  [Test]
  public async Task HandOnPaper_FailedTicketAtAHealthyStation_IsAllowed()
  {
    await SetPrintJobStatusAsync(PrintJobStatus.Failed);

    using var response = await context.SendAsync(HttpMethod.Post, $"/api/stations/{context.World.KitchenStationId}/station-orders/{ticketId}/hand-on-paper");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
  }

  [Test]
  public async Task EveryStationRoute_NoDeviceToken_IsRefused()
  {
    using var stations = await context.Client.GetAsync("/api/stations");
    using var tickets = await context.Client.GetAsync($"/api/stations/{context.World.KitchenStationId}/station-orders");
    using var status = await context.Client.GetAsync($"/api/stations/{context.World.KitchenStationId}/status");
    using var acknowledge = await context.Client.PostAsync($"/api/stations/{context.World.KitchenStationId}/station-orders/{ticketId}/hand-on-paper",
                                                           null);

    Assert.Multiple(() =>
                    {
                      Assert.That(stations.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
                      Assert.That(tickets.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
                      Assert.That(status.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
                      Assert.That(acknowledge.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
                    });
  }

  private async Task SetPrintJobStatusAsync(PrintJobStatus status)
  {
    await using var database = context.Factory.CreateContext();
    var job = await database.PrintJobs.FirstAsync(candidate => candidate.StationOrderId == ticketId);
    job.Status = status;
    await database.SaveChangesAsync();
  }

  private async Task ApplyStationConditionAsync(string condition)
  {
    await using var database = context.Factory.CreateContext();
    var station = await database.Stations.FirstAsync(candidate => candidate.Id == context.World.KitchenStationId);
    var status = await database.PrinterStatuses.FirstAsync(candidate => candidate.PrinterId == station.PrinterId);

    switch (condition)
    {
      case "IsFaulty":
        status.IsFaulty = true;
        break;
      case "IsOffline":
        status.IsOnline = false;
        break;
      case "IsPaperEnd":
        status.IsPaperEnd = true;
        break;
      case "IsCoverOpen":
        status.IsCoverOpen = true;
        break;
      case "IsInErrorState":
        status.IsInErrorState = true;
        break;
      case "IsDisabled":
        station.PrinterId = null;
        break;
      default:
        throw new ArgumentOutOfRangeException(nameof(condition), condition, "Unknown station condition.");
    }

    await database.SaveChangesAsync();
  }
}
