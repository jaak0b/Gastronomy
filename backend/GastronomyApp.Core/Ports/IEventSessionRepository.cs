using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IEventSessionRepository
{
    public Task<EventSession?> FindActiveAsync(CancellationToken cancellationToken);
}
