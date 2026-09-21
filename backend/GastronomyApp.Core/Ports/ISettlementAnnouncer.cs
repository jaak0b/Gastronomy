namespace GastronomyApp.Core.Ports;

public interface ISettlementAnnouncer
{
  public Task<bool> AnnounceOrderItemsSettledAsync(IReadOnlyList<Guid> settledOrderItemIds, IReadOnlyList<string> tableNames, CancellationToken cancellationToken);
}
