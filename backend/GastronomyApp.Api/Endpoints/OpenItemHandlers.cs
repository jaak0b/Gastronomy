using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hub;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Endpoints;

public sealed record OpenItemOwner(
  Guid OrderId,
  string TableName,
  int GlobalOrderNumber,
  DateTime OrderedAtUtc);

public sealed record OpenItemOwnerLookup(
  IReadOnlyDictionary<Guid, OpenItemOwner> Owners,
  IReadOnlyList<Guid> OrderItemIdsWithoutAnOrder);

public sealed class OpenItemsReader
{
  private readonly IClock _clock;
  private readonly TimeSpan _givenAwayLookback = TimeSpan.FromHours(24);
  private readonly ILogger<OpenItemsReader> _logger;
  private readonly OrderItemSettlementService _settlementService;

  public OpenItemsReader(OrderItemSettlementService settlementService,
                         IClock clock,
                         ILogger<OpenItemsReader> logger)
  {
    _settlementService = settlementService;
    _clock = clock;
    _logger = logger;
  }

  public async Task<OpenItemsView> ReadAsync(GastronomyAppDbContext dbContext,
                                             Guid festivalId,
                                             CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(dbContext);

    IQueryable<OrderItem> itemsAtFestival = ItemsAtFestival(dbContext, festivalId);

    List<OrderItem> openItems = await itemsAtFestival
                                     .Where(item => item.SettledAtUtc == null)
                                     .ToListAsync(cancellationToken);

    List<OrderItem> givenAwayItems = await itemsAtFestival
                                          .Where(_settlementService.WasGivenAwaySince(_clock.UtcNow - _givenAwayLookback))
                                          .ToListAsync(cancellationToken);

    OpenItemOwnerLookup lookup = await LoadOwnersAsync(dbContext, [.. openItems, .. givenAwayItems], cancellationToken);

    Dictionary<string, List<OrderItem>> openByTable = GroupByTable(openItems, lookup);
    Dictionary<string, List<OrderItem>> givenAwayByTable = GroupByTable(givenAwayItems, lookup);

    return new([
                 .. openByTable.Keys
                               .Concat(givenAwayByTable.Keys)
                               .Distinct(StringComparer.Ordinal)
                               .OrderBy(tableName => tableName, StringComparer.Ordinal)
                               .Select(tableName => BuildOpenTableView(tableName,
                                                                       ItemsAtTable(openByTable, tableName),
                                                                       ItemsAtTable(givenAwayByTable, tableName),
                                                                       lookup))
               ],
               lookup.OrderItemIdsWithoutAnOrder.Count);
  }

  public IQueryable<OrderItem> ItemsAtFestival(GastronomyAppDbContext dbContext, Guid festivalId)
  {
    ArgumentNullException.ThrowIfNull(dbContext);

    IQueryable<Guid> stationOrderIdsOfAnotherFestival = dbContext.StationOrders
                                                                 .AsNoTracking()
                                                                 .Join(dbContext.Orders.AsNoTracking(),
                                                                       stationOrder => stationOrder.OrderId,
                                                                       order => order.Id,
                                                                       (stationOrder, order) => new { StationOrder = stationOrder, Order = order })
                                                                 .Where(joined => joined.Order.FestivalId != festivalId)
                                                                 .Select(joined => joined.StationOrder.Id);

    return dbContext.OrderItems
                    .AsNoTracking()
                    .Where(item => !stationOrderIdsOfAnotherFestival.Contains(item.StationOrderId));
  }

  public async Task<TableNamesView> ReadTableNamesAsync(GastronomyAppDbContext dbContext,
                                                        Guid festivalId,
                                                        CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(dbContext);

    List<string> usedNames = await dbContext.Orders
                                            .AsNoTracking()
                                            .Where(order => order.FestivalId == festivalId)
                                            .Select(order => order.TableName)
                                            .ToListAsync(cancellationToken);

    return new([
                 .. usedNames.Distinct(StringComparer.Ordinal)
                             .OrderBy(tableName => tableName, StringComparer.Ordinal)
               ]);
  }

  public async Task<IReadOnlyList<string>> LoadTableNamesForOrderItemsAsync(GastronomyAppDbContext dbContext,
                                                                            IReadOnlyCollection<Guid> orderItemIds,
                                                                            CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(dbContext);
    ArgumentNullException.ThrowIfNull(orderItemIds);

    List<Guid> ids = [.. orderItemIds];

    List<OrderItem> items = await dbContext.OrderItems
                                           .AsNoTracking()
                                           .Where(item => ids.Contains(item.Id))
                                           .ToListAsync(cancellationToken);

    IReadOnlyDictionary<Guid, string> tableNames = await TableNameByOrderItemIdAsync(dbContext, items, cancellationToken);

    return
    [
      .. tableNames.Values
                   .Distinct(StringComparer.Ordinal)
                   .OrderBy(tableName => tableName, StringComparer.Ordinal)
    ];
  }

