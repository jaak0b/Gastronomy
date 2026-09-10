using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Endpoints;

public static class AdminFestivalEndpoints
{
  public static IEndpointRouteBuilder MapAdminFestivalEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/festivals");

    group.MapGet(string.Empty,
                 async (AdminFestivalHandler handler,
                        CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken));

    group.MapPost(string.Empty,
                  async (SaveFestivalRequest request,
                         AdminFestivalHandler handler,
                         CancellationToken cancellationToken) => await handler.CreateAsync(request, cancellationToken));

    group.MapPut("/{festivalId:guid}",
                 async (Guid festivalId,
                        SaveFestivalRequest request,
                        AdminFestivalHandler handler,
                        CancellationToken cancellationToken) =>
                   await handler.UpdateAsync(festivalId, request, cancellationToken));

    group.MapPost("/{festivalId:guid}/copy",
                  async (Guid festivalId,
                         SaveFestivalRequest request,
                         AdminFestivalHandler handler,
                         CancellationToken cancellationToken) =>
                    await handler.CopyAsync(festivalId, request, cancellationToken));

    group.MapPost("/{festivalId:guid}/hide",
                  async (Guid festivalId,
                         AdminFestivalHandler handler,
                         CancellationToken cancellationToken) => await handler.HideAsync(festivalId, cancellationToken));

    group.MapPost("/{festivalId:guid}/show",
                  async (Guid festivalId,
                         AdminFestivalHandler handler,
                         CancellationToken cancellationToken) => await handler.ShowAsync(festivalId, cancellationToken));

    group.MapPut("/{festivalId:guid}/items/{itemId:guid}",
                 async (Guid festivalId,
                        Guid itemId,
                        SaveFestivalItemRequest request,
                        AdminFestivalMenuHandler handler,
                        CancellationToken cancellationToken) =>
                   await handler.PutOnTheMenuAsync(festivalId, itemId, request, cancellationToken));

    group.MapDelete("/{festivalId:guid}/items/{itemId:guid}",
                    async (Guid festivalId,
                           Guid itemId,
                           AdminFestivalMenuHandler handler,
                           CancellationToken cancellationToken) =>
                      await handler.TakeOffTheMenuAsync(festivalId, itemId, cancellationToken));

    group.MapPost("/{festivalId:guid}/items/{itemId:guid}/availability",
                  async (Guid festivalId,
                         Guid itemId,
                         SetAvailabilityRequest request,
                         AdminFestivalMenuHandler handler,
                         CancellationToken cancellationToken) =>
                    await handler.SetAvailabilityAsync(festivalId, itemId, request, cancellationToken));

    group.MapPut("/{festivalId:guid}/stations/{stationId:guid}",
                 async (Guid festivalId,
                        Guid stationId,
                        AdminFestivalStationHandler handler,
                        CancellationToken cancellationToken) =>
                   await handler.AddAsync(festivalId, stationId, cancellationToken));

    group.MapDelete("/{festivalId:guid}/stations/{stationId:guid}",
                    async (Guid festivalId,
                           Guid stationId,
                           AdminFestivalStationHandler handler,
                           CancellationToken cancellationToken) =>
                      await handler.RemoveAsync(festivalId, stationId, cancellationToken));

    return routes;
  }
}

public sealed class FestivalMoment
{
  public DateTime AsUtc(DateTime moment)
  {
    return moment.Kind == DateTimeKind.Local
             ? moment.ToUniversalTime()
             : DateTime.SpecifyKind(moment, DateTimeKind.Utc);
  }
}

public sealed record FestivalPeriod(string Name, DateTime StartsAtUtc, DateTime EndsAtUtc);

public sealed record FestivalPeriodOutcome(FestivalPeriod? Period, IResult? Refusal);

public sealed class AdminFestivalHandler
{
  private const int FirstNumber = 1;

