namespace GastronomyApp.Core.Entities;

public sealed class PrinterStatus
{
    public required Guid ProductionLocationId { get; set; }
    public required bool IsOnline { get; set; }
    public required bool IsPaperEnd { get; set; }
    public required bool IsPaperNearEnd { get; set; }
    public required bool IsCoverOpen { get; set; }
    public required bool IsInErrorState { get; set; }
    public required bool IsFaulty { get; set; }
    public required string LastDetail { get; set; }
    public required DateTime LastChangedAtUtc { get; set; }
    public required DateTime LastHeardFromAtUtc { get; set; }
}
