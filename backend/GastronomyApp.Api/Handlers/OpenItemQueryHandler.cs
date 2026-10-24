using ErrorOr;
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

  public async Task<ApiAnswer<TableOrderReportView>> ReadTableAsync(Guid tableId, CancellationToken cancellationToken)
  {
    return await _service.ReadTableWithOrdersAsync(tableId, cancellationToken).Then(ToTableOrderReportView);
  }

  private TableOrderReportView ToTableOrderReportView(Table table)
  {
    List<OrderItem> positions = table.Orders.SelectMany(order => order.StationOrders).SelectMany(stationOrder => stationOrder.Items).ToList();

    return new(table.Id, table.Name, _settlementService.SumOpenAmountCents(positions), _mapper.Map<IReadOnlyList<TableOrderRecordView>>(table.Orders));
  }
}
