using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure;
using GastronomyApp.Infrastructure.Printing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class AdminEndpointsTest
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
    public async Task GetLocations_LoopbackCaller_ListsEveryLocationWithItsPrinterConfigurationAndStatus()
    {
        using HttpResponseMessage response = await context.Client.GetAsync("/api/admin/locations");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement locations = body.RootElement.GetProperty("locations");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(locations.GetArrayLength(), Is.EqualTo(2));
            Assert.That(locations[0].GetProperty("transportKind").GetString(), Is.EqualTo("Mock"));
            Assert.That(locations[0].GetProperty("isOnline").GetBoolean(), Is.True);
        });
    }

    [Test]
    public async Task PostLocation_NewStation_CreatesItWithAMockPrinterAndAFreshAccessKey()
    {
        using HttpResponseMessage response = await context.Client.PostAsJsonAsync(
            "/api/admin/locations",
            new { name = "Zelt", sortOrder = 3, slipLanguage = (string?)null });

        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Guid locationId = body.RootElement.GetProperty("locationId").GetGuid();

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        ProductionLocation created = await database.ProductionLocations.FirstAsync(
            location => location.Id == locationId);
        PrinterConfiguration configuration = await database.PrinterConfigurations.FirstAsync(
            candidate => candidate.ProductionLocationId == locationId);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(created.SlipLanguage, Is.EqualTo("de"));
            Assert.That(created.StationAccessKey, Has.Length.EqualTo(32));
            Assert.That(configuration.TransportKind, Is.EqualTo(TransportKind.Mock));
        });
    }

    [Test]
    public async Task PutLocation_RenamedStation_StoresTheNewName()
    {
        using HttpResponseMessage response = await context.Client.PutAsJsonAsync(
            $"/api/admin/locations/{context.World.KitchenLocationId}",
            new { name = "Kueche innen", sortOrder = 1, slipLanguage = "de" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        ProductionLocation location = await database.ProductionLocations.FirstAsync(
            candidate => candidate.Id == context.World.KitchenLocationId);

        Assert.That(location.Name, Is.EqualTo("Kueche innen"));
    }

    [Test]
    public async Task RegenerateAccessKey_ExistingStation_ReplacesTheBreakGlassLink()
    {
        string oldKey = context.World.KitchenLocationId.ToString("N");

        using HttpResponseMessage response = await context.Client.PostAsync(
            $"/api/admin/locations/{context.World.KitchenLocationId}/regenerate-access-key",
            content: null);

        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body.RootElement.GetProperty("accessKey").GetString(), Is.Not.EqualTo(oldKey));
            Assert.That(body.RootElement.GetProperty("breakGlassUrl").GetString(), Does.Contain("/station/"));
        });
    }

    [Test]
    public async Task GetStationCard_ExistingStation_NamesTheStationAndItsBreakGlassLink()
    {
        using HttpResponseMessage response = await context.Client.GetAsync(
            $"/api/admin/locations/{context.World.KitchenLocationId}/station-card");

        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body.RootElement.GetProperty("stationName").GetString(), Is.EqualTo("Kueche"));
            Assert.That(body.RootElement.GetProperty("breakGlassUrl").GetString(), Does.Contain("/station/"));
        });
    }

    [Test]
    public async Task PostItem_EmptyLocationList_IsRefusedAsUnprocessable()
    {
        using HttpResponseMessage response = await context.Client.PostAsJsonAsync(
            "/api/admin/items",
            new
            {
                name = "Pommes",
                categoryName = "Essen",
                priceCents = 250,
                sortOrder = 3,
                locationIds = Array.Empty<Guid>(),
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
    }

    [Test]
    public async Task PostItem_WithLocations_CreatesItAndItsAssignments()
    {
        using HttpResponseMessage response = await context.Client.PostAsJsonAsync(
            "/api/admin/items",
            new
            {
                name = "Pommes",
                categoryName = "Essen",
                priceCents = 250,
                sortOrder = 3,
                locationIds = new[] { context.World.KitchenLocationId },
            });

        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Guid itemId = body.RootElement.GetProperty("itemId").GetGuid();

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        int assignments = await database.ItemLocationAssignments.CountAsync(
            assignment => assignment.CatalogItemId == itemId);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(assignments, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task PostAvailability_SoldOutToggle_IsNeverRefused()
    {
        using HttpResponseMessage response = await context.Client.PostAsJsonAsync(
            $"/api/admin/items/{context.World.BratwurstItemId}/availability",
            new { isAvailable = false });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        CatalogItem item = await database.CatalogItems.FirstAsync(
            candidate => candidate.Id == context.World.BratwurstItemId);

        Assert.That(item.IsAvailable, Is.False);
    }

    [Test]
    public async Task GetServerPeople_LoopbackCaller_ReportsThePhoneBehindEveryPerson()
    {
        using HttpResponseMessage response = await context.Client.GetAsync("/api/admin/server-people");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement people = body.RootElement.GetProperty("people");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(people.GetArrayLength(), Is.EqualTo(1));
            Assert.That(people[0].GetProperty("hasDevice").GetBoolean(), Is.True);
        });
    }

    [Test]
    public async Task PutServerPerson_Rename_KeepsTheirIdentity()
    {
        using HttpResponseMessage response = await context.Client.PutAsJsonAsync(
            $"/api/admin/server-people/{context.World.ServerPersonId}",
            new { name = "Anna Maria" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        ServerPerson person = await database.ServerPeople.FirstAsync(
            candidate => candidate.Id == context.World.ServerPersonId);

        Assert.That(person.Name, Is.EqualTo("Anna Maria"));
    }

    [Test]
    public async Task RevokeDevice_PersonWithAPhone_InvalidatesTheirTokenImmediately()
    {
        using HttpResponseMessage response = await context.Client.PostAsync(
            $"/api/admin/server-people/{context.World.ServerPersonId}/revoke-device",
            content: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using HttpResponseMessage afterRevocation = await context.SendAsync(HttpMethod.Get, "/api/session");

        Assert.That(afterRevocation.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task PostEnrolmentInvitation_ForSomebodyNew_ReturnsTheCodesAndTheirExpiry()
    {
        using HttpResponseMessage response = await context.Client.PostAsJsonAsync(
            "/api/admin/enrolment/invitations",
            new { serverPersonId = (Guid?)null });

        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(body.RootElement.GetProperty("qrUrl").GetString(), Does.Contain("/j/"));
            Assert.That(body.RootElement.GetProperty("sixDigitCode").GetString(), Has.Length.EqualTo(6));
        });
    }

    [Test]
    public async Task PostEnrolmentInvitation_AnyBind_CarriesAnAddressAPhoneCanOpen()
    {
        using HttpResponseMessage response = await context.Client.PostAsJsonAsync(
            "/api/admin/enrolment/invitations",
            new { serverPersonId = (Guid?)null });

        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string qrUrl = body.RootElement.GetProperty("qrUrl").GetString()!;

        Assert.Multiple(() =>
        {
            Assert.That(qrUrl, Does.Not.Contain("0.0.0.0"), "A bind wildcard is not an address a phone can open.");
            Assert.That(qrUrl, Does.Not.Contain(":0/"), "Port zero is not an address a phone can open.");
            Assert.That(Uri.TryCreate(qrUrl, UriKind.Absolute, out Uri? _), Is.True);
            Assert.That(body.RootElement.TryGetProperty("availableAddresses", out JsonElement _), Is.True);
        });
    }

    [Test]
    public async Task GetStationCard_AnyBind_CarriesAnAddressAPhoneCanOpen()
    {
        using HttpResponseMessage response = await context.Client.GetAsync(
            $"/api/admin/locations/{context.World.KitchenLocationId}/station-card");

        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string breakGlassUrl = body.RootElement.GetProperty("breakGlassUrl").GetString()!;

        Assert.Multiple(() =>
        {
            Assert.That(breakGlassUrl, Does.Not.Contain("0.0.0.0"));
            Assert.That(breakGlassUrl, Does.Not.Contain(":0/"));
        });
    }

    [Test]
    public async Task GetPrinters_LoopbackCaller_ReportsConfigurationAndLiveStatus()
    {
        using HttpResponseMessage response = await context.Client.GetAsync("/api/admin/printers");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body.RootElement.GetProperty("printers").GetArrayLength(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task PostMockFault_MockStation_ArmsTheFault()
    {
        using HttpResponseMessage response = await context.Client.PostAsJsonAsync(
            $"/api/admin/mock/{context.World.KitchenLocationId}/fault",
            new { fault = "PaperEnd", mode = "Sticky" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        IMockFaultRegistry registry = context.Factory.Services.GetRequiredService<IMockFaultRegistry>();

        Assert.That(
            registry.GetArmedFault(context.World.KitchenLocationId),
            Is.EqualTo(MockFault.PaperEnd));
    }

    [Test]
    public async Task PostMockFault_StationThatIsNotOnTheMock_IsRefusedAsUnprocessable()
    {
        await using (GastronomyAppDbContext database = context.Factory.CreateContext())
        {
            PrinterConfiguration configuration = await database.PrinterConfigurations.FirstAsync(
                candidate => candidate.ProductionLocationId == context.World.KitchenLocationId);
            configuration.TransportKind = TransportKind.Network;
            configuration.Host = "192.0.2.10";
            configuration.Port = 9100;
            await database.SaveChangesAsync();
        }

        using HttpResponseMessage response = await context.Client.PostAsJsonAsync(
            $"/api/admin/mock/{context.World.KitchenLocationId}/fault",
            new { fault = "PaperEnd", mode = "Sticky" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
    }

    [Test]
    public async Task GetAdminOrders_AfterAnOrderWasPlaced_ListsIt()
    {
        using (HttpResponseMessage created = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid(), 700)))
        {
            Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        }

        using HttpResponseMessage response = await context.Client.GetAsync("/api/admin/orders");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(body.RootElement.GetProperty("orders").GetArrayLength(), Is.EqualTo(1));
    }

    [Test]
    public async Task PostAdminResolve_UnknownTicketOfAnotherPerson_IsStillAllowedFromTheLaptop()
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

        using HttpResponseMessage response = await context.Client.PostAsJsonAsync(
            $"/api/admin/orders/{orderId}/tickets/{ticketId}/resolve",
            new { slipIsOnThePile = true });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