  public async Task<IReadOnlyDictionary<Guid, string>> TableNameByOrderItemIdAsync(
    GastronomyAppDbContext dbContext,
    IReadOnlyCollection<OrderItem> items,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(dbContext);
    ArgumentNullException.ThrowIfNull(items);

    OpenItemOwnerLookup lookup = await LoadOwnersAsync(dbContext, items, cancellationToken);

    return lookup.Owners.ToDictionary(owner => owner.Key, owner => owner.Value.TableName);
  }

  private IReadOnlyCollection<OrderItem> ItemsAtTable(Dictionary<string, List<OrderItem>> byTable, string tableName)
  {
    return byTable.TryGetValue(tableName, out var items) ? items : [];
  }

  private OpenTableView BuildOpenTableView(string tableName,
                                           IReadOnlyCollection<OrderItem> openItems,
                                           IReadOnlyCollection<OrderItem> givenAwayItems,
                                           OpenItemOwnerLookup lookup)
  {
    return new(tableName,
               _settlementService.SumOpenAmountCents(openItems),
               _settlementService.SumWaivedAmountCents(givenAwayItems),
               BuildOpenItemViews(openItems, lookup),
               BuildGivenAwayItemViews(givenAwayItems, lookup));
  }

  private Dictionary<string, List<OrderItem>> GroupByTable(IEnumerable<OrderItem> items,
                                                           OpenItemOwnerLookup lookup)
  {
    return items
          .Where(item => lookup.Owners.ContainsKey(item.Id))
          .GroupBy(item => lookup.Owners[item.Id].TableName, StringComparer.Ordinal)
          .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
  }

  private IReadOnlyList<OpenOrderItemView> BuildOpenItemViews(IEnumerable<OrderItem> items,
                                                              OpenItemOwnerLookup lookup)
  {
    return
    [
      .. items
        .Select(item => new OpenOrderItemView(item.Id,
                                              lookup.Owners[item.Id].OrderId,
                                              lookup.Owners[item.Id].GlobalOrderNumber,
                                              item.ItemName,
                                              item.Note,
                                              item.UnitPriceCents,
                                              lookup.Owners[item.Id].OrderedAtUtc))
        .OrderBy(view => view.GlobalOrderNumber)
        .ThenBy(view => view.ItemName, StringComparer.Ordinal)
    ];
  }

  private IReadOnlyList<GivenAwayOrderItemView> BuildGivenAwayItemViews(IEnumerable<OrderItem> items,
                                                                        OpenItemOwnerLookup lookup)
  {
    return
    [
      .. items
        .Select(item => new GivenAwayOrderItemView(item.Id,
                                                   lookup.Owners[item.Id].OrderId,
                                                   lookup.Owners[item.Id].GlobalOrderNumber,
                                                   item.ItemName,
                                                   _settlementService.CalculateWaivedAmountCents(item),
                                                   item.PaymentNotice,
                                                   item.SettledAtUtc ?? lookup.Owners[item.Id].OrderedAtUtc))
        .OrderBy(view => view.GlobalOrderNumber)
        .ThenBy(view => view.ItemName, StringComparer.Ordinal)
    ];
  }

  private async Task<OpenItemOwnerLookup> LoadOwnersAsync(GastronomyAppDbContext dbContext,
                                                        IReadOnlyCollection<OrderItem> items,
                                                        CancellationToken cancellationToken)
  {
    List<Guid> stationOrderIds = [.. items.Select(item => item.StationOrderId).Distinct()];

    List<StationOrder> stationOrders = await dbContext.StationOrders
                                                      .AsNoTracking()
                                                      .Where(stationOrder => stationOrderIds.Contains(stationOrder.Id))
                                                      .ToListAsync(cancellationToken);

    List<Guid> orderIds = [.. stationOrders.Select(stationOrder => stationOrder.OrderId).Distinct()];

    Dictionary<Guid, Order> orders = await dbContext.Orders
                                                    .AsNoTracking()
                                                    .Where(order => orderIds.Contains(order.Id))
                                                    .ToDictionaryAsync(order => order.Id, cancellationToken);

    Dictionary<Guid, StationOrder> stationOrderById =
      stationOrders.ToDictionary(stationOrder => stationOrder.Id);

    Dictionary<Guid, OpenItemOwner> owners = [];
    List<Guid> withoutAnOrder = [];

    foreach (var item in items)
    {
      if (!stationOrderById.TryGetValue(item.StationOrderId, out var stationOrder)
          || !orders.TryGetValue(stationOrder.OrderId, out var order))
      {
        withoutAnOrder.Add(item.Id);
        continue;
      }

      owners[item.Id] = new(order.Id,
                            order.TableName,
                            order.GlobalOrderNumber,
                            order.CreatedAtUtc);
    }

    if (withoutAnOrder.Count > 0)
    {
      _logger.LogError("{ItemCount} order items cannot be traced back to an order and are therefore missing from the open items list. Order item ids: {OrderItemIds}. Station order ids: {StationOrderIds}.",
                       withoutAnOrder.Count,
                       withoutAnOrder,
                       items.Where(item => withoutAnOrder.Contains(item.Id))
                            .Select(item => item.StationOrderId)
                            .Distinct());
    }

    return new(owners, withoutAnOrder);
  }
}