  private readonly IClock _clock;
  private readonly GastronomyAppDbContext _dbContext;
  private readonly IFestivalRepository _festivalRepository;
  private readonly ILogger<AdminFestivalHandler> _logger;
  private readonly FestivalMoment _moment = new();
  private readonly ResultEnvelope _resultEnvelope;
  private readonly FestivalSchedule _schedule;
  private readonly CatalogWriteTransaction _writeTransaction;

  public AdminFestivalHandler(GastronomyAppDbContext dbContext,
                              IFestivalRepository festivalRepository,
                              FestivalSchedule schedule,
                              CatalogWriteTransaction writeTransaction,
                              ResultEnvelope resultEnvelope,
                              IClock clock,
                              ILogger<AdminFestivalHandler> logger)
  {
    _dbContext = dbContext;
    _festivalRepository = festivalRepository;
    _schedule = schedule;
    _writeTransaction = writeTransaction;
    _resultEnvelope = resultEnvelope;
    _clock = clock;
    _logger = logger;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    IReadOnlyCollection<Festival> festivals = await _festivalRepository.FindAllAsync(cancellationToken);

    Dictionary<Guid, int> stationCounts = await _dbContext.FestivalStations
                                                          .AsNoTracking()
                                                          .GroupBy(link => link.FestivalId)
                                                          .Select(group => new CountPerFestival(group.Key, group.Count()))
                                                          .ToDictionaryAsync(row => row.FestivalId,
                                                                             row => row.Count,
                                                                             cancellationToken);

    Dictionary<Guid, int> menuItemCounts = await _dbContext.FestivalCatalogItems
                                                           .AsNoTracking()
                                                           .GroupBy(menuRow => menuRow.FestivalId)
                                                           .Select(group => new CountPerFestival(group.Key, group.Count()))
                                                           .ToDictionaryAsync(row => row.FestivalId,
                                                                              row => row.Count,
                                                                              cancellationToken);

    Dictionary<Guid, int> orderCounts = await _dbContext.Orders
                                                        .AsNoTracking()
                                                        .GroupBy(order => order.FestivalId)
                                                        .Select(group => new CountPerFestival(group.Key, group.Count()))
                                                        .ToDictionaryAsync(row => row.FestivalId,
                                                                           row => row.Count,
                                                                           cancellationToken);

    var nowUtc = _clock.UtcNow;

    List<AdminFestivalView> views =
    [
      .. festivals.Select(festival => new AdminFestivalView(festival.Id,
                                                            festival.Name,
                                                            festival.StartsAtUtc,
                                                            festival.EndsAtUtc,
                                                            festival.IsHidden,
                                                            _schedule.IsRunning(festival, nowUtc),
                                                            CountOf(stationCounts, festival.Id),
                                                            CountOf(menuItemCounts, festival.Id),
                                                            CountOf(orderCounts, festival.Id)))
    ];

    return Results.Ok(new AdminFestivalListView(views));
  }

  public Task<IResult> CreateAsync(SaveFestivalRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return _writeTransaction.RunAsync(_dbContext,
                                      transactionCancellationToken =>
                                        CreatedAsync(request, transactionCancellationToken),
                                      cancellationToken);
  }

  public Task<IResult> UpdateAsync(Guid festivalId,
                                   SaveFestivalRequest request,
                                   CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return _writeTransaction.RunAsync(_dbContext,
                                      transactionCancellationToken =>
                                        UpdatedAsync(festivalId, request, transactionCancellationToken),
                                      cancellationToken);
  }

  public Task<IResult> CopyAsync(Guid festivalId,
                                 SaveFestivalRequest request,
                                 CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return _writeTransaction.RunAsync(_dbContext,
                                      transactionCancellationToken =>
                                        CopiedAsync(festivalId, request, transactionCancellationToken),
                                      cancellationToken);
  }

