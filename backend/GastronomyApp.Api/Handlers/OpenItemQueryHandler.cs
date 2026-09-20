using GastronomyApp.Api.Contracts;
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

  public OpenItemQueryHandler(OpenItemsService service, ILogger<OpenItemQueryHandler> logger, IMapper mapper)
  {
    _service = service;
    _logger = logger;
    _mapper = mapper;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    var report = await _service.ReadAsync(cancellationToken);

    if (report.OrderItemIdsWithoutAnOrder.Count > 0)
      _logger.LogError("{ItemCount} order items cannot be traced back to an order and are therefore missing from the open items list. Order item ids: {OrderItemIds}.", report.OrderItemIdsWithoutAnOrder.Count, report.OrderItemIdsWithoutAnOrder);

    return Results.Ok(new OpenItemsView(_mapper.Map<IReadOnlyList<OpenTableView>>(report.Tables), report.OrderItemIdsWithoutAnOrder.Count));
  }

  public async Task<IResult> ListTableNamesAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<string> tableNames = await _service.ReadTableNamesAsync(cancellationToken);

    return Results.Ok(new TableNamesView(tableNames));
  }
}
