using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class ProductionLocationRepository : IProductionLocationRepository
{
    private readonly GastronomyAppDbContext _dbContext;

    public ProductionLocationRepository(GastronomyAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<ProductionLocation>> FindActiveAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.ProductionLocations
            .Where(location => location.IsActive)
            .OrderBy(location => location.SortOrder)
            .ToListAsync(cancellationToken);
    }
}
