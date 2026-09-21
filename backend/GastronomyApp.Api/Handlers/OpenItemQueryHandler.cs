using GastronomyApp.Contracts;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Handlers;

public sealed class OpenItemQueryHandler
{
  private readonly ILogger<OpenItemQueryHandler> _logger;
  private readonly IMapper _mapper;
  private readonly OpenItemsService _service;
  private readonly OrderItemSettlementService _settlementService;

  public OpenItemQueryHandler(OpenItemsService service, OrderItemSettlementService settlementService, ILogger<OpenItemQueryHandler> logger, IMapper mapper)
  {
    _service = service;
    _settlementService = settlementService;
    _logger = logger;
    _mapper = mapper;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<OrderItem> openItems = await _service.ReadOpenItemsAsync(cancellationToken);

    IReadOnlyList<Guid> orderItemIdsWithoutAnOrder = _service.ItemIdsWithoutAnOrder(openItems);

    if (orderItemIdsWithoutAnOrder.Count > 0)
      _logger.LogError("{ItemCount} order items cannot be traced back to an order and are therefore missing from the open items list. Order item ids: {OrderItemIds}.", orderItemIdsWithoutAnOrder.Count, orderItemIdsWithoutAnOrder);

    List<OpenTableView> tables = _service.GroupItemsWithAKnownOrderByTableName(openItems).Select(ToOpenTableView).ToList();

    return Results.Ok(new OpenItemsView(tables, orderItemIdsWithoutAnOrder.Count));
  }

  public async Task<IResult> ListTableNamesAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<string> tableNames = await _service.ReadTableNamesAsync(cancellationToken);

    return Results.Ok(new TableNamesView(tableNames));
  }

  public async Task<IResult> ReadTableAsync(string? tableName, CancellationToken cancellationToken)
  {
    var readTableName = tableName ?? string.Empty;

    IReadOnlyList<Order> orders = await _service.ReadTableOrdersAsync(readTableName, cancellationToken);

    List<OrderItem> positions = orders.SelectMany(order => order.StationOrders).SelectMany(stationOrder => stationOrder.Items).ToList();

    return Results.Ok(new TableOrderReportView(readTableName, _settlementService.SumOpenAmountCents(positions), _mapper.Map<IReadOnlyList<TableOrderRecordView>>(orders)));
  }

  private OpenTableView ToOpenTableView(IGrouping<string, OrderItem> table)
  {
    List<OrderItem> openItems = table.ToList();

    return new(table.Key, _settlementService.SumOpenAmountCents(openItems), _mapper.Map<IReadOnlyList<OpenOrderItemView>>(openItems));
  }
}
