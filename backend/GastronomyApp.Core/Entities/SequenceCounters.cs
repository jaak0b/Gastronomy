namespace GastronomyApp.Core.Entities;

public sealed class SequenceCounters
{
    public required int Id { get; set; }
    public required int NextOrderNumber { get; set; }
    public required int NextPrinterJobId { get; set; }
}
