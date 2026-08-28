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
        using HttpResponseMessage response = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid()));
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(body.RootElement.GetProperty("globalOrderNumber").GetInt32(), Is.EqualTo(1));
            Assert.That(body.RootElement.GetProperty("totalCents").GetInt32(), Is.EqualTo(700));
            Assert.That(body.RootElement.GetProperty("totalCents").GetInt32(), Is.EqualTo(700));
            Assert.That(body.RootElement.GetProperty("stationOrders").GetArrayLength(), Is.EqualTo(1));
        });

        JsonElement ticket = body.RootElement.GetProperty("stationOrders")[0];

        Assert.Multiple(() =>
        {
            Assert.That(ticket.GetProperty("stationId").GetGuid(), Is.EqualTo(context.World.KitchenStationId));
            Assert.That(ticket.GetProperty("stationName").GetString(), Is.EqualTo("Kueche"));
            Assert.That(ticket.GetProperty("stationOrderNumber").GetInt32(), Is.EqualTo(1));
            Assert.That(ticket.GetProperty("itemIds").GetArrayLength(), Is.EqualTo(1));
        });

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        Order stored = await database.Orders.SingleAsync();

        Assert.That(stored.GlobalOrderNumber, Is.EqualTo(1));
    }

    [Test]
    public async Task PostOrder_TwoStations_CreatesOnePrintJobPerStationOrder()
    {
        OrderBody twoStations = new(
            Guid.NewGuid(),
            "Tisch 12",
            null,
            [
                new OrderItemBody(context.World.BratwurstItemId, 2, 350, null, null),
                new OrderItemBody(context.World.BeerItemId, 1, 350, null, null),
            ]);

        using HttpResponseMessage response = await context.PostOrderAsync(twoStations);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        int printJobCount = await database.PrintJobs.CountAsync(job => job.CopyNumber == 0);

        Assert.That(printJobCount, Is.EqualTo(2));
    }

    [Test]
    public async Task PostOrder_UnknownItemId_IsRefusedAsUnprocessable()
    {
        OrderBody unknownItem = new(
            Guid.NewGuid(),
            "Tisch 12",
            null,
                        [new OrderItemBody(Guid.NewGuid(), 1, 350, null, null)]);

        using HttpResponseMessage response = await context.PostOrderAsync(unknownItem);
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(body.RootElement.GetProperty("code").GetString(), Is.EqualTo("UnprocessableEntity"));
        });
    }

    [Test]
    public async Task PostOrder_NoItems_IsRefusedAsAValidationFailure()
    {
        OrderBody empty = new(Guid.NewGuid(), "Tisch 12", null, []);

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

        using HttpResponseMessage response = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid()));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task PostOrder_ItemPrices_AreStoredAsThePhoneSentThem()
    {
        OrderBody body = new(
            Guid.NewGuid(),
            "Tisch 12",
            null,
            [new OrderItemBody(context.World.BratwurstItemId, 2, 399, null, null)]);

        using HttpResponseMessage response = await context.PostOrderAsync(body);
        JsonDocument placed = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        OrderItem stored = await database.OrderItems.SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(stored.UnitPriceCents, Is.EqualTo(399));
            Assert.That(placed.RootElement.GetProperty("totalCents").GetInt32(), Is.EqualTo(798));
        });
    }

    [Test]
    public async Task GetMine_OrderPlaced_ListsTheOrdersOfTheStaffMemberBehindTheDevice()
    {
        using (HttpResponseMessage created = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid())))
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

        using (HttpResponseMessage created = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid())))
        {
            JsonDocument body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
            orderId = body.RootElement.GetProperty("orderId").GetGuid();
            ticketId = body.RootElement.GetProperty("stationOrders")[0].GetProperty("stationOrderId").GetGuid();
        }

        await using (GastronomyAppDbContext database = context.Factory.CreateContext())
        {
            PrintJob job = await database.PrintJobs.FirstAsync(
                candidate => candidate.StationOrderId == ticketId);
            job.Status = PrintJobStatus.Unknown;
            await database.SaveChangesAsync();
        }

        using HttpResponseMessage response = await context.SendAsync(
            HttpMethod.Post,
            $"/api/orders/{orderId}/station-orders/{ticketId}/resolve",
            new SlipOnThePileBody(true));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await using GastronomyAppDbContext verification = context.Factory.CreateContext();
        PrintJob resolved = await verification.PrintJobs.FirstAsync(
            candidate => candidate.StationOrderId == ticketId);

        Assert.That(resolved.Status, Is.EqualTo(PrintJobStatus.Printed));
    }

    [Test]
    public async Task PostResolve_TicketNoLongerUnknown_IsRefused()
    {
        Guid orderId;
        Guid ticketId;

        using (HttpResponseMessage created = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid())))
        {
            JsonDocument body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
            orderId = body.RootElement.GetProperty("orderId").GetGuid();
            ticketId = body.RootElement.GetProperty("stationOrders")[0].GetProperty("stationOrderId").GetGuid();
        }

        using HttpResponseMessage response = await context.SendAsync(
            HttpMethod.Post,
            $"/api/orders/{orderId}/station-orders/{ticketId}/resolve",
            new SlipOnThePileBody(true));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task GetPrinterStatus_StationWithNoPrinter_IsOnlineBecauseNothingAboutItHasFailed()
    {
        await using (GastronomyAppDbContext database = context.Factory.CreateContext())
        {
            Station kitchen = await database.Stations.FirstAsync(
                station => station.Id == context.World.KitchenStationId);
            kitchen.PrinterId = null;
            await database.SaveChangesAsync();
        }

        using HttpResponseMessage response = await context.SendAsync(HttpMethod.Get, "/api/printers/status");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        JsonElement kitchenStatus = body.RootElement
            .GetProperty("stations")
            .EnumerateArray()
            .Single(station => station.GetProperty("stationId").GetGuid() == context.World.KitchenStationId);

        Assert.Multiple(() =>
        {
            Assert.That(kitchenStatus.GetProperty("isOnline").GetBoolean(), Is.True);
            Assert.That(kitchenStatus.GetProperty("isFaulty").GetBoolean(), Is.False);
        });
    }

    [Test]
    public async Task GetPrinterStatus_SeededStations_ReportsEveryStation()
    {
        using HttpResponseMessage response = await context.SendAsync(HttpMethod.Get, "/api/printers/status");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body.RootElement.GetProperty("stations").GetArrayLength(), Is.EqualTo(2));
        });
    }
}

public sealed record SlipOnThePileBody(bool SlipIsOnThePile);
