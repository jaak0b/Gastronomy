namespace GastronomyApp.Core.Entities;

public sealed class Festival
{
  public required Guid Id { get; set; }

  public required string Name { get; set; }

  public required DateTime StartsAtUtc { get; set; }

  public required DateTime EndsAtUtc { get; set; }

  public required int NextOrderNumber { get; set; }

  public required bool IsHidden { get; set; }
}
