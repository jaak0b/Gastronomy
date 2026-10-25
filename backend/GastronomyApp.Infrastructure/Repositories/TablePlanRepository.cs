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

  public async Task<Table?> FindTableAsync(Guid festivalId, Guid tableId, CancellationToken cancellationToken)
  {
    return await _dbContext.Tables.FirstOrDefaultAsync(table => table.Id == tableId && table.FestivalId == festivalId, cancellationToken);
  }
}
