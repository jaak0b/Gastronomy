using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public static class AdminOrderEndpoints
{
  public static IEndpointRouteBuilder MapAdminOrderEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/orders");

    group.MapGet(string.Empty,
                 async (string? status,
                        Guid? stationId,
                        AdminOrderHandler handler,
                        CancellationToken cancellationToken) => await handler.ListAsync(status, stationId, cancellationToken));

    group.MapPost("/{orderId:guid}/station-orders/{stationOrderId:guid}/resolve",
                  async (Guid orderId,
                         Guid stationOrderId,
                         ResolveUnknownPrintRequest request,
                         StationOrderActionHandler handler,
                         CancellationToken cancellationToken) =>
                    await handler.ResolveUnknownAsync(orderId, stationOrderId, request, null, cancellationToken));

    group.MapPost("/{orderId:guid}/station-orders/{stationOrderId:guid}/print-another-copy",
                  async (Guid orderId,
                         Guid stationOrderId,
                         StationOrderActionHandler handler,
                         CancellationToken cancellationToken) =>
                    await handler.PrintAnotherCopyAsync(orderId, stationOrderId, null, cancellationToken));

    return routes;
  }
}

public sealed class AdminOrderHandler
{
  private const int DefaultLimit = 200;

  private readonly GastronomyAppDbContext _dbContext;
  private readonly OrderQueryHandler _orderQueryHandler;
  private readonly OrderReader _orderReader;

  public AdminOrderHandler(GastronomyAppDbContext dbContext,
                           OrderReader orderReader,
                           OrderQueryHandler orderQueryHandler)
  {
    _dbContext = dbContext;
    _orderReader = orderReader;
    _orderQueryHandler = orderQueryHandler;
  }

  public async Task<IResult> ListAsync(string? status,
                                       Guid? stationId,
                                       CancellationToken cancellationToken)
  {
    IQueryable<Order> query = _dbContext.Orders.AsNoTracking();

    if (stationId is not null)
    {
      List<Guid> orderIdsAtStation = await _dbContext.StationOrders
                                                    .AsNoTracking()
                                                    .Where(stationOrder => stationOrder.StationId == stationId)
                                                    .Select(stationOrder => stationOrder.OrderId)
                                                    .Distinct()
                                                    .ToListAsync(cancellationToken);

      query = query.Where(order => orderIdsAtStation.Contains(order.Id));
    }

    var filtersByStatus = Enum.TryParse(status, out OrderStatus parsedStatus);

    List<Order> orders = await query
                              .OrderByDescending(order => order.CreatedAtUtc)
                              .Take(DefaultLimit)
                              .ToListAsync(cancellationToken);

    List<OrderListEntryView> entries = [];

    foreach (var order in orders)
    {
      var loaded = (await _orderReader.LoadAsync(_dbContext, order.Id, cancellationToken))!;

      if (filtersByStatus && _orderReader.StatusOf(loaded) != parsedStatus)
      {
        continue;
      }

      entries.Add(await _orderQueryHandler.DescribeListEntryAsync(loaded, cancellationToken));
    }

    return Results.Ok(new OrderListView(entries));
  }
}
