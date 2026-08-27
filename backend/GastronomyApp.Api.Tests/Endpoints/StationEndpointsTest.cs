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
    private Guid ticketId;
    private Guid orderId;

    [SetUp]
    public async Task SetUp()
    {
        context = await new OrderTestContext.Builder().StartAsync(withRunningPrinters: false);

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
    public async Task GetStations_NoDeviceToken_IsRefused()
    {
        using HttpResponseMessage response = await context.Client.GetAsync("/api/stations");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetStations_EnrolledDevice_ListsEveryActiveStationWithItsCanPrintFlag()
    {
        using HttpResponseMessage response = await context.SendAsync(HttpMethod.Get, "/api/stations");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body.RootElement.GetProperty("stations").GetArrayLength(), Is.EqualTo(2));
            Assert.That(
                body.RootElement.GetProperty("stations")[0].GetProperty("canPrintRightNow").GetBoolean(),
                Is.True);
        });
    }

    [Test]
    public async Task GetTickets_HealthyStation_ListsTheOpenTicketWithItsAcknowledgeDecision()
    {
        using HttpResponseMessage response = await context.SendAsync(HttpMethod.Get, $"/api/stations/{context.World.KitchenStationId}/tickets");
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

        using HttpResponseMessage response = await context.SendAsync(HttpMethod.Get, $"/api/stations/{context.World.KitchenStationId}/tickets");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(body.RootElement.GetProperty("tickets").GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task GetTickets_AnotherStation_ListsThatStationsBacklog()
    {
        using HttpResponseMessage response = await context.SendAsync(
            HttpMethod.Get,
            $"/api/stations/{context.World.BarStationId}/tickets");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(body.RootElement.GetProperty("tickets").GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task Acknowledge_HealthyStationPrinter_IsRefused()
    {
        using HttpResponseMessage response = await context.SendAsync(HttpMethod.Post, $"/api/stations/{context.World.KitchenStationId}/tickets/{ticketId}/acknowledge");

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

        using HttpResponseMessage response = await context.SendAsync(HttpMethod.Post, $"/api/stations/{context.World.KitchenStationId}/tickets/{ticketId}/acknowledge");

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

        using HttpResponseMessage response = await context.SendAsync(HttpMethod.Post, $"/api/stations/{context.World.KitchenStationId}/tickets/{ticketId}/acknowledge");

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

        using (HttpResponseMessage first = await context.SendAsync(HttpMethod.Post, $"/api/stations/{context.World.KitchenStationId}/tickets/{ticketId}/acknowledge"))
        {
            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        using HttpResponseMessage second = await context.SendAsync(HttpMethod.Post, $"/api/stations/{context.World.KitchenStationId}/tickets/{ticketId}/acknowledge");

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

        using HttpResponseMessage response = await context.SendAsync(HttpMethod.Post, $"/api/stations/{context.World.KitchenStationId}/tickets/{ticketId}/acknowledge");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task EveryStationRoute_NoDeviceToken_IsRefused()
    {
        using HttpResponseMessage stations = await context.Client.GetAsync("/api/stations");
        using HttpResponseMessage tickets = await context.Client.GetAsync(
            $"/api/stations/{context.World.KitchenStationId}/tickets");
        using HttpResponseMessage status = await context.Client.GetAsync(
            $"/api/stations/{context.World.KitchenStationId}/status");
        using HttpResponseMessage acknowledge = await context.Client.PostAsync(
            $"/api/stations/{context.World.KitchenStationId}/tickets/{ticketId}/acknowledge",
            content: null);

        Assert.Multiple(() =>
        {
            Assert.That(stations.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(tickets.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(status.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(acknowledge.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
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
            candidate => candidate.StationId == context.World.KitchenStationId);
        PrinterConfiguration configuration = await database.PrinterConfigurations.FirstAsync(
            candidate => candidate.StationId == context.World.KitchenStationId);

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
