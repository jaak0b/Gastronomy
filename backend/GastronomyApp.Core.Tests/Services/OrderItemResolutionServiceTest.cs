using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OrderItemResolutionServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _catalogItemRepository = A.Fake<ICatalogItemRepository>();
    _stationRepository = A.Fake<IStationRepository>();

    A.CallTo(() => _catalogItemRepository.FindMenuRowAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._))
     .Returns(Task.FromResult<FestivalCatalogItem?>(null));
    A.CallTo(() => _stationRepository.FindAtFestivalAsync(A<Guid>._, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyCollection<Station>>([
                                                              BuildStation(_kitchenId, "Kueche", 1),
                                                              BuildStation(_barIndoorId, "Theke innen", 2)
                                                            ]));

    GivenCatalogItem(_bratwurstId, "Bratwurst", [_kitchenId]);
    GivenCatalogItem(_beerId, "Bier", [_barIndoorId]);

    _service = new(_catalogItemRepository, _stationRepository, new());
  }

  private readonly Guid _bratwurstId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
  private readonly Guid _beerId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
  private readonly Guid _kitchenId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
  private readonly Guid _barIndoorId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
  private readonly Guid _barOutdoorId = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");

  private ICatalogItemRepository _catalogItemRepository = null!;
  private IStationRepository _stationRepository = null!;
  private OrderItemResolutionService _service = null!;

  [Test]
  public void ResolveAsync_NullItemRequests_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _service.ResolveAsync(_festivalId, null!, CancellationToken.None),
                Throws.ArgumentNullException);
  }

  [Test]
  public async Task ResolveAsync_ItemsThatAllRoute_ReturnsOneResolvedItemPerRequestInTheOrderTheyWereSent()
  {
    Result<IReadOnlyList<ResolvedOrderItem>, OrderValidationFailure> result =
      await _service.ResolveAsync(_festivalId, [ItemFor(_bratwurstId), ItemFor(_beerId)], CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.True);
                      Assert.That(result.Value, Has.Count.EqualTo(2));
                      Assert.That(result.Value[0].CatalogItem.Name, Is.EqualTo("Bratwurst"));
                      Assert.That(result.Value[0].Decision.ResolvedStationId, Is.EqualTo(_kitchenId));
                      Assert.That(result.Value[1].CatalogItem.Name, Is.EqualTo("Bier"));
                      Assert.That(result.Value[1].Decision.ResolvedStationId, Is.EqualTo(_barIndoorId));
                    });
  }

  [Test]
  public async Task ResolveAsync_UnknownCatalogItemId_FailsNamingTheOffendingItemAndStopsAtTheFirstUnknown()
  {
    var unknownId = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000dead");
    var secondUnknownId = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000beef");
    A.CallTo(() => _catalogItemRepository.FindByIdAsync(unknownId, A<CancellationToken>._))
     .Returns(Task.FromResult<CatalogItem?>(null));
    A.CallTo(() => _catalogItemRepository.FindByIdAsync(secondUnknownId, A<CancellationToken>._))
     .Returns(Task.FromResult<CatalogItem?>(null));

    Result<IReadOnlyList<ResolvedOrderItem>, OrderValidationFailure> result =
      await _service.ResolveAsync(_festivalId, [ItemFor(unknownId), ItemFor(secondUnknownId)], CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.False);
                      Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.UnknownCatalogItemId));
                      Assert.That(result.Failure.OffendingCatalogItemId, Is.EqualTo(unknownId));
                    });
    A.CallTo(() => _catalogItemRepository.FindByIdAsync(secondUnknownId, A<CancellationToken>._))
     .MustNotHaveHappened();
  }

  [Test]
  public async Task ResolveAsync_MoreThanOneCandidateAndNoStationChosen_FailsWithStationRequired()
  {
    Result<IReadOnlyList<ResolvedOrderItem>, OrderValidationFailure> result =
      await _service.ResolveAsync(_festivalId, [ItemFor(AmbiguouslyRoutedItemId())], CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.False);
                      Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.StationRequired));
                    });
  }

  [Test]
  public async Task ResolveAsync_StationNotAssignedToTheItem_FailsWithStationNotAssignedToItem()
  {
    Result<IReadOnlyList<ResolvedOrderItem>, OrderValidationFailure> result =
      await _service.ResolveAsync(_festivalId, [ItemFor(_bratwurstId, _barIndoorId)], CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.False);
                      Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.StationNotAssignedToItem));
                    });
  }

  [Test]
  public async Task ResolveAsync_DeactivatedItemReturnedByTheRepository_IsRefusedNamingItemNotAvailable()
  {
    A.CallTo(() => _catalogItemRepository.FindByIdAsync(_bratwurstId, A<CancellationToken>._))
     .Returns(Task.FromResult<CatalogItem?>(new()
                                            {
                                              Id = _bratwurstId,
                                              Name = "Bratwurst",
                                              CategoryId = Guid.NewGuid(),
                                              SortOrder = 1,
                                              IsActive = false
                                            }));

    Result<IReadOnlyList<ResolvedOrderItem>, OrderValidationFailure> result =
      await _service.ResolveAsync(_festivalId, [ItemFor(_bratwurstId)], CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.False);
                      Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.ItemNotAvailable));
                      Assert.That(result.Failure.OffendingCatalogItemId, Is.EqualTo(_bratwurstId));
                      Assert.That(result.Failure.OffendingCatalogItemName, Is.EqualTo("Bratwurst"));
                    });
  }

  [Test]
  public async Task ResolveAsync_SoldOutMenuRowReturnedByTheRepository_IsRefusedNamingItemNotAvailable()
  {
    Result<IReadOnlyList<ResolvedOrderItem>, OrderValidationFailure> result =
      await _service.ResolveAsync(_festivalId, [ItemFor(SoldOutItemId())], CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.False);
                      Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.ItemNotAvailable));
                      Assert.That(result.Failure.OffendingCatalogItemId, Is.EqualTo(_bratwurstId));
                      Assert.That(result.Failure.OffendingCatalogItemName, Is.EqualTo("Bratwurst"));
                    });
  }

  [Test]
  public async Task ResolveAsync_EveryDeclaredRoutingFailureReason_MapsToItsOwnValidationFailureReason()
  {
    Dictionary<RoutingFailureReason, OrderValidationFailureReason> expectedMapping = new()
                                                                                     {
                                                                                       [RoutingFailureReason.StationRequired] =
                                                                                         OrderValidationFailureReason.StationRequired,
                                                                                       [RoutingFailureReason.StationNotAssignedToItem] =
                                                                                         OrderValidationFailureReason.StationNotAssignedToItem,
                                                                                       [RoutingFailureReason.ItemHasNoStation] =
                                                                                         OrderValidationFailureReason.ItemHasNoStation,
                                                                                       [RoutingFailureReason.ChosenStationNoLongerPreparesTheItem] =
                                                                                         OrderValidationFailureReason.ChosenStationNoLongerPreparesTheItem
                                                                                     };

    Assert.That(expectedMapping.Keys,
                Is.EquivalentTo(Enum.GetValues<RoutingFailureReason>()),
                "every routing failure reason needs a mapping this test pins down");

    foreach (var routingFailureReason in Enum.GetValues<RoutingFailureReason>())
    {
      SetUp();

      Assert.That(await ReasonProducedByAsync(routingFailureReason),
                  Is.EqualTo(expectedMapping[routingFailureReason]));
    }
  }

  private async Task<OrderValidationFailureReason> ReasonProducedByAsync(RoutingFailureReason routingFailureReason)
  {
    var itemRequest = routingFailureReason switch
                      {
                        RoutingFailureReason.StationRequired => ItemFor(AmbiguouslyRoutedItemId()),
                        RoutingFailureReason.StationNotAssignedToItem => ItemFor(_bratwurstId, _barIndoorId),
                        RoutingFailureReason.ItemHasNoStation => ItemFor(ItemWithNoActiveStationId()),
                        RoutingFailureReason.ChosenStationNoLongerPreparesTheItem =>
                          ItemFor(ItemWithAStaleStationChoiceId(), _barOutdoorId),
                        _ => throw new InvalidOperationException($"No scenario covers {routingFailureReason}")
                      };

    Result<IReadOnlyList<ResolvedOrderItem>, OrderValidationFailure> result =
      await _service.ResolveAsync(_festivalId, [itemRequest], CancellationToken.None);

    Assert.That(result.IsSuccess, Is.False, $"{routingFailureReason} should have been refused");

    return result.Failure.Reason;
  }

  private Guid AmbiguouslyRoutedItemId()
  {
    GivenAssignments(_beerId, [_barIndoorId, _barOutdoorId]);
    A.CallTo(() => _stationRepository.FindAtFestivalAsync(A<Guid>._, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyCollection<Station>>([
                                                              BuildStation(_barIndoorId, "Theke innen", 2),
                                                              BuildStation(_barOutdoorId, "Theke aussen", 3)
                                                            ]));

    return _beerId;
  }

  private Guid ItemWithNoActiveStationId()
  {
    GivenAssignments(_beerId, [_barIndoorId]);
    A.CallTo(() => _stationRepository.FindAtFestivalAsync(A<Guid>._, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyCollection<Station>>([]));

    return _beerId;
  }

  private Guid ItemWithAStaleStationChoiceId()
  {
    GivenAssignments(_beerId, [_barIndoorId, _barOutdoorId]);
    A.CallTo(() => _stationRepository.FindAtFestivalAsync(A<Guid>._, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyCollection<Station>>([BuildStation(_barIndoorId, "Theke innen", 2)]));

    return _beerId;
  }

  private Guid SoldOutItemId()
  {
    A.CallTo(() => _catalogItemRepository.FindMenuRowAsync(_festivalId, _bratwurstId, A<CancellationToken>._))
     .Returns(Task.FromResult<FestivalCatalogItem?>(new()
                                                    {
                                                      Id = Guid.NewGuid(),
                                                      FestivalId = _festivalId,
                                                      CatalogItemId = _bratwurstId,
                                                      PriceCents = 350,
                                                      IsAvailable = false
                                                    }));

    return _bratwurstId;
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

  private void GivenCatalogItem(Guid id, string name, IReadOnlyCollection<Guid> stationIds)
  {
    CatalogItem item = new()
                       {
                         Id = id,
                         Name = name,
                         CategoryId = Guid.NewGuid(),
                         SortOrder = 1,
                         IsActive = true
                       };

    A.CallTo(() => _catalogItemRepository.FindByIdAsync(id, A<CancellationToken>._))
     .Returns(Task.FromResult<CatalogItem?>(item));
    GivenAssignments(id, stationIds);
  }

  private void GivenAssignments(Guid catalogItemId, IReadOnlyCollection<Guid> stationIds)
  {
    List<ItemStationAssignment> assignments = stationIds
                                             .Select(stationId => new ItemStationAssignment
                                                                  {
                                                                    Id = Guid.NewGuid(),
                                                                    FestivalId = _festivalId,
                                                                    CatalogItemId = catalogItemId,
                                                                    StationId = stationId
                                                                  })
                                             .ToList();

    A.CallTo(() => _catalogItemRepository.FindAssignmentsAsync(A<Guid>._, catalogItemId, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyCollection<ItemStationAssignment>>(assignments));
  }

  private OrderAcceptanceItemRequest ItemFor(Guid catalogItemId, Guid? stationId = null)
  {
    return new()
           {
             CatalogItemId = catalogItemId,
             UnitPriceCents = 350,
             Note = null,
             StationId = stationId
           };
  }
}
