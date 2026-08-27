using System.Net;
using System.Text.Json;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class StationEndpointsTest
{
    private OrderTestContext context = null!;
    private string accessKey = null!;
    private Guid ticketId;
    private Guid orderId;

    [SetUp]
    public async Task SetUp()
    {
        context = await new OrderTestContext.Builder().StartAsync(withRunningPrinters: false);
        accessKey = context.World.KitchenLocationId.ToString("N");

        using HttpResponseMessage created = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid(), 700));
        JsonDocument body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        orderId = body.RootElement.GetProperty("orderId").GetGuid();
        ticketId = body.RootElement.GetProperty("tickets")[0].GetProperty("ticketId").GetGuid();
    }

    [TearDown]
    public async Task TearDown()
    {
        await context.DisposeAsync();
    }

    [Test]
    public async Task GetShell_ValidAccessKey_ServesTheSinglePageAppShell()
    {
        using HttpResponseMessage response = await context.Client.GetAsync($"/station/{accessKey}");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("text/html"));
        });
    }

    [Test]
    public async Task GetLocations_ValidAccessKey_ListsEveryActiveLocationWithItsCanPrintFlag()
    {
        using HttpResponseMessage response = await context.Client.GetAsync($"/api/station/{accessKey}/locations");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body.RootElement.GetProperty("locations").GetArrayLength(), Is.EqualTo(2));
            Assert.That(
                body.RootElement.GetProperty("locations")[0].GetProperty("canPrintRightNow").GetBoolean(),
                Is.True);
        });
    }

    [Test]
    public async Task GetTickets_HealthyStation_ListsTheOpenTicketWithItsAcknowledgeDecision()
    {
        using HttpResponseMessage response = await context.Client.GetAsync($"/api/station/{accessKey}/tickets");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement tickets = body.RootElement.GetProperty("tickets");

        Assert.Multiple(() =>
        {
            Assert.That(tickets.GetArrayLength(), Is.EqualTo(1));
            Assert.That(tickets[0].GetProperty("ticketId").GetGuid(), Is.EqualTo(ticketId));
            Assert.That(tickets[0].GetProperty("globalOrderNumber").GetInt32(), Is.EqualTo(1));
            Assert.That(tickets[0].GetProperty("sequenceNumber").GetInt32(), Is.EqualTo(1));
            Assert.That(tickets[0].GetProperty("tableLabel").GetString(), Is.EqualTo("Tisch 12"));
            Assert.That(tickets[0].GetProperty("reprintCount").GetInt32(), Is.EqualTo(0));
            Assert.That(tickets[0].GetProperty("lines").GetArrayLength(), Is.EqualTo(1));
            Assert.That(tickets[0].GetProperty("canAcknowledge").GetBoolean(), Is.False);
        });
    }

    [Test]
    public async Task GetTickets_ClosedTicket_LeavesItOutOfTheList()
    {
        await SetTicketStatusAsync(LocationTicketStatus.Printed);

        using HttpResponseMessage response = await context.Client.GetAsync($"/api/station/{accessKey}/tickets");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(body.RootElement.GetProperty("tickets").GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task GetTickets_LocationIdFilter_SelectsThatLocationInsteadOfTheKeysOwn()
    {
        using HttpResponseMessage response = await context.Client.GetAsync(
            $"/api/station/{accessKey}/tickets?locationId={context.World.BarLocationId}");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(body.RootElement.GetProperty("tickets").GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task Acknowledge_HealthyStation_IsRefused()
    {
        using HttpResponseMessage response = await context.Client.PostAsync(
            $"/api/station/{accessKey}/tickets/{ticketId}/acknowledge",
            content: null);

        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

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
    public async Task Acknowledge_PrintingTicketUnderEveryStationCondition_IsRefused(string condition)
    {
        await SetTicketStatusAsync(LocationTicketStatus.Printing);
        await ApplyStationConditionAsync(condition);

        using HttpResponseMessage response = await context.Client.PostAsync(
            $"/api/station/{accessKey}/tickets/{ticketId}/acknowledge",
            content: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [TestCase("IsFaulty")]
    [TestCase("IsOffline")]
    [TestCase("IsPaperEnd")]
    [TestCase("IsCoverOpen")]
    [TestCase("IsInErrorState")]
    [TestCase("IsDisabled")]
    public async Task Acknowledge_QueuedTicketAtAStationThatCannotPrint_MovesItToHandledOnPaper(string condition)
    {
        await ApplyStationConditionAsync(condition);

        using HttpResponseMessage response = await context.Client.PostAsync(
            $"/api/station/{accessKey}/tickets/{ticketId}/acknowledge",
            content: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        LocationTicket ticket = await database.LocationTickets.FirstAsync(candidate => candidate.Id == ticketId);
        Order order = await database.Orders.FirstAsync(candidate => candidate.Id == orderId);

        Assert.Multiple(() =>
        {
            Assert.That(ticket.Status, Is.EqualTo(LocationTicketStatus.HandledOnPaper));
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Printed));
        });
    }

    [Test]
    public async Task Acknowledge_SecondAttempt_IsRefusedAsAlreadyTaken()
    {
        await ApplyStationConditionAsync("IsPaperEnd");

        using (HttpResponseMessage first = await context.Client.PostAsync(
            $"/api/station/{accessKey}/tickets/{ticketId}/acknowledge",
            content: null))
        {
            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        using HttpResponseMessage second = await context.Client.PostAsync(
            $"/api/station/{accessKey}/tickets/{ticketId}/acknowledge",
            content: null);

        JsonDocument body = JsonDocument.Parse(await second.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("station.alreadyTaken"));
        });
    }

    [Test]
    public async Task Acknowledge_FailedTicketAtAHealthyStation_IsAllowed()
    {
        await SetTicketStatusAsync(LocationTicketStatus.Failed);

        using HttpResponseMessage response = await context.Client.PostAsync(
            $"/api/station/{accessKey}/tickets/{ticketId}/acknowledge",
            content: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task EveryStationRoute_UnknownAccessKey_AnswersNotFound()
    {
        string unknownKey = Guid.NewGuid().ToString("N");

        using HttpResponseMessage locations = await context.Client.GetAsync($"/api/station/{unknownKey}/locations");
        using HttpResponseMessage tickets = await context.Client.GetAsync($"/api/station/{unknownKey}/tickets");
        using HttpResponseMessage status = await context.Client.GetAsync($"/api/station/{unknownKey}/status");
        using HttpResponseMessage acknowledge = await context.Client.PostAsync(
            $"/api/station/{unknownKey}/tickets/{ticketId}/acknowledge",
            content: null);

        Assert.Multiple(() =>
        {
            Assert.That(locations.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(tickets.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(status.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(acknowledge.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    private async Task SetTicketStatusAsync(LocationTicketStatus status)
    {
        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        LocationTicket ticket = await database.LocationTickets.FirstAsync(candidate => candidate.Id == ticketId);
        ticket.Status = status;
        await database.SaveChangesAsync();
    }

    private async Task ApplyStationConditionAsync(string condition)
    {
        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        PrinterStatus status = await database.PrinterStatuses.FirstAsync(
            candidate => candidate.ProductionLocationId == context.World.KitchenLocationId);
        PrinterConfiguration configuration = await database.PrinterConfigurations.FirstAsync(
            candidate => candidate.ProductionLocationId == context.World.KitchenLocationId);

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
                configuration.IsEnabled = false;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(condition), condition, "Unknown station condition.");
        }

        await database.SaveChangesAsync();
    }
}
