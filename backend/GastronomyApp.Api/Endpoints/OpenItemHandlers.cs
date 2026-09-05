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

public sealed record OpenItemOwner(Guid OrderId, string TableName, int GlobalOrderNumber, DateTime OrderedAtUtc);

public sealed record OpenItemOwnerLookup(
  IReadOnlyDictionary<Guid, OpenItemOwner> Owners,
  IReadOnlyList<Guid> OrderItemIdsWithoutAnOrder);

public sealed class OpenItemsReader
{
  private const int TableNamesFromTheMostRecentOrders = 200;

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

  public async Task<OpenItemsView> ReadAsync(GastronomyAppDbContext dbContext, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(dbContext);

    List<OrderItem> openItems = await dbContext.OrderItems
                                               .AsNoTracking()
                                               .Where(item => item.SettledAtUtc == null)
                                               .ToListAsync(cancellationToken);

    List<OrderItem> givenAwayItems = await dbContext.OrderItems
                                                    .AsNoTracking()
                                                    .Where(_settlementService.WasGivenAwaySince(_clock.UtcNow - _givenAwayLookback))
                                                    .ToListAsync(cancellationToken);

    OpenItemOwnerLookup lookup = await OwnersOfAsync(dbContext, [.. openItems, .. givenAwayItems], cancellationToken);

    Dictionary<string, List<OrderItem>> openByTable = GroupByTable(openItems, lookup);
    Dictionary<string, List<OrderItem>> givenAwayByTable = GroupByTable(givenAwayItems, lookup);

    return new([
                 .. openByTable.Keys
                               .Concat(givenAwayByTable.Keys)
                               .Distinct(StringComparer.Ordinal)
                               .OrderBy(tableName => tableName, StringComparer.Ordinal)
                               .Select(tableName => TableOf(tableName,
                                                            ItemsOf(openByTable, tableName),
                                                            ItemsOf(givenAwayByTable, tableName),
                                                            lookup))
               ],
               lookup.OrderItemIdsWithoutAnOrder.Count);
  }

  public async Task<TableNamesView> ReadTableNamesAsync(GastronomyAppDbContext dbContext,
                                                        CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(dbContext);

    List<string> recentlyUsed = await dbContext.Orders
                                               .AsNoTracking()
                                               .OrderByDescending(order => order.GlobalOrderNumber)
                                               .Select(order => order.TableName)
                                               .Take(TableNamesFromTheMostRecentOrders)
                                               .ToListAsync(cancellationToken);

    return new([
                 .. recentlyUsed.Distinct(StringComparer.Ordinal)
                                .OrderBy(tableName => tableName, StringComparer.Ordinal)
               ]);
  }

  public async Task<IReadOnlyList<string>> TableNamesOfAsync(GastronomyAppDbContext dbContext,
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

    OpenItemOwnerLookup lookup = await OwnersOfAsync(dbContext, items, cancellationToken);

    return
    [
      .. lookup.Owners
               .Values
               .Select(owner => owner.TableName)
               .Distinct(StringComparer.Ordinal)
               .OrderBy(tableName => tableName, StringComparer.Ordinal)
    ];
  }

  private IReadOnlyCollection<OrderItem> ItemsOf(Dictionary<string, List<OrderItem>> byTable, string tableName)
  {
    return byTable.TryGetValue(tableName, out var items) ? items : [];
  }

  private OpenTableView TableOf(string tableName,
                                IReadOnlyCollection<OrderItem> openItems,
                                IReadOnlyCollection<OrderItem> givenAwayItems,
                                OpenItemOwnerLookup lookup)
  {
    return new(tableName,
               _settlementService.OpenAmountCentsOf(openItems),
               _settlementService.WaivedAmountCentsOf(givenAwayItems),
               DescribeOpenItems(openItems, lookup),
               DescribeGivenAwayItems(givenAwayItems, lookup));
  }

  private Dictionary<string, List<OrderItem>> GroupByTable(IEnumerable<OrderItem> items,
                                                           OpenItemOwnerLookup lookup)
  {
    return items
          .Where(item => lookup.Owners.ContainsKey(item.Id))
          .GroupBy(item => lookup.Owners[item.Id].TableName, StringComparer.Ordinal)
          .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
  }