  public Task<IResult> HideAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return _writeTransaction.RunAsync(_dbContext,
                                      transactionCancellationToken =>
                                        HiddenAsync(festivalId, transactionCancellationToken),
                                      cancellationToken);
  }

  public Task<IResult> ShowAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return _writeTransaction.RunAsync(_dbContext,
                                      transactionCancellationToken =>
                                        ShownAsync(festivalId, transactionCancellationToken),
                                      cancellationToken);
  }

  private async Task<CatalogWrite> CreatedAsync(SaveFestivalRequest request, CancellationToken cancellationToken)
  {
    FestivalPeriodOutcome period = await PeriodOfAsync(request, Guid.Empty, cancellationToken);

    if (period.Refusal is not null)
    {
      return new(period.Refusal, false);
    }

    var festivalId = Guid.NewGuid();

    _dbContext.Festivals.Add(new()
                             {
                               Id = festivalId,
                               Name = period.Period!.Name,
                               StartsAtUtc = period.Period.StartsAtUtc,
                               EndsAtUtc = period.Period.EndsAtUtc,
                               NextOrderNumber = FirstNumber,
                               IsHidden = false
                             });

    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(Results.Json(new SavedFestivalView(festivalId), statusCode: StatusCodes.Status201Created), true);
  }

  private async Task<CatalogWrite> UpdatedAsync(Guid festivalId,
                                                SaveFestivalRequest request,
                                                CancellationToken cancellationToken)
  {
    var festival = await _dbContext.Festivals
                                   .FirstOrDefaultAsync(candidate => candidate.Id == festivalId, cancellationToken);

    if (festival is null)
    {
      return new(Results.NotFound(), false);
    }

    FestivalPeriodOutcome period =
      await PeriodOfAsync(request, festivalId, cancellationToken);

    if (period.Refusal is not null)
    {
      return new(period.Refusal, false);
    }

    festival.Name = period.Period!.Name;
    festival.StartsAtUtc = period.Period.StartsAtUtc;
    festival.EndsAtUtc = period.Period.EndsAtUtc;

    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(Results.Ok(new SavedFestivalView(festivalId)), true);
  }

  private async Task<CatalogWrite> CopiedAsync(Guid festivalId,
                                               SaveFestivalRequest request,
                                               CancellationToken cancellationToken)
  {
    var copiedFrom = await _dbContext.Festivals
                                     .AsNoTracking()
                                     .FirstOrDefaultAsync(candidate => candidate.Id == festivalId, cancellationToken);

    if (copiedFrom is null)
    {
      return new(Results.NotFound(), false);
    }

    FestivalPeriodOutcome period = await PeriodOfAsync(request, Guid.Empty, cancellationToken);

    if (period.Refusal is not null)
    {
      return new(period.Refusal, false);
    }

    var newFestivalId = Guid.NewGuid();

    _dbContext.Festivals.Add(new()
                             {
                               Id = newFestivalId,
                               Name = period.Period!.Name,
                               StartsAtUtc = period.Period.StartsAtUtc,
                               EndsAtUtc = period.Period.EndsAtUtc,
                               NextOrderNumber = FirstNumber,
                               IsHidden = false
                             });

    List<FestivalStation> stationLinks = await _dbContext.FestivalStations
                                                         .AsNoTracking()
                                                         .Where(link => link.FestivalId == festivalId)
                                                         .ToListAsync(cancellationToken);

    foreach (var link in stationLinks)
    {
      _dbContext.FestivalStations.Add(new()
                                      {
                                        Id = Guid.NewGuid(),
                                        FestivalId = newFestivalId,
                                        StationId = link.StationId,
                                        NextStationOrderNumber = FirstNumber
                                      });
    }

    List<FestivalCatalogItem> menuRows = await _dbContext.FestivalCatalogItems
                                                         .AsNoTracking()
                                                         .Where(menuRow => menuRow.FestivalId == festivalId)
                                                         .ToListAsync(cancellationToken);

    foreach (var menuRow in menuRows)
    {
      _dbContext.FestivalCatalogItems.Add(new()
                                          {
                                            Id = Guid.NewGuid(),
                                            FestivalId = newFestivalId,
                                            CatalogItemId = menuRow.CatalogItemId,
                                            PriceCents = menuRow.PriceCents,
                                            IsAvailable = true
                                          });
    }

    List<ItemStationAssignment> assignments = await _dbContext.ItemStationAssignments
                                                              .AsNoTracking()
                                                              .Where(assignment => assignment.FestivalId == festivalId)
                                                              .ToListAsync(cancellationToken);

    foreach (var assignment in assignments)
    {
      _dbContext.ItemStationAssignments.Add(new()
                                            {
                                              Id = Guid.NewGuid(),
                                              FestivalId = newFestivalId,
                                              CatalogItemId = assignment.CatalogItemId,
                                              StationId = assignment.StationId
                                            });
    }

    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(Results.Json(new SavedFestivalView(newFestivalId), statusCode: StatusCodes.Status201Created), true);
  }

  private async Task<CatalogWrite> HiddenAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    var festival = await _dbContext.Festivals
                                   .FirstOrDefaultAsync(candidate => candidate.Id == festivalId, cancellationToken);

    if (festival is null)
    {
      return new(Results.NotFound(), false);
    }

    if (_schedule.IsRunning(festival, _clock.UtcNow))
    {
      _logger.LogWarning("The festival {FestivalId} was not hidden because it is running right now, and hiding it would empty every phone and every station tablet in the middle of service. The festivals page draws no hide control on a running festival, so this call did not come from that screen.",
                         festivalId);

      return new(ActionFailed(), false);
    }

    if (festival.IsHidden)
    {
      return new(Results.Ok(new SavedFestivalView(festivalId)), false);
    }

    festival.IsHidden = true;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(Results.Ok(new SavedFestivalView(festivalId)), true);
  }

  private async Task<CatalogWrite> ShownAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    var festival = await _dbContext.Festivals
                                   .FirstOrDefaultAsync(candidate => candidate.Id == festivalId, cancellationToken);

    if (festival is null)
    {
      return new(Results.NotFound(), false);
    }

    if (!festival.IsHidden)
    {
      return new(Results.Ok(new SavedFestivalView(festivalId)), false);
    }

    festival.IsHidden = false;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(Results.Ok(new SavedFestivalView(festivalId)), true);
  }

  private async Task<FestivalPeriodOutcome> PeriodOfAsync(SaveFestivalRequest request,
                                                                    Guid candidateId,
                                                                    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.Name))
    {
      return new(null,
                 _resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                        "ValidationFailed",
                                        "admin.festivalNameMissing"));
    }

    var startsAtUtc = _moment.AsUtc(request.StartsAtUtc);
    var endsAtUtc = _moment.AsUtc(request.EndsAtUtc);

    if (endsAtUtc <= startsAtUtc)
    {
      return new(null,
                 _resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                        "ValidationFailed",
                                        "admin.festivalPeriodInvalid"));
    }

    IReadOnlyCollection<Festival> others = await _festivalRepository.FindAllAsync(cancellationToken);

    var inTheWay = _schedule.Overlapping(candidateId, startsAtUtc, endsAtUtc, others);

    if (inTheWay is not null)
    {
      return new(null,
                 _resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                        "FestivalOverlaps",
                                        "admin.festivalOverlaps",
                                        new Dictionary<string, string> { ["name"] = inTheWay.Name }));
    }

    return new(new(request.Name, startsAtUtc, endsAtUtc), null);
  }

  private IResult ActionFailed()
  {
    return _resultEnvelope.Problem(StatusCodes.Status400BadRequest, "ValidationFailed", "admin.actionFailed");
  }

  private int CountOf(Dictionary<Guid, int> counts, Guid festivalId)
  {
    return counts.TryGetValue(festivalId, out var count) ? count : 0;
  }

  private sealed record CountPerFestival(Guid FestivalId, int Count);
}

