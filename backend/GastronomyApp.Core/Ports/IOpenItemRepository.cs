using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IOpenItemRepository
{
  public Task<IReadOnlyList<OrderItem>> FindForSettlementAsync(IReadOnlyCollection<Guid> orderItemIds, CancellationToken cancellationToken);

  public Task<Table?> FindTableWithOrdersAsync(Guid festivalId, Guid tableId, CancellationToken cancellationToken);

  public Task SaveChangesAsync(CancellationToken cancellationToken);
}
