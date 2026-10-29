using GastronomyApp.Core.Announcements;

namespace GastronomyApp.Core.Entities;

[Raises]
public sealed class Device : IEntity
{
  public required Guid Id { get; set; }

  public required string Language { get; set; }

  public required byte[] TokenHash { get; set; }

  public required byte[] TokenSalt { get; set; }

  public required string TokenAlgorithm { get; set; }

  public required string TokenLookupId { get; set; }

  public required DateTime CreatedAtUtc { get; set; }
}