public sealed class AdminFestivalMenuHandler
{
  private const int HighestPriceCents = 99999;
  private const int LowestPriceCents = 0;

  private readonly GastronomyAppDbContext _dbContext;
  private readonly OrderableItems _orderableItems;
  private readonly ILogger<AdminFestivalMenuHandler> _logger;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly CatalogWriteTransaction _writeTransaction;

  public AdminFestivalMenuHandler(GastronomyAppDbContext dbContext,
                                  CatalogWriteTransaction writeTransaction,
                                  OrderableItems orderableItems,
                                  ResultEnvelope resultEnvelope,
                                  ILogger<AdminFestivalMenuHandler> logger)
  {
    _dbContext = dbContext;
    _writeTransaction = writeTransaction;
    _orderableItems = orderableItems;
    _resultEnvelope = resultEnvelope;
    _logger = logger;
  }

  public Task<IResult> PutOnTheMenuAsync(Guid festivalId,
                                         Guid itemId,
                                         SaveFestivalItemRequest request,
                                         CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return _writeTransaction.RunAsync(_dbContext,
                                      transactionCancellationToken =>
                                        PutOnAsync(festivalId, itemId, request, transactionCancellationToken),
                                      cancellationToken);
  }

  public Task<IResult> TakeOffTheMenuAsync(Guid festivalId, Guid itemId, CancellationToken cancellationToken)
  {
    return _writeTransaction.RunAsync(_dbContext,
                                      transactionCancellationToken =>
                                        TakenOffAsync(festivalId, itemId, transactionCancellationToken),
                                      cancellationToken);
  }

