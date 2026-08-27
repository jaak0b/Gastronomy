using System.Net;
using System.Text.Json;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class OrderEndpointsTest
{
    private OrderTestContext context = null!;

    [SetUp]
    public async Task SetUp()
    {
        context = await new OrderTestContext.Builder().StartAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await context.DisposeAsync();
    }

    [Test]
    public async Task PostOrder_FirstSubmission_IsAcceptedNumberedAndProjected()
    {
        using HttpResponseMessage response = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid(), 700));
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(body.RootElement.GetProperty("globalOrderNumber").GetInt32(), Is.EqualTo(1));
            Assert.That(body.RootElement.GetProperty("totalCents").GetInt32(), Is.EqualTo(700));
            Assert.That(body.RootElement.GetProperty("expectedTotalCents").GetInt32(), Is.EqualTo(700));
            Assert.That(body.RootElement.GetProperty("tickets").GetArrayLength(), Is.EqualTo(1));
        });

        JsonElement ticket = body.RootElement.GetProperty("tickets")[0];

        Assert.Multiple(() =>
        {
            Assert.That(ticket.GetProperty("locationId").GetGuid(), Is.EqualTo(context.World.KitchenLocationId));
            Assert.That(ticket.GetProperty("locationName").GetString(), Is.EqualTo("Kueche"));
            Assert.That(ticket.GetProperty("sequenceNumber").GetInt32(), Is.EqualTo(1));
            Assert.That(ticket.GetProperty("lineIds").GetArrayLength(), Is.EqualTo(1));
        });

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        Order stored = await database.Orders.SingleAsync();

        Assert.That(stored.Status, Is.EqualTo(OrderStatus.Accepted));
    }

    [Test]
    public async Task PostOrder_TwoStations_EnqueuesOnePrintJobPerTicket()
    {
        OrderBody twoStations = new(
            Guid.NewGuid(),
            "Tisch 12",
            null,
            1000,
            [
                new OrderLineBody(context.World.BratwurstItemId, 2, null, null),
                new OrderLineBody(context.World.BeerItemId, 1, null, null),
            ]);

        using HttpResponseMessage response = await context.PostOrderAsync(twoStations);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        int printJobCount = await database.PrintJobs.CountAsync(job => job.Kind == PrintJobKind.Initial);

        Assert.That(printJobCount, Is.EqualTo(2));
    }

    [Test]
    public async Task PostOrder_UnknownItemId_IsRefusedAsUnprocessable()
    {
        OrderBody unknownItem = new(
            Guid.NewGuid(),
            "Tisch 12",
            null,
            0,
            [new OrderLineBody(Guid.NewGuid(), 1, null, null)]);

        using HttpResponseMessage response = await context.PostOrderAsync(unknownItem);
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(body.RootElement.GetProperty("code").GetString(), Is.EqualTo("UnprocessableEntity"));
        });
    }

    [Test]
    public async Task PostOrder_NoLines_IsRefusedAsAValidationFailure()
    {
        OrderBody empty = new(Guid.NewGuid(), "Tisch 12", null, 0, []);

        using HttpResponseMessage response = await context.PostOrderAsync(empty);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PostOrder_SoldOutItem_IsStillAccepted()
    {
        await using (GastronomyAppDbContext database = context.Factory.CreateContext())
        {
            CatalogItem item = await database.CatalogItems.FirstAsync(
                candidate => candidate.Id == context.World.BratwurstItemId);
            item.IsAvailable = false;
            await database.SaveChangesAsync();
        }

        using HttpResponseMessage response = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid(), 700));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task PostOrder_WrongExpectedTotal_StoresTheBackendTotalAndEchoesBoth()
    {
        OrderBody wrongExpectation = new(
            Guid.NewGuid(),
            "Tisch 12",
            null,
            1,
            [new OrderLineBody(context.World.BratwurstItemId, 2, null, null)]);

        using HttpResponseMessage response = await context.PostOrderAsync(wrongExpectation);
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(body.RootElement.GetProperty("totalCents").GetInt32(), Is.EqualTo(700));
            Assert.That(body.RootElement.GetProperty("expectedTotalCents").GetInt32(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GetMine_OrderPlaced_ListsTheOrdersOfThePersonBehindTheDevice()
    {
        using (HttpResponseMessage created = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid(), 700)))
        {
            Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        }

        using HttpResponseMessage response = await context.SendAsync(HttpMethod.Get, "/api/orders/mine");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(body.RootElement.GetProperty("orders").GetArrayLength(), Is.EqualTo(1));
    }

    [Test]
    public async Task PostResolve_UnknownTicket_MovesItAndRewritesTheProjectedOrderStatus()
    {
        Guid orderId;
        Guid ticketId;

        using (HttpResponseMessage created = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid(), 700)))
        {
            JsonDocument body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
            orderId = body.RootElement.GetProperty("orderId").GetGuid();
            ticketId = body.RootElement.GetProperty("tickets")[0].GetProperty("ticketId").GetGuid();
        }

        await using (GastronomyAppDbContext database = context.Factory.CreateContext())
        {
            LocationTicket ticket = await database.LocationTickets.FirstAsync(
                candidate => candidate.Id == ticketId);
            ticket.Status = LocationTicketStatus.Unknown;
            await database.SaveChangesAsync();
        }

        using HttpResponseMessage response = await context.SendAsync(
            HttpMethod.Post,
            $"/api/orders/{orderId}/tickets/{ticketId}/resolve",
            new SlipOnThePileBody(true));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await using GastronomyAppDbContext verification = context.Factory.CreateContext();
        LocationTicket resolved = await verification.LocationTickets.FirstAsync(
            candidate => candidate.Id == ticketId);
        Order order = await verification.Orders.FirstAsync(candidate => candidate.Id == orderId);

        Assert.Multiple(() =>
        {
            Assert.That(resolved.Status, Is.EqualTo(LocationTicketStatus.Printed));
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Printed));
        });
    }

    [Test]
    public async Task PostResolve_TicketNoLongerUnknown_IsRefused()
    {
        Guid orderId;
        Guid ticketId;

        using (HttpResponseMessage created = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid(), 700)))
        {
            JsonDocument body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
            orderId = body.RootElement.GetProperty("orderId").GetGuid();
            ticketId = body.RootElement.GetProperty("tickets")[0].GetProperty("ticketId").GetGuid();
        }

        using HttpResponseMessage response = await context.SendAsync(
            HttpMethod.Post,
            $"/api/orders/{orderId}/tickets/{ticketId}/resolve",
            new SlipOnThePileBody(true));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task GetPrinterStatus_SeededLocations_ReportsEveryLocation()
    {
        using HttpResponseMessage response = await context.SendAsync(HttpMethod.Get, "/api/printers/status");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body.RootElement.GetProperty("locations").GetArrayLength(), Is.EqualTo(2));
        });
    }
}

public sealed record SlipOnThePileBody(bool SlipIsOnThePile);