public sealed class OpenItemQueryHandler
{
  private readonly IClock _clock;
  private readonly GastronomyAppDbContext _dbContext;
  private readonly IFestivalRepository _festivalRepository;
  private readonly OpenItemsReader _reader;

  public OpenItemQueryHandler(GastronomyAppDbContext dbContext,
                              OpenItemsReader reader,
                              IFestivalRepository festivalRepository,
                              IClock clock)
  {
    _dbContext = dbContext;
    _reader = reader;
    _festivalRepository = festivalRepository;
    _clock = clock;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    var festival = await _festivalRepository.FindRunningAsync(_clock.UtcNow, cancellationToken);

    return festival is null
             ? Results.Ok(new OpenItemsView([], 0))
             : Results.Ok(await _reader.ReadAsync(_dbContext, festival.Id, cancellationToken));
  }

  public async Task<IResult> ListTableNamesAsync(CancellationToken cancellationToken)
  {
    var festival = await _festivalRepository.FindRunningAsync(_clock.UtcNow, cancellationToken);

    return festival is null
             ? Results.Ok(new TableNamesView([]))
             : Results.Ok(await _reader.ReadTableNamesAsync(_dbContext, festival.Id, cancellationToken));
  }
}

public sealed class OrderItemSettlementHandler
{
  private readonly IClock _clock;
  private readonly GastronomyAppDbContext _dbContext;
  private readonly HubNotificationDispatcher _dispatcher;
  private readonly ILogger<OrderItemSettlementHandler> _logger;
  private readonly OpenItemsReader _reader;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly OrderItemSettlementService _settlementService;
  private readonly IFestivalRepository _festivalRepository;
  private readonly ITransactionRunner _transactionRunner;

  public OrderItemSettlementHandler(GastronomyAppDbContext dbContext,
                                    OrderItemSettlementService settlementService,
                                    OpenItemsReader reader,
                                    HubNotificationDispatcher dispatcher,
                                    ResultEnvelope resultEnvelope,
                                    IFestivalRepository festivalRepository,
                                    ITransactionRunner transactionRunner,
                                    IClock clock,
                                    ILogger<OrderItemSettlementHandler> logger)
  {
    _dbContext = dbContext;
    _transactionRunner = transactionRunner;
    _festivalRepository = festivalRepository;
    _settlementService = settlementService;
    _reader = reader;
    _dispatcher = dispatcher;
    _resultEnvelope = resultEnvelope;
    _clock = clock;
    _logger = logger;
  }

  public async Task<IResult> SettleAsync(SettleItemsRequest request,
                                         StaffDeviceCaller caller,
                                         CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    if (await _festivalRepository.FindRunningAsync(_clock.UtcNow, cancellationToken) is null)
    {
      SettlementFailure noFestivalIsRunning = new() { Reason = SettlementFailureReason.NoRunningFestival };
      _logger.LogWarning("A settlement from staff member {StaffMemberId} was refused because no festival is running, so nothing was settled.",
                         caller.StaffMemberId);

      return _resultEnvelope.ToResult(_resultEnvelope.BuildProblemDescription(noFestivalIsRunning));
    }

    return await ApplyAsync(new()
                            {
                              Lines =
                              [
                                .. (request.Lines ?? []).Select(line => new SettlementLine
                                                                        {
                                                                          OrderItemId = line.OrderItemId,
                                                                          PaidPriceCents = line.PaidPriceCents,
                                                                          PaymentNotice = line.PaymentNotice
                                                                        })
                              ],
                              SettledByStaffMemberId = caller.StaffMemberId
                            },
                            cancellationToken);
  }

