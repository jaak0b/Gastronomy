using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IProductionLocationRepository
{
    public Task<IReadOnlyCollection<ProductionLocation>> FindActiveAsync(CancellationToken cancellationToken);
}
