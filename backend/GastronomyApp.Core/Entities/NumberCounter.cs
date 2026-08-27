using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Entities;

public sealed class NumberCounter
{
    public required NumberCounterKind CounterKind { get; set; }
    public Guid? StationId { get; set; }
    public string? PrinterEndpointKey { get; set; }
    public required int NextValue { get; set; }
}
