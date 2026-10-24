using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class AreaShapeServiceTest
{
  private readonly AreaShapeService _service = new();

  [Test]
  public void Reshape_AGeometryWithManyDecimalsAndANegativeAngle_WritesEveryValueUnchanged()
  {
    var table = BuildTable();

    _service.Reshape(table,
                     new()
                     {
                       X = -0.123456789,
                       Y = 98765.4321,
                       Width = 1e-9,
                       Height = 123.5,
                       Rotation = -179.999
                     });

    Assert.Multiple(() =>
                    {
                      Assert.That(table.X, Is.EqualTo(-0.123456789));
                      Assert.That(table.Y, Is.EqualTo(98765.4321));
                      Assert.That(table.Width, Is.EqualTo(1e-9));
                      Assert.That(table.Height, Is.EqualTo(123.5));
                      Assert.That(table.Rotation, Is.EqualTo(-179.999));
                    });
  }

  [Test]
  public void GeometryOf_AStationShape_ReadsBackWhatReshapeWrote()
  {
    FestivalStationShape shape = new()
                                 {
                                   FestivalStationId = Guid.NewGuid(),
                                   FestivalId = Guid.NewGuid(),
                                   AreaId = Guid.NewGuid()
                                 };
    AreaShapeGeometry written = new()
                                {
                                  X = 12.5,
                                  Y = -7.25,
                                  Width = 60,
                                  Height = 45.5,
                                  Rotation = -135
                                };

    _service.Reshape(shape, written);

    Assert.That(_service.GeometryOf(shape), Is.EqualTo(written));
  }

  [Test]
  public void Reshape_NullGeometry_ThrowsArgumentNullException()
  {
    Assert.That(() => _service.Reshape(BuildTable(), null!), Throws.ArgumentNullException);
  }

  [Test]
  public void GeometryOf_NullShape_ThrowsArgumentNullException()
  {
    Assert.That(() => _service.GeometryOf(null!), Throws.ArgumentNullException);
  }

  private Table BuildTable()
  {
    return new()
           {
             Id = Guid.NewGuid(),
             FestivalId = Guid.NewGuid(),
             AreaId = Guid.NewGuid(),
             Name = "Tisch 1",
             IsHidden = false
           };
  }
}
