using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Printing;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Printing;
using GastronomyApp.Infrastructure;
using GastronomyApp.Infrastructure.Printing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public static class AdminPrinterEndpoints
{
  public static IEndpointRouteBuilder MapAdminPrinterEndpoints(this IEndpointRouteBuilder routes)
  {
    RouteGroupBuilder group = routes.MapGroup("/api/admin/printers");

    group.MapGet(string.Empty, async (
        AdminPrinterHandler handler,
        CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken));

    group.MapPost(string.Empty, async (
        SavePrinterRequest request,
        AdminPrinterHandler handler,
        CancellationToken cancellationToken) => await handler.CreateAsync(request, cancellationToken));

    group.MapPut("/{printerId:guid}", async (
        Guid printerId,
        SavePrinterRequest request,
        AdminPrinterHandler handler,
        CancellationToken cancellationToken) => await handler.SaveAsync(printerId, request, cancellationToken));

    group.MapDelete("/{printerId:guid}", async (
        Guid printerId,
        AdminPrinterHandler handler,
        CancellationToken cancellationToken) => await handler.DeleteAsync(printerId, cancellationToken));

    group.MapPost("/{printerId:guid}/test-print", async (
        Guid printerId,
        AdminPrinterHandler handler,
        CancellationToken cancellationToken) => await handler.TestPrintAsync(printerId, cancellationToken));

    group.MapPost("/{printerId:guid}/reconnect", async (
        Guid printerId,
        AdminPrinterHandler handler,
        CancellationToken cancellationToken) => await handler.ReconnectAsync(printerId, cancellationToken));

    return routes;
  }
}

public sealed class AdminPrinterHandler
{
  private readonly GastronomyAppDbContext dbContext;
  private readonly IPrinterFleet printerFleet;
  private readonly PrinterFleet fleet;
  private readonly IMockFaultRegistry mockFaultRegistry;
  private readonly IPrinterWorkerDataAccess printerWorkerDataAccess;
  private readonly ResultEnvelope resultEnvelope;

  public AdminPrinterHandler(
      GastronomyAppDbContext dbContext,
      IPrinterFleet printerFleet,
      PrinterFleet fleet,
      IMockFaultRegistry mockFaultRegistry,
      IPrinterWorkerDataAccess printerWorkerDataAccess,
      ResultEnvelope resultEnvelope)
  {
    this.dbContext = dbContext;
    this.printerFleet = printerFleet;
    this.fleet = fleet;
    this.mockFaultRegistry = mockFaultRegistry;
    this.printerWorkerDataAccess = printerWorkerDataAccess;
    this.resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    List<Printer> printers = await dbContext.Printers
        .AsNoTracking()
        .OrderBy(printer => printer.Name)
        .ToListAsync(cancellationToken);

    List<Station> stations = await dbContext.Stations
        .AsNoTracking()
        .OrderBy(station => station.SortOrder)
        .ToListAsync(cancellationToken);

    List<PrinterStatus> statuses = await dbContext.PrinterStatuses
        .AsNoTracking()
        .ToListAsync(cancellationToken);

    List<AdminPrinterView> views = [];

    foreach (Printer printer in printers)
    {
      List<Station> served = [.. stations.Where(station => station.PrinterId == printer.Id)];
      PrinterStatus? status = statuses.FirstOrDefault(
          candidate => candidate.PrinterId == printer.Id);

      int waitingTicketCount = 0;
      foreach (Station station in served)
      {
        waitingTicketCount += await printerWorkerDataAccess.CountWaitingPrintJobsAsync(
            station.Id,
            cancellationToken);
      }

      views.Add(ViewOf(printer, served, status, waitingTicketCount));
    }

    return Results.Ok(new AdminPrinterListView(views));
  }

