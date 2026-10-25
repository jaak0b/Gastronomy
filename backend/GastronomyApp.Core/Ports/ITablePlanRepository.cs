using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface ITablePlanRepository
{
  public Task<Table?> FindTableAsync(Guid festivalId, Guid tableId, CancellationToken cancellationToken);
}
