using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class UnitRepository : IUnitRepository
{
  private readonly GastronomyAppDbContext _dbContext;

  public UnitRepository(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<IReadOnlyList<Unit>> FindAllWithIngredientsOrderedByNameAsync(CancellationToken cancellationToken)
  {
    return await _dbContext.Units.AsNoTracking().Include(unit => unit.Ingredients).OrderBy(unit => unit.Name).ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<Unit>> FindAllOrderedByNameAsync(CancellationToken cancellationToken)
  {
    return await _dbContext.Units.AsNoTracking().OrderBy(unit => unit.Name).ToListAsync(cancellationToken);
  }

  public async Task<Unit?> FindByIdAsync(Guid unitId, CancellationToken cancellationToken)
  {
    return await _dbContext.Units.FirstOrDefaultAsync(unit => unit.Id == unitId, cancellationToken);
  }

  public async Task<Unit?> FindWithIngredientsAsync(Guid unitId, CancellationToken cancellationToken)
  {
    return await _dbContext.Units.Include(unit => unit.Ingredients).FirstOrDefaultAsync(unit => unit.Id == unitId, cancellationToken);
  }

  public async Task AddAsync(Unit unit, CancellationToken cancellationToken)
  {
    await _dbContext.Units.AddAsync(unit, cancellationToken);
  }

  public async Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