  private async Task<IResult> ApplyAsync(SettlementRequest request, CancellationToken cancellationToken)
  {
    Result<SettlementResult, SettlementFailure> settlement =
      await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                        {
                                          IReadOnlyList<Guid> ids = _settlementService.ReadSelectedIds(request);

                                          List<OrderItem> selected = await _dbContext.OrderItems
                                                                                     .Where(item => ids.Contains(item.Id))
                                                                                     .ToListAsync(transactionCancellationToken);

                                          IReadOnlyCollection<SettlementCandidate> candidates =
                                            await LoadSettlementCandidatesAsync(selected, transactionCancellationToken);

                                          Result<SettlementResult, SettlementFailure> outcome =
                                            _settlementService.Settle(request, candidates, _clock.UtcNow);

                                          if (outcome.IsSuccess)
                                          {
                                            await _dbContext.SaveChangesAsync(transactionCancellationToken);
                                          }

                                          return new TransactionOutcome<Result<SettlementResult, SettlementFailure>>
                                                 {
                                                   Value = outcome,
                                                   ShouldCommit = outcome.IsSuccess
                                                 };
                                        },
                                        cancellationToken);

    if (!settlement.IsSuccess)
    {
      WarnAboutASettlementTheScreenCannotProduce(settlement.Failure, request.SettledByStaffMemberId);

      return _resultEnvelope.ToResult(_resultEnvelope.BuildProblemDescription(settlement.Failure));
    }

    List<Guid> settledIds = [.. settlement.Value.NewlySettled.Select(item => item.Id)];
    List<Guid> reappliedIds = [.. settlement.Value.Reapplied.Select(item => item.Id)];
    List<Guid> alreadySettledByOthersIds = [.. settlement.Value.AlreadySettledByOthers.Select(item => item.Id)];

    var otherPhonesWereTold = settledIds.Count == 0
                              || await TellTheOtherPhonesWithoutFailingTheSettlementAsync(settledIds, cancellationToken);

    return Results.Ok(new SettlementView(settledIds,
                                         reappliedIds,
                                         alreadySettledByOthersIds,
                                         otherPhonesWereTold));
  }

  private async Task<IReadOnlyCollection<SettlementCandidate>> LoadSettlementCandidatesAsync(
    IReadOnlyCollection<OrderItem> selected,
    CancellationToken cancellationToken)
  {
    IReadOnlyDictionary<Guid, string> tableNames =
      await _reader.TableNameByOrderItemIdAsync(_dbContext, selected, cancellationToken);

    return
    [
      .. selected.Where(item => tableNames.ContainsKey(item.Id))
                 .Select(item => new SettlementCandidate { Item = item, TableName = tableNames[item.Id] })
    ];
  }

  private void WarnAboutASettlementTheScreenCannotProduce(SettlementFailure failure,
                                                          Guid staffMemberId)
  {
    if (failure.Reason is SettlementFailureReason.AmountPaidMissing
                          or SettlementFailureReason.AmountPaidNegative
                          or SettlementFailureReason.DuplicateOrderItemId
                          or SettlementFailureReason.NoItemsSelected
                          or SettlementFailureReason.UnknownOrderItemId
                          or SettlementFailureReason.PaymentNoticeMissing)
    {
      _logger.LogWarning("A settlement from staff member {StaffMemberId} was refused because {Reason}. The order item it names is {OrderItemId}, and the open items screen cannot produce that, so nothing was settled.",
                         staffMemberId,
                         failure.Reason,
                         failure.OffendingOrderItemId);
      return;
    }

    if (failure.Reason is SettlementFailureReason.SelectionSpansSeveralTables)
    {
      _logger.LogWarning("A settlement from staff member {StaffMemberId} was refused because {Reason}. The items the phone sent belong to the tables {TableNames}, and the open items screen holds every other table back once one of them has something ticked, so nothing was settled.",
                         staffMemberId,
                         failure.Reason,
                         failure.TableNamesInTheSelection);
    }
  }

  private async Task<bool> TellTheOtherPhonesWithoutFailingTheSettlementAsync(IReadOnlyList<Guid> settledIds,
                                                                              CancellationToken cancellationToken)
  {
    try
    {
      IReadOnlyList<string> tableNames = await _reader.LoadTableNamesForOrderItemsAsync(_dbContext, settledIds, cancellationToken);
      await _dispatcher.PushOrderItemsSettledAsync(new(settledIds, tableNames), cancellationToken);
      return true;
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
      _logger.LogError(exception,
                       "{SettledItemCount} order items were settled and saved, but the other phones could not be told, so their open items list stays out of date until it is loaded again. Order item ids: {SettledOrderItemIds}.",
                       settledIds.Count,
                       settledIds);
      return false;
    }
  }
}
