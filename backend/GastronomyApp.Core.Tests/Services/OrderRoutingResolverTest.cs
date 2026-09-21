using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OrderRoutingResolverTest
{
  [SetUp]
  public void SetUp()
  {
    _resolver = new();
  }

  private readonly Guid _catalogItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
  private readonly Guid _kitchenId = Guid.Parse("22222222-2222-2222-2222-222222222222");
  private readonly Guid _barIndoorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
  private readonly Guid _barOutdoorId = Guid.Parse("44444444-4444-4444-4444-444444444444");

  private OrderRoutingResolver _resolver = new();

  private ItemStationAssignment AssignmentTo(Guid stationId)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             FestivalId = Guid.NewGuid(),
             CatalogItemId = _catalogItemId,
             StationId = stationId
           };
  }

  private CatalogItem BuildCatalogItem()
  {
    return new()
           {
             Id = _catalogItemId,
             Name = "Bratwurst",
             CategoryId = Guid.NewGuid(),
             SortOrder = 1,
             IsActive = true
           };
  }

  private Station BuildStation(Guid id, string name, int sortOrder)
  {
    return new()
           {
             Id = id,
             Name = name,
             SortOrder = sortOrder,
             IsActive = true
           };
  }

  [Test]
  public void Resolve_NullAssignments_ThrowsArgumentNullException()
  {
    Assert.That(() => _resolver.Resolve(BuildCatalogItem(), null!, [BuildStation(_kitchenId, "Kueche", 1)], null), Throws.ArgumentNullException);
  }

  [Test]
  public void Resolve_NullActiveStations_ThrowsArgumentNullException()
  {
    Assert.That(() => _resolver.Resolve(BuildCatalogItem(), [AssignmentTo(_kitchenId)], null!, null), Throws.ArgumentNullException);
  }

  [Test]
  public void Resolve_SingleCandidateAndNoChoice_RoutesThereWithoutStationControl()
  {
    ErrorOr<Station> result = _resolver.Resolve(BuildCatalogItem(), [AssignmentTo(_kitchenId)], [BuildStation(_kitchenId, "Kueche", 1)], null);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.True);
                      Assert.That(result.Value.Id, Is.EqualTo(_kitchenId));
                    });
  }

  [Test]
  public void Resolve_MoreThanOneCandidateAndNoChoice_FailsWithStationRequired()
  {
    ErrorOr<Station> result = _resolver.Resolve(BuildCatalogItem(),
                                                                              [
                                                                                AssignmentTo(_barIndoorId),
                                                                                AssignmentTo(_barOutdoorId)
                                                                              ],
                                                                              [
                                                                                BuildStation(_barIndoorId, "Theke innen", 1),
                                                                                BuildStation(_barOutdoorId, "Theke aussen", 2)
                                                                              ],
                                                                              null);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.False);
                      Assert.That(result.RefusalMessageKey(), Is.EqualTo("order.cannotBeProcessed"));
                    });
  }

  [Test]
  public void Resolve_ChoiceNotAssignedToItem_FailsWithStationNotAssignedToItem()
  {
    ErrorOr<Station> result = _resolver.Resolve(BuildCatalogItem(),
                                                                              [
                                                                                AssignmentTo(_barIndoorId),
                                                                                AssignmentTo(_barOutdoorId)
                                                                              ],
                                                                              [
                                                                                BuildStation(_barIndoorId, "Theke innen", 1),
                                                                                BuildStation(_barOutdoorId, "Theke aussen", 2),
                                                                                BuildStation(_kitchenId, "Kueche", 3)
                                                                              ],
                                                                              _kitchenId);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.False);
                      Assert.That(result.RefusalMessageKey(), Is.EqualTo("catalog.itemSoldOut"));
                    });
  }

  [Test]
  public void Resolve_ValidActiveChoice_RoutesThereAndEchoesTheChoice()
  {
    ErrorOr<Station> result = _resolver.Resolve(BuildCatalogItem(),
                                                                              [
                                                                                AssignmentTo(_barIndoorId),
                                                                                AssignmentTo(_barOutdoorId)
                                                                              ],
                                                                              [
                                                                                BuildStation(_barIndoorId, "Theke innen", 1),
                                                                                BuildStation(_barOutdoorId, "Theke aussen", 2)
                                                                              ],
                                                                              _barOutdoorId);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.True);
                      Assert.That(result.Value.Id, Is.EqualTo(_barOutdoorId));
                    });
  }

  [Test]
  public void Resolve_ChoiceNoLongerActive_IsRefusedInsteadOfGoingSomewhereTheWaiterDidNotPick()
  {
    ErrorOr<Station> result = _resolver.Resolve(BuildCatalogItem(),
                                                                              [
                                                                                AssignmentTo(_barIndoorId),
                                                                                AssignmentTo(_barOutdoorId),
                                                                                AssignmentTo(_kitchenId)
                                                                              ],
                                                                              [
                                                                                BuildStation(_kitchenId, "Kueche", 7),
                                                                                BuildStation(_barIndoorId, "Theke innen", 3)
                                                                              ],
                                                                              _barOutdoorId);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.False, "a station the waiter chose and that is switched off must never be replaced by another one");
                      Assert.That(result.RefusalMessageKey(), Is.EqualTo("catalog.itemSoldOut"));
                    });
  }

  [Test]
  public void Resolve_SingleCandidateAndAChoiceNamingIt_RoutesThereAndEchoesTheChoice()
  {
    ErrorOr<Station> result = _resolver.Resolve(BuildCatalogItem(), [AssignmentTo(_kitchenId)], [BuildStation(_kitchenId, "Kueche", 1)], _kitchenId);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.True);
                      Assert.That(result.Value.Id, Is.EqualTo(_kitchenId));
                    });
  }

  [Test]
  public void Resolve_NoActiveCandidate_RefusesWithAStatedReasonRatherThanThrowing()
  {
    ErrorOr<Station> result = _resolver.Resolve(BuildCatalogItem(), [AssignmentTo(_kitchenId)], [], null);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.False);
                      Assert.That(result.RefusalMessageKey(), Is.EqualTo("order.cannotBeProcessed"));
                    });
  }
}
