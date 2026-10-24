namespace GastronomyApp.Core.Entities;

public abstract class AreaShape
{
  protected AreaShape()
  {
  }

  public required Guid FestivalId { get; set; }

  public required Guid AreaId { get; set; }

  public double X { get; set; }

  public double Y { get; set; }

  public double Width { get; set; }

  public double Height { get; set; }

  public double Rotation { get; set; }

  public Festival Festival { get; set; } = null!;

  public Area Area { get; set; } = null!;
}
