using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Endpoints;

public sealed class OpenItemQueryHandler
{
  private readonly ILogger<OpenItemQueryHandler> _logger;
  private readonly OpenItemsService _service;

  public OpenItemQueryHandler(OpenItemsService service, ILogger<OpenItemQueryHandler> logger)
  {
    _service = service;
    _logger = logger;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    OpenItemsReport report = await _service.ReadAsync(cancellationToken);

    if (report.OrderItemIdsWithoutAnOrder.Count > 0)
    {
      _logger.LogError("{ItemCount} order items cannot be traced back to an order and are therefore missing from the open items list. Order item ids: {OrderItemIds}.",
                       report.OrderItemIdsWithoutAnOrder.Count,
                       report.OrderItemIdsWithoutAnOrder);
    }

    return Results.Ok(new OpenItemsView([.. report.Tables.Select(BuildOpenTableView)],
                                        report.OrderItemIdsWithoutAnOrder.Count));
  }

  public async Task<IResult> ListTableNamesAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<string> tableNames = await _service.ReadTableNamesAsync(cancellationToken);

    return Results.Ok(new TableNamesView(tableNames));
  }

  private OpenTableView BuildOpenTableView(OpenTable table)
  {
    return new(table.TableName,
               table.OpenAmountCents,
               table.GivenAwayAmountCents,
               [.. table.Items.Select(BuildOpenOrderItemView)],
               [.. table.GivenAwayItems.Select(BuildGivenAwayOrderItemView)]);
  }

  private OpenOrderItemView BuildOpenOrderItemView(OpenOrderItem item)
  {
    return new(item.OrderItemId,
               item.OrderId,
               item.GlobalOrderNumber,
               item.ItemName,
               item.Note,
               item.UnitPriceCents,
               item.OrderedAtUtc);
  }

  private GivenAwayOrderItemView BuildGivenAwayOrderItemView(GivenAwayOrderItem item)
  {
    return new(item.OrderItemId,
               item.OrderId,
               item.GlobalOrderNumber,
               item.ItemName,
               item.WaivedAmountCents,
               item.PaymentNotice,
               item.SettledAtUtc);
  }
}
