namespace GastronomyApp.Contracts.TablePlan;

public sealed record AreaShapeGeometry
{
  public required double X { get; init; }

  public required double Y { get; init; }

  public required double Width { get; init; }

  public required double Height { get; init; }

  public required double Rotation { get; init; }
}
