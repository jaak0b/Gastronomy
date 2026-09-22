using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.OpenItems;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using MapsterMapper;

namespace GastronomyApp.Api.Handlers;

public sealed class OpenItemQueryHandler
{
  private readonly IMapper _mapper;
  private readonly OpenItemsService _service;
  private readonly OrderItemSettlementService _settlementService;

  public OpenItemQueryHandler(OpenItemsService service, OrderItemSettlementService settlementService, IMapper mapper)
  {
    _service = service;
    _settlementService = settlementService;
    _mapper = mapper;
  }

  public async Task<ApiAnswer<OpenItemsView>> ListAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<OrderItem> openItems = await _service.ReadOpenItemsAsync(cancellationToken);

    IReadOnlyList<Guid> orderItemIdsWithoutAnOrder = _service.ItemIdsWithoutAnOrder(openItems);

    List<OpenTableView> tables = _service.GroupItemsWithAKnownOrderByTableName(openItems).Select(ToOpenTableView).ToList();

    return new OpenItemsView(tables, orderItemIdsWithoutAnOrder.Count);
  }

  public async Task<ApiAnswer<TableNamesView>> ListTableNamesAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<string> tableNames = await _service.ReadTableNamesAsync(cancellationToken);

    return new TableNamesView(tableNames);
  }

  public async Task<ApiAnswer<TableOrderReportView>> ReadTableAsync(string? tableName, CancellationToken cancellationToken)
  {
    var readTableName = tableName ?? string.Empty;

    IReadOnlyList<Order> orders = await _service.ReadTableOrdersAsync(readTableName, cancellationToken);

    List<OrderItem> positions = orders.SelectMany(order => order.StationOrders).SelectMany(stationOrder => stationOrder.Items).ToList();

    return new TableOrderReportView(readTableName, _settlementService.SumOpenAmountCents(positions), _mapper.Map<IReadOnlyList<TableOrderRecordView>>(orders));
  }

  private OpenTableView ToOpenTableView(IGrouping<string, OrderItem> table)
  {
    List<OrderItem> openItems = table.ToList();

    return new(table.Key, _settlementService.SumOpenAmountCents(openItems), _mapper.Map<IReadOnlyList<OpenOrderItemView>>(openItems));
  }
}
