using GastronomyApp.Core.Entities;
using GastronomyApp.Core.ReadModels;

namespace GastronomyApp.Core.Ports;

public interface IOrderRepository
{
  public Task<Order?> FindByClientOrderIdAsync(Guid clientOrderId, CancellationToken cancellationToken);

  public Task<PlacedOrder?> FindPlacedAsync(Guid orderId, CancellationToken cancellationToken);

  public Task AddAsync(Order order, CancellationToken cancellationToken);
}