  public async Task<IResult> CreateAsync(SavePrinterRequest request, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.Name))
    {
      return NameIsMissing();
    }

    Printer printer = NewPrinterFrom(request);
    dbContext.Printers.Add(printer);
    ApplyTo(printer, request);
    await dbContext.SaveChangesAsync(cancellationToken);
    await fleet.ReconcileAsync(cancellationToken);

    return Results.Json(
        new SavedPrinterView(printer.Id, []),
        statusCode: StatusCodes.Status201Created);
  }

  public async Task<IResult> SaveAsync(
      Guid printerId,
      SavePrinterRequest request,
      CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.Name))
    {
      return NameIsMissing();
    }

    Printer? printer = await dbContext.Printers
        .FirstOrDefaultAsync(candidate => candidate.Id == printerId, cancellationToken);

    if (printer is null)
    {
      return Results.NotFound();
    }

    if (!MatchesTypeOf(printer, request))
    {
      return resultEnvelope.Problem(
          StatusCodes.Status422UnprocessableEntity,
          "UnprocessableEntity",
          "admin.printerTypeCannotChange");
    }

    printer.Name = request.Name.Trim();
    ApplyTo(printer, request);
    await dbContext.SaveChangesAsync(cancellationToken);
    await fleet.ReconcileAsync(cancellationToken);

    IReadOnlyList<string> stationNames = await StationNamesOnAsync(printerId, cancellationToken);
    return Results.Ok(new SavedPrinterView(printerId, stationNames));
  }

  public async Task<IResult> DeleteAsync(Guid printerId, CancellationToken cancellationToken)
  {
    Printer? printer = await dbContext.Printers
        .FirstOrDefaultAsync(candidate => candidate.Id == printerId, cancellationToken);

    if (printer is null)
    {
      return Results.NotFound();
    }

    IReadOnlyList<string> stationNames = await StationNamesOnAsync(printerId, cancellationToken);
    if (stationNames.Count > 0)
    {
      return resultEnvelope.Problem(
          StatusCodes.Status409Conflict,
          "Conflict",
          "admin.printerStillHasStations",
          new Dictionary<string, string> { ["names"] = string.Join(", ", stationNames) });
    }

    PrinterStatus? status = await dbContext.PrinterStatuses
        .FirstOrDefaultAsync(candidate => candidate.PrinterId == printerId, cancellationToken);
    if (status is not null)
    {
      dbContext.PrinterStatuses.Remove(status);
    }

    dbContext.Printers.Remove(printer);
    await dbContext.SaveChangesAsync(cancellationToken);
    await fleet.ReconcileAsync(cancellationToken);

    return Results.Ok(new SavedPrinterView(printerId, []));
  }

  public async Task<IResult> TestPrintAsync(Guid printerId, CancellationToken cancellationToken)
  {
    if (!await PrinterExistsAsync(printerId, cancellationToken))
    {
      return Results.NotFound();
    }

    try
    {
      await printerFleet.TestPrintAsync(printerId, cancellationToken);
    }
    catch (UnknownStationOrderException)
    {
      return NoWorkerServesThisPrinter();
    }

    return Results.Json(new SavedPrinterView(printerId, []), statusCode: StatusCodes.Status202Accepted);
  }

  public async Task<IResult> ReconnectAsync(Guid printerId, CancellationToken cancellationToken)
  {
    if (!await PrinterExistsAsync(printerId, cancellationToken))
    {
      return Results.NotFound();
    }

    IReadOnlyList<Guid> clearedStationIds;

    try
    {
      clearedStationIds = await printerFleet.ReconnectAsync(printerId, cancellationToken);
    }
    catch (UnknownStationOrderException)
    {
      return NoWorkerServesThisPrinter();
    }

    return Results.Json(
        new ReconnectedView(printerId, clearedStationIds),
        statusCode: StatusCodes.Status202Accepted);
  }

  private AdminPrinterView ViewOf(
      Printer printer,
      IReadOnlyList<Station> served,
      PrinterStatus? status,
      int waitingTicketCount)
  {
    IReadOnlyList<string> stationNames = [.. served.Select(station => station.Name)];

    if (printer is EpsonTmT20ivNetworkPrinter network)
    {
      return new EpsonTmT20ivNetworkPrinterView
      {
        PrinterId = printer.Id,
        Name = printer.Name,
        IsOnline = status?.IsOnline ?? false,
        IsPaperEnd = status?.IsPaperEnd ?? false,
        IsPaperNearEnd = status?.IsPaperNearEnd ?? false,
        IsCoverOpen = status?.IsCoverOpen ?? false,
        IsFaulty = status?.IsFaulty ?? false,
        WaitingTicketCount = waitingTicketCount,
        LastChangedAtUtc = status?.LastChangedAtUtc,
        StatusDetail = status?.LastDetail,
        StationNames = stationNames,
        Host = network.Host,
        Port = network.Port,
      };
    }

    ArmedMockFault armed = mockFaultRegistry.Armed(printer.Id);
    return new TestPrinterView
    {
      PrinterId = printer.Id,
      Name = printer.Name,
      IsOnline = status?.IsOnline ?? false,
      IsPaperEnd = status?.IsPaperEnd ?? false,
      IsPaperNearEnd = status?.IsPaperNearEnd ?? false,
      IsCoverOpen = status?.IsCoverOpen ?? false,
      IsFaulty = status?.IsFaulty ?? false,
      WaitingTicketCount = waitingTicketCount,
      LastChangedAtUtc = status?.LastChangedAtUtc,
      StatusDetail = status?.LastDetail,
      StationNames = stationNames,
      SimulatedFault = armed.Fault.ToString(),
      SimulatedFaultMode = armed.Mode.ToString(),
    };
  }

  private Printer NewPrinterFrom(SavePrinterRequest request)
  {
    if (request is SaveEpsonTmT20ivNetworkPrinterRequest network)
    {
      return new EpsonTmT20ivNetworkPrinter
      {
        Id = Guid.NewGuid(),
        Name = request.Name!.Trim(),
        Host = network.Host?.Trim() ?? string.Empty,
        Port = network.Port,
      };
    }

    return new TestPrinter
    {
      Id = Guid.NewGuid(),
      Name = request.Name!.Trim(),
    };
  }

  private void ApplyTo(Printer printer, SavePrinterRequest request)
  {
    if (printer is EpsonTmT20ivNetworkPrinter network
        && request is SaveEpsonTmT20ivNetworkPrinterRequest networkRequest)
    {
      network.Host = networkRequest.Host?.Trim() ?? string.Empty;
      network.Port = networkRequest.Port;
      return;
    }

    if (request is SaveTestPrinterRequest testRequest)
    {
      MockFault fault = Enum.TryParse(testRequest.SimulatedFault, out MockFault parsedFault)
          ? parsedFault
          : MockFault.None;
      MockFaultMode mode = Enum.TryParse(testRequest.SimulatedFaultMode, out MockFaultMode parsedMode)
          ? parsedMode
          : MockFaultMode.Once;
      mockFaultRegistry.Arm(printer.Id, fault, mode);
    }
  }

  private bool MatchesTypeOf(Printer printer, SavePrinterRequest request)
  {
    return (printer, request) switch
    {
      (EpsonTmT20ivNetworkPrinter, SaveEpsonTmT20ivNetworkPrinterRequest) => true,
      (TestPrinter, SaveTestPrinterRequest) => true,
      _ => false,
    };
  }

  private Task<bool> PrinterExistsAsync(Guid printerId, CancellationToken cancellationToken)
  {
    return dbContext.Printers.AnyAsync(printer => printer.Id == printerId, cancellationToken);
  }

  private async Task<IReadOnlyList<string>> StationNamesOnAsync(Guid printerId, CancellationToken cancellationToken)
  {
    return await dbContext.Stations
        .AsNoTracking()
        .Where(station => station.PrinterId == printerId)
        .OrderBy(station => station.SortOrder)
        .Select(station => station.Name)
        .ToListAsync(cancellationToken);
  }

  private IResult NameIsMissing()
  {
    return resultEnvelope.Problem(
        StatusCodes.Status400BadRequest,
        "ValidationFailed",
        "admin.printerNameMissing");
  }

  private IResult NoWorkerServesThisPrinter()
  {
    return resultEnvelope.Problem(
        StatusCodes.Status409Conflict,
        "NoPrinterWorkerForStation",
        "admin.stationHasNoPrinterWorker");
  }
}
