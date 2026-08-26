namespace GastronomyApp.Core.Entities;

public sealed class ServerPerson
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public required bool IsActive { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
}
