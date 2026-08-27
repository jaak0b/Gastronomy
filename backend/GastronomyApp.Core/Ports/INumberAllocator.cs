namespace GastronomyApp.Core.Ports;

public interface INumberAllocator
{
    public Task<int> AllocateGlobalOrderNumberAsync(CancellationToken cancellationToken);

    public Task<int> AllocateStationSequenceNumberAsync(Guid stationId, CancellationToken cancellationToken);

    public Task<int> AllocatePrinterProcessIdAsync(string printerEndpointKey, CancellationToken cancellationToken);

    public Task ResetOrderAndSlipNumbersAsync(CancellationToken cancellationToken);
}
