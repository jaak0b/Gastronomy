using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class OpenItemRepository : IOpenItemRepository
{
  private readonly GastronomyAppDbContext _dbContext;
  private readonly GivenAwayItemSpecification _givenAwaySpecification;

  public OpenItemRepository(GastronomyAppDbContext dbContext, GivenAwayItemSpecification givenAwaySpecification)
  {
    _dbContext = dbContext;
    _givenAwaySpecification = givenAwaySpecification;
  }

  public async Task<IReadOnlyList<OrderItem>> FindOpenAtFestivalAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await ItemsAtFestival(festivalId).Where(item => item.SettledAtUtc == null).ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<OrderItem>> FindGivenAwayAtFestivalSinceAsync(Guid festivalId, DateTime settledFromUtc, CancellationToken cancellationToken)
  {
    return await ItemsAtFestival(festivalId).Where(_givenAwaySpecification.WasGivenAwaySince(settledFromUtc)).ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<OrderItem>> FindForSettlementAsync(IReadOnlyCollection<Guid> orderItemIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    List<Guid> ids = orderItemIds.ToList();

    return await _dbContext.OrderItems.Where(item => ids.Contains(item.Id)).ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyDictionary<Guid, OrderItemOwner>> FindOwnersAsync(IReadOnlyCollection<Guid> orderItemIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    List<Guid> ids = orderItemIds.ToList();

    return await _dbContext.OrderItems.AsNoTracking()
                           .Where(item => ids.Contains(item.Id))
                           .Join(_dbContext.StationOrders.AsNoTracking(),
                                 item => item.StationOrderId,
                                 stationOrder => stationOrder.Id,
                                 (item, stationOrder) => new
                                                         {
                                                           Item = item,
                                                           StationOrder = stationOrder
                                                         })
                           .Join(_dbContext.Orders.AsNoTracking(),
                                 joined => joined.StationOrder.OrderId,
                                 order => order.Id,
                                 (joined, order) => new
                                                    {
                                                      joined.Item,
                                                      Order = order
                                                    })
                           .ToDictionaryAsync(joined => joined.Item.Id,
                                              joined => new OrderItemOwner
                                                        {
                                                          OrderId = joined.Order.Id,
                                                          TableName = joined.Order.TableName,
                                                          GlobalOrderNumber = joined.Order.GlobalOrderNumber,
                                                          OrderedAtUtc = joined.Order.CreatedAtUtc
                                                        },
                                              cancellationToken);
  }

  public async Task<IReadOnlyList<string>> FindTableNamesAtFestivalAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    List<string> usedNames = await _dbContext.Orders.AsNoTracking().Where(order => order.FestivalId == festivalId).Select(order => order.TableName).ToListAsync(cancellationToken);

    return usedNames.Distinct(StringComparer.Ordinal).OrderBy(tableName => tableName, StringComparer.Ordinal).ToList();
  }

  public async Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }

  private IQueryable<OrderItem> ItemsAtFestival(Guid festivalId)
  {
    IQueryable<Guid> stationOrderIdsOfAnotherFestival = _dbContext.StationOrders.AsNoTracking()
                                                                  .Join(_dbContext.Orders.AsNoTracking(),
                                                                        stationOrder => stationOrder.OrderId,
                                                                        order => order.Id,
                                                                        (stationOrder, order) => new
                                                                                                 {
                                                                                                   StationOrder = stationOrder,
                                                                                                   Order = order
                                                                                                 })
                                                                  .Where(joined => joined.Order.FestivalId != festivalId)
                                                                  .Select(joined => joined.StationOrder.Id);

    return _dbContext.OrderItems.AsNoTracking().Where(item => !stationOrderIdsOfAnotherFestival.Contains(item.StationOrderId));
  }
}
