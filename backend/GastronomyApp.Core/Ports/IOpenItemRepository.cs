using GastronomyApp.Core.Entities;
using GastronomyApp.Core.ReadModels;

namespace GastronomyApp.Core.Ports;

public interface IOpenItemRepository
{
  public Task<IReadOnlyList<OrderItem>> FindOpenAtFestivalAsync(Guid festivalId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<OrderItem>> FindGivenAwayAtFestivalSinceAsync(Guid festivalId,
                                                                          DateTime settledFromUtc,
                                                                          CancellationToken cancellationToken);

  public Task<IReadOnlyList<OrderItem>> FindForSettlementAsync(IReadOnlyCollection<Guid> orderItemIds,
                                                                CancellationToken cancellationToken);

  public Task<IReadOnlyDictionary<Guid, OrderItemOwner>> FindOwnersAsync(IReadOnlyCollection<Guid> orderItemIds,
                                                                          CancellationToken cancellationToken);

  public Task<IReadOnlyList<string>> FindTableNamesAtFestivalAsync(Guid festivalId,
                                                                    CancellationToken cancellationToken);

  public Task SaveChangesAsync(CancellationToken cancellationToken);
}
