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

    return routes;
  }
}

public sealed class AdminOrderHandler
{
  private const int DefaultLimit = 200;

  private readonly GastronomyAppDbContext _dbContext;
  private readonly OrderReader _orderReader;

  public AdminOrderHandler(GastronomyAppDbContext dbContext, OrderReader orderReader)
  {
    _dbContext = dbContext;
    _orderReader = orderReader;
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

    var filtersByStatus = Enum.TryParse(status, true, out OrderStatus parsedStatus);

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

      entries.Add(_orderReader.DescribeListEntry(loaded));
    }

    return Results.Ok(new OrderListView(entries));
  }
}
