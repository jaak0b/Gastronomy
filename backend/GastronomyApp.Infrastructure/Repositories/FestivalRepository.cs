using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class FestivalRepository : IFestivalRepository
{
  private readonly GastronomyAppDbContext _dbContext;
  private readonly FestivalSchedule _schedule;

  public FestivalRepository(GastronomyAppDbContext dbContext, FestivalSchedule schedule)
  {
    _dbContext = dbContext;
    _schedule = schedule;
  }

  public async Task<Festival?> FindRunningAsync(DateTime nowUtc, CancellationToken cancellationToken)
  {
    List<Festival> shown = await _dbContext.Festivals
                                           .AsNoTracking()
                                           .Where(festival => !festival.IsHidden)
                                           .ToListAsync(cancellationToken);

    return _schedule.FindRunningAt(shown, nowUtc);
  }

  public async Task<IReadOnlyCollection<Festival>> FindAllAsync(CancellationToken cancellationToken)
  {
    return await _dbContext.Festivals
                           .AsNoTracking()
                           .OrderByDescending(festival => festival.StartsAtUtc)
                           .ToListAsync(cancellationToken);
  }

  public async Task<bool> ExistsAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _dbContext.Festivals
                           .AsNoTracking()
                           .AnyAsync(festival => festival.Id == festivalId, cancellationToken);
  }

  public async Task<IReadOnlyList<Guid>> FindIdsNotEndedAsync(DateTime nowUtc, CancellationToken cancellationToken)
  {
    return await _dbContext.Festivals
                           .AsNoTracking()
                           .Where(festival => !festival.IsHidden && festival.EndsAtUtc > nowUtc)
                           .Select(festival => festival.Id)
                           .ToListAsync(cancellationToken);
  }
}
