using ErrorOr;
using FakeItEasy;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class FestivalStationShapeServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _festivalStationRepository = A.Fake<IFestivalStationRepository>();
    _tablePlanRepository = A.Fake<ITablePlanRepository>();

    A.CallTo(() => _festivalStationRepository.FindLinkAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<FestivalStation?>(null));
    A.CallTo(() => _festivalStationRepository.FindLinkAsync(_festivalId, _kitchenId, A<CancellationToken>._)).Returns(Task.FromResult<FestivalStation?>(BuildLink()));
    A.CallTo(() => _tablePlanRepository.FindStationShapeAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<FestivalStationShape?>(null));
    A.CallTo(() => _tablePlanRepository.FindAreaAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Area?>(null));
    A.CallTo(() => _tablePlanRepository.FindAreaAsync(_festivalId, _gardenId, A<CancellationToken>._)).Returns(Task.FromResult<Area?>(BuildArea(_gardenId)));
    A.CallTo(() => _tablePlanRepository.FindAreaAsync(_festivalId, _hallId, A<CancellationToken>._)).Returns(Task.FromResult<Area?>(BuildArea(_hallId)));

    _service = new(_festivalStationRepository, _tablePlanRepository, new());
  }

  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _gardenId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
  private readonly Guid _hallId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
  private readonly Guid _kitchenId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
  private readonly Guid _kitchenLinkId = Guid.Parse("ffffffff-0000-0000-0000-000000000001");

  private IFestivalStationRepository _festivalStationRepository = null!;
  private FestivalStationShapeService _service = null!;
  private ITablePlanRepository _tablePlanRepository = null!;

  [Test]
  public async Task PlaceAsync_AStationThatDoesNotTakePartInTheFestival_IsNotFound()
  {
    var strangerId = Guid.NewGuid();

    ErrorOr<FestivalStationShape> placed = await _service.PlaceAsync(_festivalId, strangerId, BuildRequest(_gardenId), CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(placed.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
                      Assert.That(placed.RefusalMessageKey(), Is.EqualTo("StationLinkNotFound"));
                      Assert.That(placed.RefusalDescription(), Does.Contain(strangerId.ToString()));
                    });
  }

  [Test]
  public async Task PlaceAsync_AnAreaOfAnotherFestival_IsRefusedGenerically()
  {
    var strangerAreaId = Guid.NewGuid();

    ErrorOr<FestivalStationShape> placed = await _service.PlaceAsync(_festivalId, _kitchenId, BuildRequest(strangerAreaId), CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(placed.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(placed.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(placed.RefusalDescription(), Does.Contain(strangerAreaId.ToString()));
                    });

    A.CallTo(() => _tablePlanRepository.AddStationShapeAsync(A<FestivalStationShape>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task PlaceAsync_AStationNotOnThePlanYet_AddsItsShapeExactlyAsSent()
  {
    ErrorOr<FestivalStationShape> placed = await _service.PlaceAsync(_festivalId,
                                                                     _kitchenId,
                                                                     new()
                                                                     {
                                                                       AreaId = _gardenId,
                                                                       Geometry = new()
                                                                                  {
                                                                                    X = -3.75,
                                                                                    Y = 412.125,
                                                                                    Width = 55.5,
                                                                                    Height = 0.25,
                                                                                    Rotation = -90.5
                                                                                  }
                                                                     },
                                                                     CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(placed.Value.FestivalStationId, Is.EqualTo(_kitchenLinkId));
                      Assert.That(placed.Value.FestivalId, Is.EqualTo(_festivalId));
                      Assert.That(placed.Value.AreaId, Is.EqualTo(_gardenId));
                      Assert.That(placed.Value.X, Is.EqualTo(-3.75));
                      Assert.That(placed.Value.Y, Is.EqualTo(412.125));
                      Assert.That(placed.Value.Width, Is.EqualTo(55.5));
                      Assert.That(placed.Value.Height, Is.EqualTo(0.25));
                      Assert.That(placed.Value.Rotation, Is.EqualTo(-90.5));
                    });

    A.CallTo(() => _tablePlanRepository.AddStationShapeAsync(placed.Value, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _tablePlanRepository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task PlaceAsync_AStationAlreadyOnThePlan_MovesTheSameShapeToTheNewArea()
  {
    var shape = BuildShape();
    A.CallTo(() => _tablePlanRepository.FindStationShapeAsync(_kitchenLinkId, A<CancellationToken>._)).Returns(Task.FromResult<FestivalStationShape?>(shape));

    ErrorOr<FestivalStationShape> placed = await _service.PlaceAsync(_festivalId, _kitchenId, BuildRequest(_hallId), CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(placed.Value, Is.SameAs(shape));
                      Assert.That(shape.AreaId, Is.EqualTo(_hallId));
                      Assert.That(shape.X, Is.EqualTo(150));
                      Assert.That(shape.Y, Is.EqualTo(75));
                      Assert.That(shape.Width, Is.EqualTo(60));
                      Assert.That(shape.Height, Is.EqualTo(30));
                      Assert.That(shape.Rotation, Is.EqualTo(15));
                    });

    A.CallTo(() => _tablePlanRepository.AddStationShapeAsync(A<FestivalStationShape>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _tablePlanRepository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task RemoveAsync_AStationNotOnThePlan_IsNotFound()
  {
    ErrorOr<FestivalStationShape> removed = await _service.RemoveAsync(_festivalId, _kitchenId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(removed.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
                      Assert.That(removed.RefusalMessageKey(), Is.EqualTo("StationShapeNotFound"));
                    });
  }

  [Test]
  public async Task RemoveAsync_AStationThatDoesNotTakePartInTheFestival_IsNotFound()
  {
    ErrorOr<FestivalStationShape> removed = await _service.RemoveAsync(_festivalId, Guid.NewGuid(), CancellationToken.None);

    Assert.That(removed.RefusalMessageKey(), Is.EqualTo("StationLinkNotFound"));
  }

  [Test]
  public async Task RemoveAsync_AStationOnThePlan_RemovesItsShape()
  {
    var shape = BuildShape();
    A.CallTo(() => _tablePlanRepository.FindStationShapeAsync(_kitchenLinkId, A<CancellationToken>._)).Returns(Task.FromResult<FestivalStationShape?>(shape));

    ErrorOr<FestivalStationShape> removed = await _service.RemoveAsync(_festivalId, _kitchenId, CancellationToken.None);

    Assert.That(removed.IsError, Is.False);

    A.CallTo(() => _tablePlanRepository.RemoveStationShape(shape)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _tablePlanRepository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  private SaveStationShapeRequest BuildRequest(Guid areaId)
  {
    return new()
           {
             AreaId = areaId,
             Geometry = new()
                        {
                          X = 150,
                          Y = 75,
                          Width = 60,
                          Height = 30,
                          Rotation = 15
                        }
           };
  }

  private FestivalStation BuildLink()
  {
    return new()
           {
             Id = _kitchenLinkId,
             FestivalId = _festivalId,
             StationId = _kitchenId,
             NextStationOrderNumber = 1
           };
  }

  private Area BuildArea(Guid areaId)
  {
    return new()
           {
             Id = areaId,
             FestivalId = _festivalId,
             Name = "Garten",
             SortOrder = 1,
             IsHidden = false
           };
  }

  private FestivalStationShape BuildShape()
  {
    return new()
           {
             FestivalStationId = _kitchenLinkId,
             FestivalId = _festivalId,
             AreaId = _gardenId,
             X = 1,
             Y = 2,
             Width = 3,
             Height = 4,
             Rotation = 5
           };
  }
}
