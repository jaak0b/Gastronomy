namespace GastronomyApp.Core.Entities;

public sealed class Station
{
  public required Guid Id { get; set; }

  public required string Name { get; set; }

  public required int SortOrder { get; set; }

  public required bool IsActive { get; set; }

  public Guid? PrinterId { get; set; }

  public required int NextStationOrderNumber { get; set; }
}
