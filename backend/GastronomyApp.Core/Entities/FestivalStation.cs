namespace GastronomyApp.Core.Entities;

public sealed class FestivalStation
{
  public required Guid Id { get; set; }

  public required Guid FestivalId { get; set; }

  public required Guid StationId { get; set; }

  public required int NextStationOrderNumber { get; set; }
}
