using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OrderAcceptanceServiceTest
{
    private readonly Guid _eventSessionId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private readonly Guid _staffMemberId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private readonly Guid _deviceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
    private readonly Guid _bratwurstId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private readonly Guid _beerId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private readonly Guid _kitchenId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private readonly Guid _barIndoorId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    private readonly Guid _barOutdoorId = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
    private readonly Guid _clientOrderId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    private readonly DateTime _now = new(2026, 8, 26, 17, 42, 3, DateTimeKind.Utc);

    private IOrderRepository _orderRepository = null!;
    private ICatalogItemRepository _catalogItemRepository = null!;
    private IProductionLocationRepository _productionLocationRepository = null!;
    private INumberAllocator _numberAllocator = null!;
    private IClock _clock = null!;
    private OrderAcceptanceService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _orderRepository = A.Fake<IOrderRepository>();
        _catalogItemRepository = A.Fake<ICatalogItemRepository>();
        _productionLocationRepository = A.Fake<IProductionLocationRepository>();
        _numberAllocator = A.Fake<INumberAllocator>();
        _clock = A.Fake<IClock>();

        A.CallTo(() => _clock.UtcNow).Returns(_now);
        A.CallTo(() => _orderRepository.FindByClientOrderIdAsync(A<Guid>._, A<CancellationToken>._))
            .Returns(Task.FromResult<Order?>(null));
        A.CallTo(() => _productionLocationRepository.FindActiveAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyCollection<ProductionLocation>>(
            [
                LocationOf(_kitchenId, "Kueche", 1),
                LocationOf(_barIndoorId, "Theke innen", 2),
            ]));
        A.CallTo(() => _numberAllocator.AllocateGlobalOrderNumberAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(137));
        A.CallTo(() => _numberAllocator.AllocateLocationSequenceNumberAsync(A<Guid>._, A<CancellationToken>._))
            .Returns(Task.FromResult(42));

        GivenCatalogItem(_bratwurstId, "Bratwurst", 350, [_kitchenId]);
        GivenCatalogItem(_beerId, "Bier", 400, [_barIndoorId]);

        _service = new OrderAcceptanceService(
            _orderRepository,
            _catalogItemRepository,
            _productionLocationRepository,
            _numberAllocator,
            new OrderRoutingResolver(),
            new OrderTotalCalculator(),
            _clock);
    }

    private ProductionLocation LocationOf(Guid id, string name, int sortOrder)
    {
        return new ProductionLocation
        {
            Id = id,
            Name = name,
            SortOrder = sortOrder,
            IsActive = true,
        };
    }

    private void GivenCatalogItem(Guid id, string name, int priceCents, IReadOnlyCollection<Guid> locationIds)
    {
        CatalogItem item = new()
        {
            Id = id,
            Name = name,
            CategoryName = "Speisen",
            PriceCents = priceCents,
            SortOrder = 1,
            IsActive = true,
            IsAvailable = true,
        };

        A.CallTo(() => _catalogItemRepository.FindByIdAsync(id, A<CancellationToken>._))
            .Returns(Task.FromResult<CatalogItem?>(item));
        GivenAssignments(id, locationIds);
    }

    private void GivenAssignments(Guid catalogItemId, IReadOnlyCollection<Guid> locationIds)
    {
        List<ItemLocationAssignment> assignments = locationIds
            .Select(locationId => new ItemLocationAssignment
            {
                Id = Guid.NewGuid(),
                CatalogItemId = catalogItemId,
                ProductionLocationId = locationId,
            })
            .ToList();

        A.CallTo(() => _catalogItemRepository.FindAssignmentsAsync(catalogItemId, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyCollection<ItemLocationAssignment>>(assignments));
    }

    private OrderAcceptanceRequest RequestWith(
        IReadOnlyList<OrderAcceptanceLineRequest> lines,
        string tableLabel = "Tisch 12")
    {
        return new OrderAcceptanceRequest
        {
            ClientOrderId = _clientOrderId,
            StaffMemberId = _staffMemberId,
            DeviceId = _deviceId,
            TableLabel = tableLabel,
            Note = null,
            Lines = lines,
        };
    }

    private OrderAcceptanceLineRequest LineFor(Guid catalogItemId, int quantity, Guid? productionLocationId = null)
    {
        return new OrderAcceptanceLineRequest
        {
            CatalogItemId = catalogItemId,
            Quantity = quantity,
            Note = null,
            ProductionLocationId = productionLocationId,
        };
    }

    private void AssertNothingWasAllocatedOrStored()
    {
        A.CallTo(_numberAllocator).MustNotHaveHappened();
        A.CallTo(() => _orderRepository.AddAsync(A<Order>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task AcceptAsync_EmptyLines_FailsWithNoLinesAndAllocatesNothing()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result =
            await _service.AcceptAsync(RequestWith([]), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.NoLines));
        });
        AssertNothingWasAllocatedOrStored();
    }

    [Test]
    public async Task AcceptAsync_QuantityOfZero_FailsWithQuantityOutOfRange()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result =
            await _service.AcceptAsync(RequestWith([LineFor(_bratwurstId, 0)]), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.QuantityOutOfRange));
        });
        AssertNothingWasAllocatedOrStored();
    }

    [Test]
    public async Task AcceptAsync_QuantityOfOneHundred_FailsWithQuantityOutOfRange()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result =
            await _service.AcceptAsync(RequestWith([LineFor(_bratwurstId, 100)]), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.QuantityOutOfRange));
        });
        AssertNothingWasAllocatedOrStored();
    }

    [Test]
    public async Task AcceptAsync_QuantityOfOne_PassesTheQuantityCheck()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result =
            await _service.AcceptAsync(RequestWith([LineFor(_bratwurstId, 1)]), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public async Task AcceptAsync_QuantityOfNinetyNine_PassesTheQuantityCheck()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result =
            await _service.AcceptAsync(RequestWith([LineFor(_bratwurstId, 99)]), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public async Task AcceptAsync_EmptyTableLabel_FailsWithTableLabelMissing()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([LineFor(_bratwurstId, 1)], string.Empty),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.TableLabelMissing));
        });
        AssertNothingWasAllocatedOrStored();
    }

    [Test]
    public async Task AcceptAsync_WhitespaceTableLabel_FailsWithTableLabelMissing()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([LineFor(_bratwurstId, 1)], "   "),
            CancellationToken.None);

        Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.TableLabelMissing));
    }

    [Test]
    public async Task AcceptAsync_TableLabelOfFortyOneCharacters_FailsWithTableLabelTooLong()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([LineFor(_bratwurstId, 1)], new string('T', 41)),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.TableLabelTooLong));
        });
        AssertNothingWasAllocatedOrStored();
    }

    [Test]
    public async Task AcceptAsync_TableLabelOfFortyCharacters_PassesTheTableLabelCheck()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([LineFor(_bratwurstId, 1)], new string('T', 40)),
            CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public async Task AcceptAsync_UnknownCatalogItemId_FailsNamingTheOffendingItemAndStopsAtTheFirstUnknown()
    {
        Guid unknownId = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000dead");
        Guid secondUnknownId = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000beef");
        A.CallTo(() => _catalogItemRepository.FindByIdAsync(unknownId, A<CancellationToken>._))
            .Returns(Task.FromResult<CatalogItem?>(null));
        A.CallTo(() => _catalogItemRepository.FindByIdAsync(secondUnknownId, A<CancellationToken>._))
            .Returns(Task.FromResult<CatalogItem?>(null));

        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([LineFor(unknownId, 1), LineFor(secondUnknownId, 1)]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.UnknownCatalogItemId));
            Assert.That(result.Failure.OffendingCatalogItemId, Is.EqualTo(unknownId));
        });
        A.CallTo(() => _catalogItemRepository.FindByIdAsync(secondUnknownId, A<CancellationToken>._))
            .MustNotHaveHappened();
        AssertNothingWasAllocatedOrStored();
    }

    [Test]
    public async Task AcceptAsync_MoreThanOneCandidateAndNoStationChosen_FailsWithStationRequired()
    {
        GivenAssignments(_beerId, [_barIndoorId, _barOutdoorId]);
        A.CallTo(() => _productionLocationRepository.FindActiveAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyCollection<ProductionLocation>>(
            [
                LocationOf(_barIndoorId, "Theke innen", 2),
                LocationOf(_barOutdoorId, "Theke aussen", 3),
            ]));

        Result<OrderAcceptanceResult, OrderValidationFailure> result =
            await _service.AcceptAsync(RequestWith([LineFor(_beerId, 1)]), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.StationRequired));
        });
        AssertNothingWasAllocatedOrStored();
    }

    [Test]
    public async Task AcceptAsync_StationNotAssignedToTheItem_FailsWithStationNotAssignedToItem()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([LineFor(_bratwurstId, 1, _barIndoorId)]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.StationNotAssignedToItem));
        });
        AssertNothingWasAllocatedOrStored();
    }

    [Test]
    public async Task AcceptAsync_SoldOutItem_IsStillAccepted()
    {
        A.CallTo(() => _catalogItemRepository.FindByIdAsync(_bratwurstId, A<CancellationToken>._))
            .Returns(Task.FromResult<CatalogItem?>(new CatalogItem
            {
                Id = _bratwurstId,
                Name = "Bratwurst",
                CategoryName = "Speisen",
                PriceCents = 350,
                SortOrder = 1,
                IsActive = true,
                IsAvailable = false,
            }));

        Result<OrderAcceptanceResult, OrderValidationFailure> result =
            await _service.AcceptAsync(RequestWith([LineFor(_bratwurstId, 1)]), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public async Task AcceptAsync_DeactivatedItemReturnedByTheRepository_IsStillAccepted()
    {
        A.CallTo(() => _catalogItemRepository.FindByIdAsync(_bratwurstId, A<CancellationToken>._))
            .Returns(Task.FromResult<CatalogItem?>(new CatalogItem
            {
                Id = _bratwurstId,
                Name = "Bratwurst",
                CategoryName = "Speisen",
                PriceCents = 350,
                SortOrder = 1,
                IsActive = false,
                IsAvailable = true,
            }));

        Result<OrderAcceptanceResult, OrderValidationFailure> result =
            await _service.AcceptAsync(RequestWith([LineFor(_bratwurstId, 1)]), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public async Task AcceptAsync_FirstTimeClientOrderId_AllocatesNumbersAndStoresTheOrderOnce()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([LineFor(_bratwurstId, 2), LineFor(_beerId, 1)]),
            CancellationToken.None);

        Order order = result.Value.Order;

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.WasAlreadyAccepted, Is.False);
            Assert.That(order.GlobalOrderNumber, Is.EqualTo(137));
            Assert.That(order.Lines, Has.Count.EqualTo(2));
            Assert.That(order.Tickets, Has.Count.EqualTo(2));
            Assert.That(order.TotalCents, Is.EqualTo(1100));
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Accepted));
            Assert.That(order.StaffMemberId, Is.EqualTo(_staffMemberId));
            Assert.That(order.DeviceId, Is.EqualTo(_deviceId));
            Assert.That(order.ClientOrderId, Is.EqualTo(_clientOrderId));
        });

        A.CallTo(() => _numberAllocator.AllocateGlobalOrderNumberAsync(A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _numberAllocator.AllocateLocationSequenceNumberAsync(A<Guid>._, A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();
        A.CallTo(() => _orderRepository.AddAsync(order, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task AcceptAsync_TwoLinesAtOneLocation_AllocatesOneSequenceNumberForOneTicket()
    {
        GivenCatalogItem(_beerId, "Bier", 400, [_kitchenId]);

        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([LineFor(_bratwurstId, 1), LineFor(_beerId, 1)]),
            CancellationToken.None);

        Order order = result.Value.Order;

        Assert.Multiple(() =>
        {
            Assert.That(order.Tickets, Has.Count.EqualTo(1));
            Assert.That(order.Lines, Has.Count.EqualTo(2));
            Assert.That(order.Lines.Select(line => line.LocationTicketId).Distinct().Count(), Is.EqualTo(1));
            Assert.That(order.Lines[0].LocationTicketId, Is.EqualTo(order.Tickets[0].Id));
        });
        A.CallTo(() => _numberAllocator.AllocateLocationSequenceNumberAsync(_kitchenId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task AcceptAsync_LinesAtTwoLocations_CreatesOneTicketPerLocationWithItsOwnSequenceNumber()
    {
        A.CallTo(() => _numberAllocator.AllocateLocationSequenceNumberAsync(_kitchenId, A<CancellationToken>._))
            .Returns(Task.FromResult(42));
        A.CallTo(() => _numberAllocator.AllocateLocationSequenceNumberAsync(_barIndoorId, A<CancellationToken>._))
            .Returns(Task.FromResult(7));

        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([LineFor(_bratwurstId, 2), LineFor(_beerId, 1)]),
            CancellationToken.None);

        Order order = result.Value.Order;
        LocationTicket kitchenTicket = order.Tickets.Single(ticket => ticket.ProductionLocationId == _kitchenId);
        LocationTicket barTicket = order.Tickets.Single(ticket => ticket.ProductionLocationId == _barIndoorId);

        Assert.Multiple(() =>
        {
            Assert.That(kitchenTicket.LocationSequenceNumber, Is.EqualTo(42));
            Assert.That(barTicket.LocationSequenceNumber, Is.EqualTo(7));
            Assert.That(order.TotalCents, Is.EqualTo(1100));
        });
    }

    [Test]
    public async Task AcceptAsync_KnownClientOrderId_ReturnsTheExistingOrderAndAllocatesNothing()
    {
        Order existing = new()
        {
            Id = Guid.NewGuid(),
            ClientOrderId = _clientOrderId,
            GlobalOrderNumber = 12,
            StaffMemberId = _staffMemberId,
            DeviceId = _deviceId,
            TableLabel = "Tisch 12",
            TotalCents = 350,
            Status = OrderStatus.Accepted,
            CreatedAtUtc = _now,
        };
        A.CallTo(() => _orderRepository.FindByClientOrderIdAsync(_clientOrderId, A<CancellationToken>._))
            .Returns(Task.FromResult<Order?>(existing));

        Result<OrderAcceptanceResult, OrderValidationFailure> result =
            await _service.AcceptAsync(RequestWith([LineFor(_bratwurstId, 1)]), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Order, Is.SameAs(existing));
            Assert.That(result.Value.WasAlreadyAccepted, Is.True);
        });
        AssertNothingWasAllocatedOrStored();
    }

    [Test]
    public async Task AcceptAsync_StaleChosenLocation_RoutesToTheFallbackAndKeepsTheChoice()
    {
        GivenAssignments(_beerId, [_barIndoorId, _barOutdoorId]);

        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([LineFor(_beerId, 1, _barOutdoorId)]),
            CancellationToken.None);

        Order order = result.Value.Order;

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(order.Tickets[0].ProductionLocationId, Is.EqualTo(_barIndoorId));
            Assert.That(order.Lines[0].ChosenProductionLocationId, Is.EqualTo(_barOutdoorId));
        });
    }

    [Test]
    public async Task AcceptAsync_ValidRequest_GivesEveryCreatedRowItsOwnNonEmptyIdentifier()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([LineFor(_bratwurstId, 2), LineFor(_beerId, 1)]),
            CancellationToken.None);

        Order order = result.Value.Order;
        List<Guid> everyId =
        [
            order.Id,
            .. order.Lines.Select(line => line.Id),
            .. order.Tickets.Select(ticket => ticket.Id),
        ];

        Assert.Multiple(() =>
        {
            Assert.That(everyId, Has.None.EqualTo(Guid.Empty));
            Assert.That(everyId.Distinct().Count(), Is.EqualTo(everyId.Count));
            Assert.That(order.Lines.Select(line => line.OrderId), Is.All.EqualTo(order.Id));
            Assert.That(order.Tickets.Select(ticket => ticket.OrderId), Is.All.EqualTo(order.Id));
        });
    }

    [Test]
    public async Task AcceptAsync_ValidRequest_StampsEveryCreatedRowWithTheClocksTime()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([LineFor(_bratwurstId, 2), LineFor(_beerId, 1)]),
            CancellationToken.None);

        Order order = result.Value.Order;

        Assert.Multiple(() =>
        {
            Assert.That(order.CreatedAtUtc, Is.EqualTo(_now));
            Assert.That(order.Tickets.Select(ticket => ticket.CreatedAtUtc), Is.All.EqualTo(_now));
        });
    }

    [Test]
    public async Task AcceptAsync_ValidRequest_StartsEveryTicketQueuedWithNoReprints()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([LineFor(_bratwurstId, 2), LineFor(_beerId, 1)]),
            CancellationToken.None);

        Order order = result.Value.Order;

        Assert.Multiple(() =>
        {
            Assert.That(order.Tickets.Select(ticket => ticket.Status), Is.All.EqualTo(LocationTicketStatus.Queued));
            Assert.That(order.Tickets.Select(ticket => ticket.ReprintCount), Is.All.EqualTo(0));
        });
    }

    [Test]
    public async Task AcceptAsync_ValidRequest_SnapshotsTheNameAndPriceFromTheCatalogRow()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([LineFor(_bratwurstId, 2), LineFor(_beerId, 1)]),
            CancellationToken.None);

        Order order = result.Value.Order;
        OrderLine bratwurstLine = order.Lines.Single(line => line.CatalogItemId == _bratwurstId);
        OrderLine beerLine = order.Lines.Single(line => line.CatalogItemId == _beerId);

        Assert.Multiple(() =>
        {
            Assert.That(bratwurstLine.ItemNameSnapshot, Is.EqualTo("Bratwurst"));
            Assert.That(bratwurstLine.UnitPriceCentsSnapshot, Is.EqualTo(350));
            Assert.That(bratwurstLine.Quantity, Is.EqualTo(2));
            Assert.That(beerLine.ItemNameSnapshot, Is.EqualTo("Bier"));
            Assert.That(beerLine.UnitPriceCentsSnapshot, Is.EqualTo(400));
        });
    }

    private async Task<OrderValidationFailureReason> ReasonProducedByAsync(OrderValidationFailureReason scenario)
    {
        OrderAcceptanceRequest request = scenario switch
        {
            OrderValidationFailureReason.NoLines => RequestWith([]),
            OrderValidationFailureReason.QuantityOutOfRange => RequestWith([LineFor(_bratwurstId, 0)]),
            OrderValidationFailureReason.TableLabelMissing =>
                RequestWith([LineFor(_bratwurstId, 1)], string.Empty),
            OrderValidationFailureReason.TableLabelTooLong =>
                RequestWith([LineFor(_bratwurstId, 1)], new string('T', 41)),
            OrderValidationFailureReason.UnknownCatalogItemId => RequestWith([LineFor(UnknownItemId(), 1)]),
            OrderValidationFailureReason.StationRequired => RequestWith([LineFor(AmbiguouslyRoutedItemId(), 1)]),
            OrderValidationFailureReason.StationNotAssignedToItem =>
                RequestWith([LineFor(_bratwurstId, 1, _barIndoorId)]),
            OrderValidationFailureReason.ItemHasNoStation =>
                RequestWith([LineFor(ItemWithNoActiveStationId(), 1)]),
            _ => throw new InvalidOperationException($"No scenario covers {scenario}"),
        };

        Result<OrderAcceptanceResult, OrderValidationFailure> result =
            await _service.AcceptAsync(request, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False, $"{scenario} should have been rejected");

        return result.Failure.Reason;
    }

    private Guid UnknownItemId()
    {
        Guid unknownId = Guid.Parse("bbbbbbbb-0000-0000-0000-0000000000ff");
        A.CallTo(() => _catalogItemRepository.FindByIdAsync(unknownId, A<CancellationToken>._))
            .Returns(Task.FromResult<CatalogItem?>(null));

        return unknownId;
    }

    private Guid AmbiguouslyRoutedItemId()
    {
        GivenAssignments(_beerId, [_barIndoorId, _barOutdoorId]);
        A.CallTo(() => _productionLocationRepository.FindActiveAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyCollection<ProductionLocation>>(
            [
                LocationOf(_barIndoorId, "Theke innen", 2),
                LocationOf(_barOutdoorId, "Theke aussen", 3),
            ]));

        return _beerId;
    }

    private Guid ItemWithNoActiveStationId()
    {
        GivenAssignments(_beerId, [_barIndoorId]);
        A.CallTo(() => _productionLocationRepository.FindActiveAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyCollection<ProductionLocation>>([]));

        return _beerId;
    }

    [Test]
    public async Task AcceptAsync_EveryDeclaredValidationFailureReason_IsProducedByARealRequest()
    {
        foreach (OrderValidationFailureReason reason in Enum.GetValues<OrderValidationFailureReason>())
        {
            SetUp();

            Assert.That(await ReasonProducedByAsync(reason), Is.EqualTo(reason));
        }
    }

    [Test]
    public async Task AcceptAsync_EveryDeclaredRoutingFailureReason_MapsToItsOwnValidationFailureReason()
    {
        Dictionary<RoutingFailureReason, OrderValidationFailureReason> expectedMapping = new()
        {
            [RoutingFailureReason.StationRequired] = OrderValidationFailureReason.StationRequired,
            [RoutingFailureReason.StationNotAssignedToItem] =
                OrderValidationFailureReason.StationNotAssignedToItem,
            [RoutingFailureReason.ItemHasNoStation] = OrderValidationFailureReason.ItemHasNoStation,
        };

        Assert.That(
            expectedMapping.Keys,
            Is.EquivalentTo(Enum.GetValues<RoutingFailureReason>()),
            "every routing failure reason needs a mapping this test pins down");

        foreach (RoutingFailureReason routingFailureReason in Enum.GetValues<RoutingFailureReason>())
        {
            SetUp();

            Assert.That(
                await ReasonProducedByAsync(expectedMapping[routingFailureReason]),
                Is.EqualTo(expectedMapping[routingFailureReason]));
        }
    }
}
