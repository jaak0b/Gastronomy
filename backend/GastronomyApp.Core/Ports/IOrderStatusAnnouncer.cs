using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IOrderStatusAnnouncer
{
  public Task AnnounceOrderStatusChangedAsync(Order order, CancellationToken cancellationToken);
}
