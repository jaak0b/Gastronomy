using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class LocationDeactivationTest
{
    private OrderTestContext context = null!;

    [SetUp]
    public async Task SetUp()
    {
        context = await new OrderTestContext.Builder().StartAsync(withRunningPrinters: false);
    }

    [TearDown]
    public async Task TearDown()
    {
        await context.DisposeAsync();
    }

    [Test]
    public async Task Deactivate_FreshStationThatOnlyEverTestPrinted_ReportsNoOpenSlipsAndSwitchesOff()
    {
        Guid locationId = await CreateStationAsync();

        using (HttpResponseMessage testPrint = await context.Client.PostAsync(
            $"/api/admin/printers/{locationId}/test-print",
            content: null))
        {
            Assert.That(
                testPrint.StatusCode,
                Is.EqualTo(HttpStatusCode.Accepted),
                "A test print on a fresh station must be accepted.");
        }

        using HttpResponseMessage response = await context.Client.PostAsync(
            $"/api/admin/locations/{locationId}/deactivate",
            content: null);

        string body = await response.Content.ReadAsStringAsync();

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.OK),
            $"A station with no orders must switch off. Body: {body}");

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        ProductionLocation location = await database.ProductionLocations.FirstAsync(
            candidate => candidate.Id == locationId);

        Assert.That(location.IsActive, Is.False);
    }

    [Test]
    public async Task Deactivate_StationWithAGenuinelyOpenTicket_IsStillRefused()
    {
        using (HttpResponseMessage placed = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid(), 700)))
        {
            Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        }

        using HttpResponseMessage response = await context.Client.PostAsync(
            $"/api/admin/locations/{context.World.KitchenLocationId}/deactivate",
            content: null);

        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(
                body.RootElement.GetProperty("messageKey").GetString(),
                Is.EqualTo("admin.locationHasOpenTickets"));
            Assert.That(body.RootElement.GetProperty("parameters").GetProperty("count").GetString(), Is.EqualTo("1"));
        });
    }

    [Test]
    public async Task Deactivate_StationWhoseItemsWouldLoseTheirOnlyStation_IsRefusedForThatReason()
    {
        using HttpResponseMessage response = await context.Client.PostAsync(
            $"/api/admin/locations/{context.World.BarLocationId}/deactivate",
            content: null);

        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(
                body.RootElement.GetProperty("messageKey").GetString(),
                Is.EqualTo("admin.itemsWouldHaveNoStation"),
                "A station losing its items is a different refusal from one holding open slips.");
        });
    }

    [Test]
    public async Task Deactivate_StationWhoseTicketsAreAllSettled_SwitchesOff()
    {
        using (HttpResponseMessage placed = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid(), 700)))
        {
            Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        }

        await using (GastronomyAppDbContext database = context.Factory.CreateContext())
        {
            List<LocationTicket> tickets = await database.LocationTickets.ToListAsync();

            foreach (LocationTicket ticket in tickets)
            {
                ticket.Status = LocationTicketStatus.HandledOnPaper;
            }

            await database.SaveChangesAsync();
        }

        using (HttpResponseMessage assigned = await context.Client.PutAsJsonAsync(
            $"/api/admin/items/{context.World.BratwurstItemId}",
            new
            {
                name = "Bratwurst mit Brot",
                categoryName = "Essen",
                priceCents = 350,
                sortOrder = 1,
                locationIds = new[] { context.World.BarLocationId },
            }))
        {
            Assert.That(assigned.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        using HttpResponseMessage response = await context.Client.PostAsync(
            $"/api/admin/locations/{context.World.KitchenLocationId}/deactivate",
            content: null);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.OK),
            $"Settled slips must not block a station. Body: {await response.Content.ReadAsStringAsync()}");
    }

    [Test]
    public async Task Activate_StationThatWasSwitchedOff_SwitchesItBackOn()
    {
        Guid locationId = await CreateStationAsync();

        using (HttpResponseMessage switchedOff = await context.Client.PostAsync(
            $"/api/admin/locations/{locationId}/deactivate",
            content: null))
        {
            Assert.That(switchedOff.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        using HttpResponseMessage response = await context.Client.PostAsync(
            $"/api/admin/locations/{locationId}/activate",
            content: null);

        string body = await response.Content.ReadAsStringAsync();

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.OK),
            $"A station that was switched off must be switchable back on. Body: {body}");

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        ProductionLocation location = await database.ProductionLocations.FirstAsync(
            candidate => candidate.Id == locationId);

        Assert.That(location.IsActive, Is.True);
    }

    private async Task<Guid> CreateStationAsync()
    {
        using HttpResponseMessage response = await context.Client.PostAsJsonAsync(
            "/api/admin/locations",
            new { name = "Zelt", sortOrder = 3 });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return body.RootElement.GetProperty("locationId").GetGuid();
    }
}
