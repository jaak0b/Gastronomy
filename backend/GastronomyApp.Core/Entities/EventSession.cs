namespace GastronomyApp.Core.Entities;

public sealed class EventSession
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public required bool IsPractice { get; set; }
    public required DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public required bool IsActive { get; set; }
}
