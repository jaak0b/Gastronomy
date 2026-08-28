using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IOrderRepository
{
  public Task<Order?> FindByClientOrderIdAsync(Guid clientOrderId, CancellationToken cancellationToken);

  public Task AddAsync(Order order, CancellationToken cancellationToken);
}
