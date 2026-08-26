using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OrderRoutingResolverTest
{
    private readonly Guid _catalogItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _kitchenId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _barIndoorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _barOutdoorId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private OrderRoutingResolver _resolver = new();

    [SetUp]
    public void SetUp()
    {
        _resolver = new OrderRoutingResolver();
    }

    private ItemLocationAssignment AssignmentTo(Guid productionLocationId)
    {
        return new ItemLocationAssignment
        {
            Id = Guid.NewGuid(),
            CatalogItemId = _catalogItemId,
            ProductionLocationId = productionLocationId,
        };
    }

    private ProductionLocation LocationOf(Guid id, string name, int sortOrder)
    {
        return new ProductionLocation
        {
            Id = id,
            Name = name,
            StationAccessKey = name,
            SlipLanguage = "de",
            SortOrder = sortOrder,
            IsActive = true,
        };
    }

    [Test]
    public void Resolve_SingleCandidateAndNoChoice_RoutesThereWithoutStationControl()
    {
        Result<RoutingDecision, RoutingFailure> result = _resolver.Resolve(
            _catalogItemId,
            [AssignmentTo(_kitchenId)],
            [LocationOf(_kitchenId, "Kueche", 1)],
            null);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.ResolvedProductionLocationId, Is.EqualTo(_kitchenId));
            Assert.That(result.Value.ChosenProductionLocationId, Is.Null);
            Assert.That(result.Value.FellBackFromStaleChoice, Is.False);
        });
    }

    [Test]
    public void Resolve_MoreThanOneCandidateAndNoChoice_FailsWithStationRequired()
    {
        Result<RoutingDecision, RoutingFailure> result = _resolver.Resolve(
            _catalogItemId,
            [AssignmentTo(_barIndoorId), AssignmentTo(_barOutdoorId)],
            [LocationOf(_barIndoorId, "Theke innen", 1), LocationOf(_barOutdoorId, "Theke aussen", 2)],
            null);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure.Reason, Is.EqualTo(RoutingFailureReason.StationRequired));
        });
    }

    [Test]
    public void Resolve_ChoiceNotAssignedToItem_FailsWithStationNotAssignedToItem()
    {
        Result<RoutingDecision, RoutingFailure> result = _resolver.Resolve(
            _catalogItemId,
            [AssignmentTo(_barIndoorId), AssignmentTo(_barOutdoorId)],
            [
                LocationOf(_barIndoorId, "Theke innen", 1),
                LocationOf(_barOutdoorId, "Theke aussen", 2),
                LocationOf(_kitchenId, "Kueche", 3),
            ],
            _kitchenId);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure.Reason, Is.EqualTo(RoutingFailureReason.StationNotAssignedToItem));
        });
    }

    [Test]
    public void Resolve_ValidActiveChoice_RoutesThereAndEchoesTheChoice()
    {
        Result<RoutingDecision, RoutingFailure> result = _resolver.Resolve(
            _catalogItemId,
            [AssignmentTo(_barIndoorId), AssignmentTo(_barOutdoorId)],
            [LocationOf(_barIndoorId, "Theke innen", 1), LocationOf(_barOutdoorId, "Theke aussen", 2)],
            _barOutdoorId);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.ResolvedProductionLocationId, Is.EqualTo(_barOutdoorId));
            Assert.That(result.Value.ChosenProductionLocationId, Is.EqualTo(_barOutdoorId));
            Assert.That(result.Value.FellBackFromStaleChoice, Is.False);
        });
    }

    [Test]
    public void Resolve_ChoiceNoLongerActive_FallsBackToLowestSortOrderActiveCandidateAndKeepsTheChoice()
    {
        Result<RoutingDecision, RoutingFailure> result = _resolver.Resolve(
            _catalogItemId,
            [AssignmentTo(_barIndoorId), AssignmentTo(_barOutdoorId), AssignmentTo(_kitchenId)],
            [LocationOf(_kitchenId, "Kueche", 7), LocationOf(_barIndoorId, "Theke innen", 3)],
            _barOutdoorId);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.ResolvedProductionLocationId, Is.EqualTo(_barIndoorId));
            Assert.That(result.Value.ChosenProductionLocationId, Is.EqualTo(_barOutdoorId));
            Assert.That(result.Value.FellBackFromStaleChoice, Is.True);
        });
    }

    [Test]
    public void Resolve_SingleCandidateAndAChoiceNamingIt_RoutesThereAndEchoesTheChoice()
    {
        Result<RoutingDecision, RoutingFailure> result = _resolver.Resolve(
            _catalogItemId,
            [AssignmentTo(_kitchenId)],
            [LocationOf(_kitchenId, "Kueche", 1)],
            _kitchenId);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.ResolvedProductionLocationId, Is.EqualTo(_kitchenId));
            Assert.That(result.Value.ChosenProductionLocationId, Is.EqualTo(_kitchenId));
        });
    }

    [Test]
    public void Resolve_NoActiveCandidate_ThrowsNamingTheItem()
    {
        InvalidOperationException? thrown = Assert.Throws<InvalidOperationException>(() => _resolver.Resolve(
            _catalogItemId,
            [AssignmentTo(_kitchenId)],
            [],
            null));

        Assert.That(thrown!.Message, Does.Contain(_catalogItemId.ToString()));
    }
}
