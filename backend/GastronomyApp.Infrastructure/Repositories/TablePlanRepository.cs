using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class TablePlanRepository : ITablePlanRepository
{
  private readonly GastronomyAppDbContext _dbContext;

  public TablePlanRepository(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<Area?> FindAreaAsync(Guid festivalId, Guid areaId, CancellationToken cancellationToken)
  {
    return await _dbContext.Areas.FirstOrDefaultAsync(area => area.Id == areaId && area.FestivalId == festivalId, cancellationToken);
  }

  public async Task<Table?> FindTableAsync(Guid festivalId, Guid tableId, CancellationToken cancellationToken)
  {
    return await _dbContext.Tables.FirstOrDefaultAsync(table => table.Id == tableId && table.FestivalId == festivalId, cancellationToken);
  }

  public async Task<FestivalStationShape?> FindStationShapeAsync(Guid festivalStationId, CancellationToken cancellationToken)
  {
    return await _dbContext.FestivalStationShapes.FirstOrDefaultAsync(shape => shape.FestivalStationId == festivalStationId, cancellationToken);
  }

  public async Task AddStationShapeAsync(FestivalStationShape shape, CancellationToken cancellationToken)
  {
    await _dbContext.FestivalStationShapes.AddAsync(shape, cancellationToken);
  }

  public void RemoveStationShape(FestivalStationShape shape)
  {
    _dbContext.FestivalStationShapes.Remove(shape);
  }

  public async Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
