using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IOpenItemRepository
{
  public Task<IReadOnlyList<OrderItem>> FindOpenAtFestivalAsync(Guid festivalId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<OrderItem>> FindForSettlementAsync(IReadOnlyCollection<Guid> orderItemIds, CancellationToken cancellationToken);

  public Task<IReadOnlyList<string>> FindTableNamesAtFestivalAsync(Guid festivalId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<Order>> FindTableOrdersAsync(Guid festivalId, string tableName, CancellationToken cancellationToken);

  public Task SaveChangesAsync(CancellationToken cancellationToken);
}
