using GastronomyApp.Core.Ports;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class SequenceNumberAllocator : INumberAllocator
{
  private readonly GastronomyAppDbContext _dbContext;

  public SequenceNumberAllocator(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<int> AllocateGlobalOrderNumberAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    var festival = await _dbContext.Festivals
                                   .FirstAsync(candidate => candidate.Id == festivalId, cancellationToken);

    var allocatedValue = festival.NextOrderNumber;
    festival.NextOrderNumber = allocatedValue + 1;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return allocatedValue;
  }

  public async Task<int> AllocateStationOrderNumberAsync(Guid festivalId,
                                                         Guid stationId,
                                                         CancellationToken cancellationToken)
  {
    var link = await _dbContext.FestivalStations
                               .FirstAsync(candidate => candidate.FestivalId == festivalId
                                                        && candidate.StationId == stationId,
                                           cancellationToken);

    var allocatedValue = link.NextStationOrderNumber;
    link.NextStationOrderNumber = allocatedValue + 1;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return allocatedValue;
  }
}