  public Task<IResult> SetAvailabilityAsync(Guid festivalId,
                                            Guid itemId,
                                            SetAvailabilityRequest request,
                                            CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return _writeTransaction.RunAsync(_dbContext,
                                      transactionCancellationToken =>
                                        AvailabilitySetAsync(festivalId, itemId, request, transactionCancellationToken),
                                      cancellationToken);
  }

  private async Task<CatalogWrite> PutOnAsync(Guid festivalId,
                                              Guid itemId,
                                              SaveFestivalItemRequest request,
                                              CancellationToken cancellationToken)
  {
    var festivalExists = await _dbContext.Festivals
                                         .AsNoTracking()
                                         .AnyAsync(candidate => candidate.Id == festivalId, cancellationToken);

    var itemExists = await _dbContext.CatalogItems
                                     .AsNoTracking()
                                     .AnyAsync(candidate => candidate.Id == itemId, cancellationToken);

    if (!festivalExists || !itemExists)
    {
      return new(Results.NotFound(), false);
    }

    if (request.PriceCents is < LowestPriceCents or > HighestPriceCents)
    {
      return new(_resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                        "ValidationFailed",
                                        "admin.itemPriceOutOfRange"),
                 false);
    }

    List<Guid> stationIds = [.. request.StationIds ?? []];

    List<Guid> stationIdsAtTheFestival = await _dbContext.FestivalStations
                                                         .AsNoTracking()
                                                         .Where(link => link.FestivalId == festivalId)
                                                         .Select(link => link.StationId)
                                                         .ToListAsync(cancellationToken);

    List<Guid> strangers = [.. stationIds.Where(stationId => !stationIdsAtTheFestival.Contains(stationId))];

