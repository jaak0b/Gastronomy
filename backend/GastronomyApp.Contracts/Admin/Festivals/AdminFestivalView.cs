namespace GastronomyApp.Contracts.Admin.Festivals;

public sealed record AdminFestivalView
{
  public required Guid FestivalId { get; init; }

  public required string Name { get; init; }

  public required DateTime StartsAtUtc { get; init; }

  public required DateTime EndsAtUtc { get; init; }

  public required bool IsHidden { get; init; }

  public required bool IsRunning { get; init; }

  public required int StationCount { get; init; }

  public required int MenuItemCount { get; init; }

  public required int OrderCount { get; init; }
}
