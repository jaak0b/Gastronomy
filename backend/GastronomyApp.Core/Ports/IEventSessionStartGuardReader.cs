using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Ports;

public interface IEventSessionStartGuardReader
{
    public Task<IReadOnlyCollection<LocationTicketStatus>> FindTicketStatusesAsync(Guid eventSessionId, CancellationToken cancellationToken);

    public Task<DateTime?> FindMostRecentOrderAcceptedAtUtcAsync(Guid eventSessionId, CancellationToken cancellationToken);

    public Task<IReadOnlyCollection<ProductionLocation>> FindActiveLocationsOnTestPrinterAsync(CancellationToken cancellationToken);
}