    if (strangers.Count > 0)
    {
      _logger.LogWarning("The item {ItemId} was not put on the menu of the festival {FestivalId} because the stations {StationIds} do not belong to that festival. The item screen offers only that festival's stations, so this call did not come from that screen.",
                         itemId,
                         festivalId,
                         strangers);

      return new(_resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                        "ValidationFailed",
                                        "admin.actionFailed"),
                 false);
    }

    if (!await _orderableItems.AnyOfThemWouldPrepareAtAsync(_dbContext, festivalId, stationIds, cancellationToken))
    {
      return new(_resultEnvelope.Problem(StatusCodes.Status422UnprocessableEntity,
                                        "UnprocessableEntity",
                                        "admin.itemNeedsAStation"),
                 false);
    }

    var menuRow = await _dbContext.FestivalCatalogItems
                                  .FirstOrDefaultAsync(candidate => candidate.FestivalId == festivalId
                                                                    && candidate.CatalogItemId == itemId,
                                                       cancellationToken);

    if (menuRow is null)
    {
      _dbContext.FestivalCatalogItems.Add(new()
                                          {
                                            Id = Guid.NewGuid(),
                                            FestivalId = festivalId,
                                            CatalogItemId = itemId,
                                            PriceCents = request.PriceCents,
                                            IsAvailable = true
                                          });
    }
    else
    {
      menuRow.PriceCents = request.PriceCents;
    }

    List<ItemStationAssignment> existing = await _dbContext.ItemStationAssignments
                                                           .Where(assignment => assignment.FestivalId == festivalId
                                                                                && assignment.CatalogItemId == itemId)
                                                           .ToListAsync(cancellationToken);

    _dbContext.ItemStationAssignments.RemoveRange(existing);

    foreach (var stationId in stationIds.Distinct())
    {
      _dbContext.ItemStationAssignments.Add(new()
                                            {
                                              Id = Guid.NewGuid(),
                                              FestivalId = festivalId,
                                              CatalogItemId = itemId,
                                              StationId = stationId
                                            });
    }

    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(Results.Ok(new SavedItemView(itemId)), true);
  }

  private async Task<CatalogWrite> TakenOffAsync(Guid festivalId, Guid itemId, CancellationToken cancellationToken)
  {
    var menuRow = await _dbContext.FestivalCatalogItems
                                  .FirstOrDefaultAsync(candidate => candidate.FestivalId == festivalId
                                                                    && candidate.CatalogItemId == itemId,
                                                       cancellationToken);

    if (menuRow is null)
    {
      return new(Results.NotFound(), false);
    }

    List<ItemStationAssignment> assignments = await _dbContext.ItemStationAssignments
                                                              .Where(assignment => assignment.FestivalId == festivalId
                                                                                   && assignment.CatalogItemId == itemId)
                                                              .ToListAsync(cancellationToken);

    _dbContext.ItemStationAssignments.RemoveRange(assignments);
    _dbContext.FestivalCatalogItems.Remove(menuRow);

    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(Results.NoContent(), true);
  }

  private async Task<CatalogWrite> AvailabilitySetAsync(Guid festivalId,
                                                        Guid itemId,
                                                        SetAvailabilityRequest request,
                                                        CancellationToken cancellationToken)
  {
    var menuRow = await _dbContext.FestivalCatalogItems
                                  .FirstOrDefaultAsync(candidate => candidate.FestivalId == festivalId
                                                                    && candidate.CatalogItemId == itemId,
                                                       cancellationToken);

    if (menuRow is null)
    {
      return new(Results.NotFound(), false);
    }

    if (menuRow.IsAvailable == request.IsAvailable)
    {
      return new(Results.Ok(new SavedItemView(itemId)), false);
    }

    menuRow.IsAvailable = request.IsAvailable;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(Results.Ok(new SavedItemView(itemId)), true);
  }
}

public sealed class AdminFestivalStationHandler
{
  private const int FirstNumber = 1;

  private readonly StationChangeAnnouncer _announcer;
  private readonly GastronomyAppDbContext _dbContext;
  private readonly OrderableItems _orderableItems;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly ImmediateTransactionRunner _transactionRunner = new();

  public AdminFestivalStationHandler(GastronomyAppDbContext dbContext,
                                     StationChangeAnnouncer announcer,
                                     OrderableItems orderableItems,
                                     ResultEnvelope resultEnvelope)
  {
    _dbContext = dbContext;
    _announcer = announcer;
    _orderableItems = orderableItems;
    _resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> AddAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    CatalogWrite written = await _transactionRunner.RunAsync(_dbContext,
                                                             async transactionCancellationToken =>
                                                             {
                                                               CatalogWrite outcome =
                                                                 await AddedAsync(festivalId,
                                                                                  stationId,
                                                                                  transactionCancellationToken);

                                                               return new TransactionOutcome<CatalogWrite>
                                                                      {
                                                                        Value = outcome,
                                                                        ShouldCommit = outcome.SomethingChanged
                                                                      };
                                                             },
                                                             cancellationToken);

    return await AnnouncedAsync(written, stationId);
  }

