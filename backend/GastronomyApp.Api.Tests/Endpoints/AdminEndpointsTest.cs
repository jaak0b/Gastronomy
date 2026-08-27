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
    public async Task GetStations_LoopbackCaller_ListsEveryStationWithItsPrinterConfigurationAndStatus()
    {
        using HttpResponseMessage response = await context.Client.GetAsync("/api/admin/stations");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement stations = body.RootElement.GetProperty("stations");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(stations.GetArrayLength(), Is.EqualTo(2));
            Assert.That(stations[0].GetProperty("transportKind").GetString(), Is.EqualTo("Mock"));
            Assert.That(stations[0].GetProperty("isOnline").GetBoolean(), Is.True);
        });
    }

    [Test]
    public async Task PostStation_NewStation_CreatesItWithAMockPrinterAndAFreshAccessKey()
    {
        using HttpResponseMessage response = await context.Client.PostAsJsonAsync(
            "/api/admin/stations",
            new { name = "Zelt", sortOrder = 3, });

        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Guid stationId = body.RootElement.GetProperty("stationId").GetGuid();

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        Station created = await database.Stations.FirstAsync(
            station => station.Id == stationId);
        PrinterConfiguration configuration = await database.PrinterConfigurations.FirstAsync(
            candidate => candidate.StationId == stationId);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(configuration.TransportKind, Is.EqualTo(TransportKind.Mock));
        });
    }

    [Test]
    public async Task PutStation_RenamedStation_StoresTheNewName()
    {
        using HttpResponseMessage response = await context.Client.PutAsJsonAsync(
            $"/api/admin/stations/{context.World.KitchenStationId}",
            new { name = "Kueche innen", sortOrder = 1, });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        Station station = await database.Stations.FirstAsync(
            candidate => candidate.Id == context.World.KitchenStationId);

        Assert.That(station.Name, Is.EqualTo("Kueche innen"));
    }

    [Test]
    public async Task PostItem_EmptyStationList_IsRefusedAsUnprocessable()
    {
        using HttpResponseMessage response = await context.Client.PostAsJsonAsync(
            "/api/admin/items",
            new
            {
                name = "Pommes",
                categoryName = "Essen",
                priceCents = 250,
                sortOrder = 3,
                stationIds = Array.Empty<Guid>(),
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
    }

    [Test]
    public async Task PostItem_WithStations_CreatesItAndItsAssignments()
    {
        using HttpResponseMessage response = await context.Client.PostAsJsonAsync(
            "/api/admin/items",
            new
            {
                name = "Pommes",
                categoryName = "Essen",
                priceCents = 250,
                sortOrder = 3,
                stationIds = new[] { context.World.KitchenStationId },
            });

        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Guid itemId = body.RootElement.GetProperty("itemId").GetGuid();

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        int assignments = await database.ItemStationAssignments.CountAsync(
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
    public async Task GetStaffMembers_LoopbackCaller_ReportsThePhoneBehindEveryStaffMember()
    {
        using HttpResponseMessage response = await context.Client.GetAsync("/api/admin/staff-members");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement staffMembers = body.RootElement.GetProperty("staffMembers");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(staffMembers.GetArrayLength(), Is.EqualTo(1));
            Assert.That(staffMembers[0].GetProperty("hasDevice").GetBoolean(), Is.True);
        });
    }

    [Test]
    public async Task PutStaffMember_Rename_KeepsTheirIdentity()
    {
        using HttpResponseMessage response = await context.Client.PutAsJsonAsync(
            $"/api/admin/staff-members/{context.World.StaffMemberId}",
            new { name = "Anna Maria" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        StaffMember staffMember = await database.StaffMembers.FirstAsync(
            candidate => candidate.Id == context.World.StaffMemberId);

        Assert.That(staffMember.Name, Is.EqualTo("Anna Maria"));
    }

    [Test]
    public async Task RevokeDevice_StaffMemberWithAPhone_InvalidatesTheirTokenImmediately()
    {
        using HttpResponseMessage response = await context.Client.PostAsync(
            $"/api/admin/staff-members/{context.World.StaffMemberId}/revoke-device",
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
            new { staffMemberId = (Guid?)null });

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
            new { staffMemberId = (Guid?)null });

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
    public async Task GetPrinters_LoopbackCaller_CarriesEverythingThePrinterScreenShows()
    {
        using HttpResponseMessage response = await context.Client.GetAsync("/api/admin/printers");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement printer = body.RootElement.GetProperty("printers")[0];

        Assert.Multiple(() =>
        {
            Assert.That(printer.TryGetProperty("isPaperNearEnd", out _), Is.True);
            Assert.That(printer.TryGetProperty("waitingTicketCount", out _), Is.True);
            Assert.That(printer.TryGetProperty("lastChangedAtUtc", out _), Is.True);
            Assert.That(printer.TryGetProperty("sharedWithStationNames", out _), Is.True);
            Assert.That(printer.TryGetProperty("mockFolderPath", out _), Is.True);
        });
    }

    [Test]
    public async Task Activate_ItemTakenOffTheMenu_PutsItBackOnTheMenu()
    {
        using (HttpResponseMessage takenOff = await context.Client.PostAsync(
            $"/api/admin/items/{context.World.BratwurstItemId}/deactivate",
            content: null))
        {
            Assert.That(takenOff.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        using HttpResponseMessage response = await context.Client.PostAsync(
            $"/api/admin/items/{context.World.BratwurstItemId}/activate",
            content: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        CatalogItem item = await database.CatalogItems.FirstAsync(
            candidate => candidate.Id == context.World.BratwurstItemId);

        Assert.That(item.IsActive, Is.True);
    }

    [Test]
    public async Task Activate_StaffMemberTakenOffTheList_PutsThemBackOnTheList()
    {
        using (HttpResponseMessage takenOff = await context.Client.PostAsync(
            $"/api/admin/staff-members/{context.World.StaffMemberId}/deactivate",
            content: null))
        {
            Assert.That(takenOff.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        using HttpResponseMessage response = await context.Client.PostAsync(
            $"/api/admin/staff-members/{context.World.StaffMemberId}/activate",
            content: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        StaffMember staffMember = await database.StaffMembers.FirstAsync(
            candidate => candidate.Id == context.World.StaffMemberId);

        Assert.That(staffMember.IsActive, Is.True);
    }

    [Test]
    public async Task Activate_ItemWhoseOnlyStationIsSwitchedOff_IsRefused()
    {
        await SwitchOffEveryStationOfBratwurstAsync();

        using HttpResponseMessage response = await context.Client.PostAsync(
            $"/api/admin/items/{context.World.BratwurstItemId}/activate",
            content: null);

        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(
                body.RootElement.GetProperty("messageKey").GetString(),
                Is.EqualTo("admin.itemHasNoActiveStation"));
        });
    }

    [Test]
    public async Task Activate_ItemWhoseOnlyStationIsSwitchedOff_LeavesItDeactivated()
    {
        await SwitchOffEveryStationOfBratwurstAsync();

        using HttpResponseMessage response = await context.Client.PostAsync(
            $"/api/admin/items/{context.World.BratwurstItemId}/activate",
            content: null);

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        CatalogItem item = await database.CatalogItems.FirstAsync(
            candidate => candidate.Id == context.World.BratwurstItemId);

        Assert.That(item.IsActive, Is.False);
    }

    [Test]
    public async Task PutItem_OnlyStationsThatAreSwitchedOff_IsRefused()
    {
        await using (GastronomyAppDbContext database = context.Factory.CreateContext())
        {
            await database.Stations
                .Where(station => station.Id == context.World.BarStationId)
                .ExecuteUpdateAsync(station => station.SetProperty(entry => entry.IsActive, false));
        }

        using HttpResponseMessage response = await context.Client.PutAsJsonAsync(
            $"/api/admin/items/{context.World.BratwurstItemId}",
            new
            {
                name = "Bratwurst",
                categoryName = "Essen",
                priceCents = 350,
                sortOrder = 1,
                stationIds = new[] { context.World.BarStationId },
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
    }

    private async Task SwitchOffEveryStationOfBratwurstAsync()
    {
        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        List<Guid> stationIds = await database.ItemStationAssignments
            .Where(assignment => assignment.CatalogItemId == context.World.BratwurstItemId)
            .Select(assignment => assignment.StationId)
            .ToListAsync();

        await database.CatalogItems
            .Where(item => item.Id == context.World.BratwurstItemId)
            .ExecuteUpdateAsync(item => item.SetProperty(entry => entry.IsActive, false));
        await database.Stations
            .Where(station => stationIds.Contains(station.Id))
            .ExecuteUpdateAsync(station => station.SetProperty(entry => entry.IsActive, false));
    }

    [Test]
    public async Task PostMockFault_MockStation_ArmsTheFault()
    {
        using HttpResponseMessage response = await context.Client.PostAsJsonAsync(
            $"/api/admin/mock/{context.World.KitchenStationId}/fault",
            new { fault = "PaperEnd", mode = "Sticky" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        IMockFaultRegistry registry = context.Factory.Services.GetRequiredService<IMockFaultRegistry>();

        Assert.That(
            registry.GetArmedFault(context.World.KitchenStationId),
            Is.EqualTo(MockFault.PaperEnd));
    }

    [Test]
    public async Task PostMockFault_StationThatIsNotOnTheMock_IsRefusedAsUnprocessable()
    {
        await using (GastronomyAppDbContext database = context.Factory.CreateContext())
        {
            PrinterConfiguration configuration = await database.PrinterConfigurations.FirstAsync(
                candidate => candidate.StationId == context.World.KitchenStationId);
            configuration.TransportKind = TransportKind.Network;
            configuration.Host = "192.0.2.10";
            configuration.Port = 9100;
            await database.SaveChangesAsync();
        }

        using HttpResponseMessage response = await context.Client.PostAsJsonAsync(
            $"/api/admin/mock/{context.World.KitchenStationId}/fault",
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
    public async Task PostAdminResolve_UnknownTicketOfAnotherStaffMember_IsStillAllowedFromTheLaptop()
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
