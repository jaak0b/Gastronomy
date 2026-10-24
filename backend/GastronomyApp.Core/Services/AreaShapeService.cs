using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Services;

public sealed class AreaShapeService
{
  public void Reshape(AreaShape shape, AreaShapeGeometry geometry)
  {
    ArgumentNullException.ThrowIfNull(shape);
    ArgumentNullException.ThrowIfNull(geometry);

    shape.X = geometry.X;
    shape.Y = geometry.Y;
    shape.Width = geometry.Width;
    shape.Height = geometry.Height;
    shape.Rotation = geometry.Rotation;
  }

  public AreaShapeGeometry GeometryOf(AreaShape shape)
  {
    ArgumentNullException.ThrowIfNull(shape);

    return new()
           {
             X = shape.X,
             Y = shape.Y,
             Width = shape.Width,
             Height = shape.Height,
             Rotation = shape.Rotation
           };
  }
}