  public async Task<IResult> RemoveAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    CatalogWrite written = await _transactionRunner.RunAsync(_dbContext,
                                                             async transactionCancellationToken =>
                                                             {
                                                               CatalogWrite outcome =
                                                                 await RemovedAsync(festivalId,
                                                                                    stationId,
                                                                                    transactionCancellationToken);

                                                               return new TransactionOutcome<CatalogWrite>
                                                                      {
                                                                        Value = outcome,
                                                                        ShouldCommit = outcome.SomethingChanged
                                                                      };
                                                             },
                                                             cancellationToken);

    return await AnnouncedAsync(written, stationId);
  }

  private async Task<CatalogWrite> AddedAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    var festivalExists = await _dbContext.Festivals
                                         .AsNoTracking()
                                         .AnyAsync(candidate => candidate.Id == festivalId, cancellationToken);

    var stationExists = await _dbContext.Stations
                                        .AsNoTracking()
                                        .AnyAsync(candidate => candidate.Id == stationId, cancellationToken);

    if (!festivalExists || !stationExists)
    {
      return new(Results.NotFound(), false);
    }

    var alreadyThere = await _dbContext.FestivalStations
                                       .AsNoTracking()
                                       .AnyAsync(link => link.FestivalId == festivalId && link.StationId == stationId,
                                                 cancellationToken);

    if (alreadyThere)
    {
      return new(Results.Ok(new SavedStationView(stationId)), false);
    }

    _dbContext.FestivalStations.Add(new()
                                    {
                                      Id = Guid.NewGuid(),
                                      FestivalId = festivalId,
                                      StationId = stationId,
                                      NextStationOrderNumber = FirstNumber
                                    });

    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(Results.Ok(new SavedStationView(stationId)), true);
  }

  private async Task<CatalogWrite> RemovedAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    var link = await _dbContext.FestivalStations
                               .FirstOrDefaultAsync(candidate => candidate.FestivalId == festivalId
                                                                 && candidate.StationId == stationId,
                                                    cancellationToken);

    if (link is null)
    {
      return new(Results.NotFound(), false);
    }

    var sliceCount = await (from slice in _dbContext.StationOrders.AsNoTracking()
                            join order in _dbContext.Orders.AsNoTracking()
                              on slice.OrderId equals order.Id
                            where slice.StationId == stationId && order.FestivalId == festivalId
                            select slice.Id)
                           .CountAsync(cancellationToken);

    if (sliceCount > 0)
    {
      return new(_resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                        "StationHasOrdersAtTheFestival",
                                        "admin.stationHasOrdersAtTheFestival",
                                        new Dictionary<string, string> { ["count"] = sliceCount.ToString() }),
                 false);
    }

    IReadOnlyList<Guid> strandedItemIds =
      await _orderableItems.WouldStopBeingOrderableAtAsync(_dbContext,
                                                           festivalId,
                                                           [stationId],
                                                           cancellationToken);

    if (strandedItemIds.Count > 0)
    {
      return new(_resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                        "ItemsWouldHaveNoStation",
                                        "admin.itemsWouldHaveNoStation",
                                        new Dictionary<string, string>
                                        {
                                          ["count"] = strandedItemIds.Count.ToString()
                                        }),
                 false);
    }

    List<ItemStationAssignment> assignmentsHere =
      await _dbContext.ItemStationAssignments
                      .Where(assignment => assignment.FestivalId == festivalId
                                           && assignment.StationId == stationId)
                      .ToListAsync(cancellationToken);

    _dbContext.ItemStationAssignments.RemoveRange(assignmentsHere);
    _dbContext.FestivalStations.Remove(link);

    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(Results.NoContent(), true);
  }

  private async Task<IResult> AnnouncedAsync(CatalogWrite written, Guid stationId)
  {
    if (written.SomethingChanged)
    {
      await _announcer.AnnounceAsync(stationId);
    }

    return written.Response;
  }
}
