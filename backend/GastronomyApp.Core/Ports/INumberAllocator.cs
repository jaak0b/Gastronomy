namespace GastronomyApp.Core.Ports;

public interface INumberAllocator
{
    public Task<int> AllocateGlobalOrderNumberAsync(Guid eventSessionId, CancellationToken cancellationToken);

    public Task<int> AllocateLocationSequenceNumberAsync(Guid eventSessionId, Guid productionLocationId, CancellationToken cancellationToken);

    public Task<int> AllocatePrinterProcessIdAsync(string printerEndpointKey, CancellationToken cancellationToken);
}