  private IReadOnlyList<OpenOrderItemView> DescribeOpenItems(IEnumerable<OrderItem> items,
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

  private IReadOnlyList<GivenAwayOrderItemView> DescribeGivenAwayItems(IEnumerable<OrderItem> items,
                                                                       OpenItemOwnerLookup lookup)
  {
    return
    [
      .. items
        .Select(item => new GivenAwayOrderItemView(item.Id,
                                                   lookup.Owners[item.Id].OrderId,
                                                   lookup.Owners[item.Id].GlobalOrderNumber,
                                                   item.ItemName,
                                                   _settlementService.WaivedAmountCentsOf(item),
                                                   item.PaymentNotice,
                                                   item.SettledAtUtc ?? lookup.Owners[item.Id].OrderedAtUtc))
        .OrderBy(view => view.GlobalOrderNumber)
        .ThenBy(view => view.ItemName, StringComparer.Ordinal)
    ];
  }

  private async Task<OpenItemOwnerLookup> OwnersOfAsync(GastronomyAppDbContext dbContext,
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

    Dictionary<Guid, Guid> orderIdByStationOrderId =
      stationOrders.ToDictionary(stationOrder => stationOrder.Id, stationOrder => stationOrder.OrderId);

    Dictionary<Guid, OpenItemOwner> owners = [];
    List<Guid> withoutAnOrder = [];

    foreach (var item in items)
    {
      if (!orderIdByStationOrderId.TryGetValue(item.StationOrderId, out var orderId)
          || !orders.TryGetValue(orderId, out var order))
      {
        withoutAnOrder.Add(item.Id);
        continue;
      }

      owners[item.Id] = new(order.Id, order.TableName, order.GlobalOrderNumber, order.CreatedAtUtc);
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
  private readonly GastronomyAppDbContext _dbContext;
  private readonly OpenItemsReader _reader;

  public OpenItemQueryHandler(GastronomyAppDbContext dbContext, OpenItemsReader reader)
  {
    _dbContext = dbContext;
    _reader = reader;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    return Results.Ok(await _reader.ReadAsync(_dbContext, cancellationToken));
  }

  public async Task<IResult> ListTableNamesAsync(CancellationToken cancellationToken)
  {
    return Results.Ok(await _reader.ReadTableNamesAsync(_dbContext, cancellationToken));
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
  private readonly ImmediateTransactionRunner _transactionRunner = new();

  public OrderItemSettlementHandler(GastronomyAppDbContext dbContext,
                                    OrderItemSettlementService settlementService,
                                    OpenItemsReader reader,
                                    HubNotificationDispatcher dispatcher,
                                    ResultEnvelope resultEnvelope,
                                    IClock clock,
                                    ILogger<OrderItemSettlementHandler> logger)
  {
    _dbContext = dbContext;
    _settlementService = settlementService;
    _reader = reader;
    _dispatcher = dispatcher;
    _resultEnvelope = resultEnvelope;
    _clock = clock;
    _logger = logger;
  }

  public Task<IResult> SettleAtTheDisplayedPriceAsync(SettleItemsRequest request,
                                                      DeviceCaller caller,
                                                      CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    return ApplyAsync(new()
                      {
                        Kind = SettlementKind.AtTheDisplayedPrice,
                        OrderItemIds = request.OrderItemIds ?? [],
                        SettledByStaffMemberId = caller.StaffMemberId
                      },
                      cancellationToken);
  }

  public Task<IResult> SettleFreeOfChargeAsync(SettleItemsFreeOfChargeRequest request,
                                               DeviceCaller caller,
                                               CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    return ApplyAsync(new()
                      {
                        Kind = SettlementKind.FreeOfCharge,
                        OrderItemIds = request.OrderItemIds ?? [],
                        SettledByStaffMemberId = caller.StaffMemberId,
                        PaymentNotice = request.PaymentNotice
                      },
                      cancellationToken);
  }

  private async Task<IResult> ApplyAsync(SettlementRequest request, CancellationToken cancellationToken)
  {
    Result<SettlementResult, SettlementFailure> settlement =
      await _transactionRunner.RunAsync(_dbContext,
                                        async transactionCancellationToken =>
                                        {
                                          IReadOnlyList<Guid> ids = _settlementService.SelectedIdsOf(request);

                                          List<OrderItem> selected = await _dbContext.OrderItems
                                                                                     .Where(item => ids.Contains(item.Id))
                                                                                     .ToListAsync(transactionCancellationToken);

                                          Result<SettlementResult, SettlementFailure> outcome =
                                            _settlementService.Settle(request, selected, _clock.UtcNow);

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
      return _resultEnvelope.ToResult(_resultEnvelope.Describe(settlement.Failure));
    }

    List<Guid> settledIds = [.. settlement.Value.NewlySettled.Select(item => item.Id)];
    List<Guid> alreadySettledIds = [.. settlement.Value.AlreadySettledBeforehand.Select(item => item.Id)];

    var otherPhonesWereTold = settledIds.Count == 0
                              || await TellTheOtherPhonesWithoutFailingTheSettlementAsync(settledIds, cancellationToken);

    return Results.Ok(new SettlementView(settledIds, alreadySettledIds, otherPhonesWereTold));
  }

  private async Task<bool> TellTheOtherPhonesWithoutFailingTheSettlementAsync(IReadOnlyList<Guid> settledIds,
                                                                              CancellationToken cancellationToken)
  {
    try
    {
      IReadOnlyList<string> tableNames = await _reader.TableNamesOfAsync(_dbContext, settledIds, cancellationToken);
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
