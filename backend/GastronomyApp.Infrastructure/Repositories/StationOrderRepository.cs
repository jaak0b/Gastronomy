using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class StationOrderRepository : IStationOrderRepository
{
  private readonly GastronomyAppDbContext _dbContext;

  public StationOrderRepository(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<Station?> FindStationWithUnfinishedOrdersAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    return await _dbContext.Stations.AsNoTracking()
                           .Where(station => station.Id == stationId)
                           .Include(station => station.StationOrders.Where(stationOrder => stationOrder.FestivalId == festivalId && stationOrder.Items.Any(item => item.FulfilledAtUtc == null)))
                           .ThenInclude(stationOrder => stationOrder.Items)
                           .Include(station => station.StationOrders.Where(stationOrder => stationOrder.FestivalId == festivalId && stationOrder.Items.Any(item => item.FulfilledAtUtc == null)))
                           .ThenInclude(stationOrder => stationOrder.Order)
                           .ThenInclude(order => order.StaffMember)
                           .FirstOrDefaultAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<StationOrder>> FindFulfilledAtStationAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    return await _dbContext.StationOrders.AsNoTracking()
                           .Where(stationOrder => stationOrder.StationId == stationId && stationOrder.FestivalId == festivalId && stationOrder.Items.Any(item => item.FulfilledAtUtc != null))
                           .Include(stationOrder => stationOrder.Items)
                           .Include(stationOrder => stationOrder.Order)
                           .ThenInclude(order => order.StaffMember)
                           .OrderBy(stationOrder => stationOrder.StationOrderNumber)
                           .ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<OrderItem>> FindItemsAtStationAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    List<Guid> ids = orderItemIds.ToList();

    return await _dbContext.OrderItems.Where(item => ids.Contains(item.Id) && item.StationOrder.StationId == stationId).Include(item => item.StationOrder).ToListAsync(cancellationToken);
  }

  public async Task<StationOrder?> FindAtStationAsync(Guid stationOrderId, Guid stationId, Guid festivalId, CancellationToken cancellationToken)
  {
    return await _dbContext.StationOrders.FirstOrDefaultAsync(stationOrder => stationOrder.Id == stationOrderId && stationOrder.StationId == stationId && stationOrder.FestivalId == festivalId, cancellationToken);
  }

  public async Task<IReadOnlyList<Guid>> FindOrderIdsOfStationOrdersAsync(IReadOnlyCollection<Guid> stationOrderIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(stationOrderIds);

    List<Guid> ids = stationOrderIds.ToList();

    return await _dbContext.StationOrders.AsNoTracking().Where(stationOrder => ids.Contains(stationOrder.Id)).Select(stationOrder => stationOrder.OrderId).Distinct().ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<Order>> FindOrdersWithItemsAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderIds);

    List<Guid> ids = orderIds.ToList();

    return await _dbContext.Orders.AsNoTracking().Where(order => ids.Contains(order.Id)).Include(order => order.StationOrders).ThenInclude(stationOrder => stationOrder.Items).OrderBy(order => order.GlobalOrderNumber).ToListAsync(cancellationToken);
  }

  public async Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
