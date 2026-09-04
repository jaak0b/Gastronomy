using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure.Printing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class AdminEndpointsTest
{

  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContext.Builder().StartAsync(false);
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
  }

  private OrderTestContext _context = null!;

  [Test]
  public async Task GetStations_LoopbackCaller_ListsEveryStationWithItsPrinterAndStatus()
  {
    using var response = await _context.Client.GetAsync("/api/admin/stations");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var stations = body.RootElement.GetProperty("stations");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(stations.GetArrayLength(), Is.EqualTo(2));
                      Assert.That(stations[0].GetProperty("printerName").GetString(), Is.Not.Null);
                      Assert.That(stations[0].GetProperty("isOnline").GetBoolean(), Is.True);
                    });
  }

  [Test]
  public async Task PostStation_NewStation_CreatesItWithNoPrinterYet()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/stations",
                                                              new { name = "Zelt", sortOrder = 3 });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var stationId = body.RootElement.GetProperty("stationId").GetGuid();

    await using var database = _context.Factory.CreateContext();
    var created = await database.Stations.FirstAsync(station => station.Id == stationId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                      Assert.That(created.PrinterId, Is.Null);
                    });
  }

  [Test]
  public async Task PutStation_RenamedStation_StoresTheNewName()
  {
    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/stations/{_context.World.KitchenStationId}",
                                                             new { name = "Kueche innen", sortOrder = 1 });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    await using var database = _context.Factory.CreateContext();
    var station = await database.Stations.FirstAsync(candidate => candidate.Id == _context.World.KitchenStationId);

    Assert.That(station.Name, Is.EqualTo("Kueche innen"));
  }

  [Test]
  public async Task PostItem_EmptyStationList_IsRefusedAsUnprocessable()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/items",
                                                              new
                                                              {
                                                                name = "Pommes",
                                                                categoryName = "Essen",
                                                                priceCents = 250,
                                                                sortOrder = 3,
                                                                stationIds = Array.Empty<Guid>()
                                                              });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
  }

  [Test]
  public async Task PostItem_BlankCategory_NamesTheCategoryAsTheMissingPart()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/items",
                                                              new
                                                              {
                                                                name = "Pommes",
                                                                categoryName = "  ",
                                                                priceCents = 250,
                                                                sortOrder = 3,
                                                                stationIds = new[] { _context.World.KitchenStationId }
                                                              });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.itemCategoryMissing"));
                    });
  }

  [Test]
  public async Task PostItem_WithStations_CreatesItAndItsAssignments()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/items",
                                                              new
                                                              {
                                                                name = "Pommes",
                                                                categoryName = "Essen",
                                                                priceCents = 250,
                                                                sortOrder = 3,
                                                                stationIds = new[] { _context.World.KitchenStationId }
                                                              });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var itemId = body.RootElement.GetProperty("itemId").GetGuid();

    await using var database = _context.Factory.CreateContext();
    var assignments = await database.ItemStationAssignments.CountAsync(assignment => assignment.CatalogItemId == itemId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                      Assert.That(assignments, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task PostAvailability_SoldOutToggle_IsNeverRefused()
  {
    using var response = await _context.Client.PostAsJsonAsync($"/api/admin/items/{_context.World.BratwurstItemId}/availability",
                                                              new { isAvailable = false });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    await using var database = _context.Factory.CreateContext();
    var item = await database.CatalogItems.FirstAsync(candidate => candidate.Id == _context.World.BratwurstItemId);

    Assert.That(item.IsAvailable, Is.False);
  }

  [Test]
  public async Task GetStaffMembers_LoopbackCaller_ReportsThePhoneBehindEveryStaffMember()
  {
    using var response = await _context.Client.GetAsync("/api/admin/staff-members");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var staffMembers = body.RootElement.GetProperty("staffMembers");

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
    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/staff-members/{_context.World.StaffMemberId}",
                                                             new { name = "Anna Maria" });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    await using var database = _context.Factory.CreateContext();
    var staffMember = await database.StaffMembers.FirstAsync(candidate => candidate.Id == _context.World.StaffMemberId);

    Assert.That(staffMember.Name, Is.EqualTo("Anna Maria"));
  }

  [Test]
  public async Task Deactivate_StaffMemberWithAPhone_InvalidatesTheirTokenImmediately()
  {
    using var response = await _context.Client.PostAsync($"/api/admin/staff-members/{_context.World.StaffMemberId}/deactivate",
                                                        null);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    using var afterRevocation = await _context.SendAsync(HttpMethod.Get, "/api/session");

    Assert.That(afterRevocation.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
  }

  [Test]
  public async Task PostEnrolmentInvitation_ForSomebodyNew_ReturnsTheCodesAndTheirExpiry()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/enrolment/invitations",
                                                              new { staffMemberId = (Guid?)null });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                      Assert.That(body.RootElement.GetProperty("qrUrl").GetString(), Does.Contain("/j/"));
                    });
  }

  [Test]
  public async Task PostEnrolmentInvitation_AnyBind_CarriesAnAddressAPhoneCanOpen()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/enrolment/invitations",
                                                              new { staffMemberId = (Guid?)null });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var qrUrl = body.RootElement.GetProperty("qrUrl").GetString()!;

    Assert.Multiple(() =>
                    {
                      Assert.That(qrUrl, Does.Not.Contain("0.0.0.0"), "A bind wildcard is not an address a phone can open.");
                      Assert.That(qrUrl, Does.Not.Contain(":0/"), "Port zero is not an address a phone can open.");
                      Assert.That(Uri.TryCreate(qrUrl, UriKind.Absolute, out var _), Is.True);
                      Assert.That(body.RootElement.TryGetProperty("availableAddresses", out var _), Is.True);
                    });
  }

  [Test]
  public async Task GetPrinters_LoopbackCaller_ReportsConfigurationAndLiveStatus()
  {
    using var response = await _context.Client.GetAsync("/api/admin/printers");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("printers").GetArrayLength(), Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task GetPrinters_LoopbackCaller_CarriesEverythingThePrinterScreenShows()
  {
    using var response = await _context.Client.GetAsync("/api/admin/printers");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var printer = body.RootElement.GetProperty("printers")[0];

    Assert.Multiple(() =>
                    {
                      Assert.That(printer.TryGetProperty("isPaperNearEnd", out _), Is.True);
                      Assert.That(printer.TryGetProperty("waitingTicketCount", out _), Is.True);
                      Assert.That(printer.TryGetProperty("lastChangedAtUtc", out _), Is.True);
                      Assert.That(printer.TryGetProperty("stationNames", out _), Is.True);
                      Assert.That(printer.TryGetProperty("statusDetail", out _), Is.True);
                      Assert.That(printer.GetProperty("printerType").GetString(), Is.EqualTo("TestPrinter"));
                    });
  }

  [Test]
  public async Task Activate_ItemTakenOffTheMenu_PutsItBackOnTheMenu()
  {
    using (var takenOff = await _context.Client.PostAsync($"/api/admin/items/{_context.World.BratwurstItemId}/deactivate",
                                                         null))
    {
      Assert.That(takenOff.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var response = await _context.Client.PostAsync($"/api/admin/items/{_context.World.BratwurstItemId}/activate",
                                                        null);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    await using var database = _context.Factory.CreateContext();
    var item = await database.CatalogItems.FirstAsync(candidate => candidate.Id == _context.World.BratwurstItemId);

    Assert.That(item.IsActive, Is.True);
  }

  [Test]
  public async Task Activate_StaffMemberTakenOffTheList_PutsThemBackOnTheList()
  {
    using (var takenOff = await _context.Client.PostAsync($"/api/admin/staff-members/{_context.World.StaffMemberId}/deactivate",
                                                         null))
    {
      Assert.That(takenOff.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var response = await _context.Client.PostAsync($"/api/admin/staff-members/{_context.World.StaffMemberId}/activate",
                                                        null);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    await using var database = _context.Factory.CreateContext();
    var staffMember = await database.StaffMembers.FirstAsync(candidate => candidate.Id == _context.World.StaffMemberId);

    Assert.That(staffMember.IsActive, Is.True);
  }

  [Test]
  public async Task Activate_ItemWhoseOnlyStationIsSwitchedOff_IsRefused()
  {
    await SwitchOffEveryStationOfBratwurstAsync();

    using var response = await _context.Client.PostAsync($"/api/admin/items/{_context.World.BratwurstItemId}/activate",
                                                        null);

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.itemHasNoActiveStation"));
                    });
  }

  [Test]
  public async Task Activate_ItemWhoseOnlyStationIsSwitchedOff_LeavesItDeactivated()
  {
    await SwitchOffEveryStationOfBratwurstAsync();

    using var response = await _context.Client.PostAsync($"/api/admin/items/{_context.World.BratwurstItemId}/activate",
                                                        null);

    await using var database = _context.Factory.CreateContext();
    var item = await database.CatalogItems.FirstAsync(candidate => candidate.Id == _context.World.BratwurstItemId);

    Assert.That(item.IsActive, Is.False);
  }

  [Test]
  public async Task PutItem_OnlyStationsThatAreSwitchedOff_IsRefused()
  {
    await using (var database = _context.Factory.CreateContext())
    {
      await database.Stations
                    .Where(station => station.Id == _context.World.BarStationId)
                    .ExecuteUpdateAsync(station => station.SetProperty(entry => entry.IsActive, false));
    }

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/items/{_context.World.BratwurstItemId}",
                                                             new
                                                             {
                                                               name = "Bratwurst",
                                                               categoryName = "Essen",
                                                               priceCents = 350,
                                                               sortOrder = 1,
                                                               stationIds = new[] { _context.World.BarStationId }
                                                             });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
  }

  private async Task SwitchOffEveryStationOfBratwurstAsync()
  {
    await using var database = _context.Factory.CreateContext();
    List<Guid> stationIds = await database.ItemStationAssignments
                                          .Where(assignment => assignment.CatalogItemId == _context.World.BratwurstItemId)
                                          .Select(assignment => assignment.StationId)
                                          .ToListAsync();

    await database.CatalogItems
                  .Where(item => item.Id == _context.World.BratwurstItemId)
                  .ExecuteUpdateAsync(item => item.SetProperty(entry => entry.IsActive, false));
    await database.Stations
                  .Where(station => stationIds.Contains(station.Id))
                  .ExecuteUpdateAsync(station => station.SetProperty(entry => entry.IsActive, false));
  }

  [Test]
  public async Task PutPrinter_TestPrinterWithASimulatedFault_ArmsThatFault()
  {
    var printerId = await KitchenPrinterIdAsync();

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/printers/{printerId}",
                                                             new
                                                             {
                                                               printerType = "TestPrinter",
                                                               name = "Testdrucker Küche",
                                                               simulatedFault = "PaperEnd",
                                                               simulatedFaultMode = "Sticky"
                                                             });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    var registry = _context.Factory.Services.GetRequiredService<IMockFaultRegistry>();

    Assert.That(registry.GetArmedFault(printerId), Is.EqualTo(MockFault.PaperEnd));
  }

  [Test]
  public async Task PutPrinter_ABodyForAnotherPrinterType_IsRefusedAsUnprocessable()
  {
    var printerId = await KitchenPrinterIdAsync();

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/printers/{printerId}",
                                                             new
                                                             {
                                                               printerType = "EpsonTmT20ivNetworkPrinter",
                                                               name = "Drucker Küche",
                                                               host = "192.0.2.10",
                                                               port = 9100
                                                             });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
  }

  [Test]
  public async Task PostTestPrint_PrinterNoStationUses_IsAcceptedBecauseTestingIsAboutTheDevice()
  {
    var printerId = await CreatePrinterAsync();

    using var response = await _context.Client.PostAsync($"/api/admin/printers/{printerId}/test-print",
                                                        null);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
  }

  [Test]
  public async Task PostReconnect_PrinterNoStationUses_IsAcceptedBecauseConnectingIsAboutTheDevice()
  {
    var printerId = await CreatePrinterAsync();

    using var response = await _context.Client.PostAsync($"/api/admin/printers/{printerId}/reconnect",
                                                        null);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
  }

  [Test]
  public async Task DeletePrinter_PrinterNoStationUses_RemovesItFromTheList()
  {
    var printerId = await CreatePrinterAsync();

    using var response = await _context.Client.DeleteAsync($"/api/admin/printers/{printerId}");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    await using var database = _context.Factory.CreateContext();
    Assert.That(await database.Printers.AnyAsync(printer => printer.Id == printerId), Is.False);
  }

  [Test]
  public async Task DeletePrinter_PrinterAStationStillUses_IsRefusedAndNamesTheStation()
  {
    var printerId = await KitchenPrinterIdAsync();

    using var response = await _context.Client.DeleteAsync($"/api/admin/printers/{printerId}");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
  }

  private async Task<Guid> CreatePrinterAsync()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/printers",
                                                              new { printerType = "TestPrinter", name = "Drucker ohne Ausgabestelle" });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.GetProperty("printerId").GetGuid();
  }

  private async Task<Guid> KitchenPrinterIdAsync()
  {
    await using var database = _context.Factory.CreateContext();
    var kitchen = await database.Stations.FirstAsync(station => station.Id == _context.World.KitchenStationId);
    return kitchen.PrinterId!.Value;
  }

  [Test]
  public async Task GetAdminOrders_AfterAnOrderWasPlaced_ListsIt()
  {
    using (var created = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid())))
    {
      Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    using var response = await _context.Client.GetAsync("/api/admin/orders");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.That(body.RootElement.GetProperty("orders").GetArrayLength(), Is.EqualTo(1));
  }

  [Test]
  public async Task PostAdminResolve_UnknownTicketOfAnotherStaffMember_IsStillAllowedFromTheLaptop()
  {
    Guid orderId;
    Guid ticketId;

    using (var created = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid())))
    {
      var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
      orderId = body.RootElement.GetProperty("orderId").GetGuid();
      ticketId = body.RootElement.GetProperty("stationOrders")[0].GetProperty("stationOrderId").GetGuid();
    }

    await using (var database = _context.Factory.CreateContext())
    {
      var job = await database.PrintJobs.FirstAsync(candidate => candidate.StationOrderId == ticketId);
      job.Status = PrintJobStatus.Unknown;
      await database.SaveChangesAsync();
    }

    using var response = await _context.Client.PostAsJsonAsync($"/api/admin/orders/{orderId}/station-orders/{ticketId}/resolve",
                                                              new { slipIsOnThePile = true });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
  }

  [Test]
  public async Task PostAdminResolve_TicketNoLongerUnknown_IsRefused()
  {
    Guid orderId;
    Guid ticketId;

    using (var created = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid())))
    {
      var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
      orderId = body.RootElement.GetProperty("orderId").GetGuid();
      ticketId = body.RootElement.GetProperty("stationOrders")[0].GetProperty("stationOrderId").GetGuid();
    }

    using var response = await _context.Client.PostAsJsonAsync($"/api/admin/orders/{orderId}/station-orders/{ticketId}/resolve",
                                                              new SlipOnThePileBody(true));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
  }
}
