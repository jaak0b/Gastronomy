using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IUnitRepository
{
  public Task<IReadOnlyList<Unit>> FindAllWithIngredientsOrderedByNameAsync(CancellationToken cancellationToken);

  public Task<IReadOnlyList<Unit>> FindAllOrderedByNameAsync(CancellationToken cancellationToken);

  public Task<Unit?> FindByIdAsync(Guid unitId, CancellationToken cancellationToken);

  public Task<Unit?> FindWithIngredientsAsync(Guid unitId, CancellationToken cancellationToken);

  public Task AddAsync(Unit unit, CancellationToken cancellationToken);

  public Task SaveChangesAsync(CancellationToken cancellationToken);
}
