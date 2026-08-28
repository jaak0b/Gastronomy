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
    private IStationRepository _stationRepository = null!;
    private INumberAllocator _numberAllocator = null!;
    private IClock _clock = null!;
    private OrderAcceptanceService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _orderRepository = A.Fake<IOrderRepository>();
        _catalogItemRepository = A.Fake<ICatalogItemRepository>();
        _stationRepository = A.Fake<IStationRepository>();
        _numberAllocator = A.Fake<INumberAllocator>();
        _clock = A.Fake<IClock>();

        A.CallTo(() => _clock.UtcNow).Returns(_now);
        A.CallTo(() => _orderRepository.FindByClientOrderIdAsync(A<Guid>._, A<CancellationToken>._))
            .Returns(Task.FromResult<Order?>(null));
        A.CallTo(() => _stationRepository.FindActiveAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyCollection<Station>>(
            [
                StationOf(_kitchenId, "Kueche", 1),
                StationOf(_barIndoorId, "Theke innen", 2),
            ]));
        A.CallTo(() => _numberAllocator.AllocateGlobalOrderNumberAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(137));
        A.CallTo(() => _numberAllocator.AllocateStationOrderNumberAsync(A<Guid>._, A<CancellationToken>._))
            .Returns(Task.FromResult(42));

        GivenCatalogItem(_bratwurstId, "Bratwurst", 350, [_kitchenId]);
        GivenCatalogItem(_beerId, "Bier", 400, [_barIndoorId]);

        _service = new OrderAcceptanceService(
            _orderRepository,
            _catalogItemRepository,
            _stationRepository,
            _numberAllocator,
            new OrderRoutingResolver(),
            _clock);
    }

    private Station StationOf(Guid id, string name, int sortOrder)
    {
        return new Station
        {
            Id = id,
            Name = name,
            SortOrder = sortOrder,
            IsActive = true,
            NextStationOrderNumber = 1,
        };
    }

    private void GivenCatalogItem(Guid id, string name, int priceCents, IReadOnlyCollection<Guid> stationIds)
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
        GivenAssignments(id, stationIds);
    }

    private void GivenAssignments(Guid catalogItemId, IReadOnlyCollection<Guid> stationIds)
    {
        List<ItemStationAssignment> assignments = stationIds
            .Select(stationId => new ItemStationAssignment
            {
                Id = Guid.NewGuid(),
                CatalogItemId = catalogItemId,
                StationId = stationId,
            })
            .ToList();

        A.CallTo(() => _catalogItemRepository.FindAssignmentsAsync(catalogItemId, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyCollection<ItemStationAssignment>>(assignments));
    }

    private OrderAcceptanceRequest RequestWith(
        IReadOnlyList<OrderAcceptanceItemRequest> items,
        string tableName = "Tisch 12")
    {
        return new OrderAcceptanceRequest
        {
            ClientOrderId = _clientOrderId,
            StaffMemberId = _staffMemberId,
            TableName = tableName,
            Note = null,
            Items = items,
        };
    }

    private OrderAcceptanceItemRequest ItemFor(
        Guid catalogItemId,
        Guid? stationId = null,
        int unitPriceCents = 350)
    {
        return new OrderAcceptanceItemRequest
        {
            CatalogItemId = catalogItemId,
            UnitPriceCents = unitPriceCents,
            Note = null,
            StationId = stationId,
        };
    }

    private static List<OrderItem> ItemsOf(Order order)
    {
        return [.. order.StationOrders.SelectMany(stationOrder => stationOrder.Items)];
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
            Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.NoItems));
        });
        AssertNothingWasAllocatedOrStored();
    }

    [Test]
    public async Task AcceptAsync_MoreItemsThanOneOrderMayHold_FailsWithTooManyItems()
    {
        IReadOnlyList<OrderAcceptanceItemRequest> items =
            [.. Enumerable.Range(0, 201).Select(_ => ItemFor(_bratwurstId))];

        Result<OrderAcceptanceResult, OrderValidationFailure> result =
            await _service.AcceptAsync(RequestWith(items), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.TooManyItems));
        });
        AssertNothingWasAllocatedOrStored();
    }

    [Test]
    public async Task AcceptAsync_AsManyItemsAsOneOrderMayHold_IsAccepted()
    {
        IReadOnlyList<OrderAcceptanceItemRequest> items =
            [.. Enumerable.Range(0, 200).Select(_ => ItemFor(_bratwurstId))];

        Result<OrderAcceptanceResult, OrderValidationFailure> result =
            await _service.AcceptAsync(RequestWith(items), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public async Task AcceptAsync_TheSameItemTwice_StoresOneRowPerItem()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([ItemFor(_bratwurstId), ItemFor(_bratwurstId)]),
            CancellationToken.None);

        Assert.That(ItemsOf(result.Value.Order), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task AcceptAsync_EmptyTableLabel_FailsWithTableLabelMissing()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([ItemFor(_bratwurstId)], string.Empty),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.TableNameMissing));
        });
        AssertNothingWasAllocatedOrStored();
    }

    [Test]
    public async Task AcceptAsync_WhitespaceTableLabel_FailsWithTableLabelMissing()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([ItemFor(_bratwurstId)], "   "),
            CancellationToken.None);

        Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.TableNameMissing));
    }

    [Test]
    public async Task AcceptAsync_TableLabelOfFortyOneCharacters_FailsWithTableLabelTooLong()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([ItemFor(_bratwurstId)], new string('T', 41)),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.TableNameTooLong));
        });
        AssertNothingWasAllocatedOrStored();
    }

    [Test]
    public async Task AcceptAsync_TableLabelOfFortyCharacters_PassesTheTableLabelCheck()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([ItemFor(_bratwurstId)], new string('T', 40)),
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
            RequestWith([ItemFor(unknownId), ItemFor(secondUnknownId)]),
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
        A.CallTo(() => _stationRepository.FindActiveAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyCollection<Station>>(
            [
                StationOf(_barIndoorId, "Theke innen", 2),
                StationOf(_barOutdoorId, "Theke aussen", 3),
            ]));

        Result<OrderAcceptanceResult, OrderValidationFailure> result =
            await _service.AcceptAsync(RequestWith([ItemFor(_beerId)]), CancellationToken.None);

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
            RequestWith([ItemFor(_bratwurstId, _barIndoorId)]),
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
            await _service.AcceptAsync(RequestWith([ItemFor(_bratwurstId)]), CancellationToken.None);

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
            await _service.AcceptAsync(RequestWith([ItemFor(_bratwurstId)]), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public async Task AcceptAsync_FirstTimeClientOrderId_AllocatesNumbersAndStoresTheOrderOnce()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([ItemFor(_bratwurstId), ItemFor(_beerId)]),
            CancellationToken.None);

        Order order = result.Value.Order;

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.WasAlreadyAccepted, Is.False);
            Assert.That(order.GlobalOrderNumber, Is.EqualTo(137));
            Assert.That(ItemsOf(order), Has.Count.EqualTo(2));
            Assert.That(order.StationOrders, Has.Count.EqualTo(2));
            Assert.That(order.StaffMemberId, Is.EqualTo(_staffMemberId));
            Assert.That(order.ClientOrderId, Is.EqualTo(_clientOrderId));
        });

        A.CallTo(() => _numberAllocator.AllocateGlobalOrderNumberAsync(A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _numberAllocator.AllocateStationOrderNumberAsync(A<Guid>._, A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();
        A.CallTo(() => _orderRepository.AddAsync(order, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task AcceptAsync_TwoLinesAtOneStation_AllocatesOneSequenceNumberForOneTicket()
    {
        GivenCatalogItem(_beerId, "Bier", 400, [_kitchenId]);

        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([ItemFor(_bratwurstId), ItemFor(_beerId)]),
            CancellationToken.None);

        Order order = result.Value.Order;

        Assert.Multiple(() =>
        {
            Assert.That(order.StationOrders, Has.Count.EqualTo(1));
            Assert.That(ItemsOf(order), Has.Count.EqualTo(2));
            Assert.That(ItemsOf(order).Select(item => item.StationOrderId).Distinct().Count(), Is.EqualTo(1));
            Assert.That(ItemsOf(order)[0].StationOrderId, Is.EqualTo(order.StationOrders[0].Id));
        });
        A.CallTo(() => _numberAllocator.AllocateStationOrderNumberAsync(_kitchenId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task AcceptAsync_LinesAtTwoStations_CreatesOneTicketPerStationWithItsOwnSequenceNumber()
    {
        A.CallTo(() => _numberAllocator.AllocateStationOrderNumberAsync(_kitchenId, A<CancellationToken>._))
            .Returns(Task.FromResult(42));
        A.CallTo(() => _numberAllocator.AllocateStationOrderNumberAsync(_barIndoorId, A<CancellationToken>._))
            .Returns(Task.FromResult(7));

        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([ItemFor(_bratwurstId), ItemFor(_beerId)]),
            CancellationToken.None);

        Order order = result.Value.Order;
        StationOrder kitchenTicket = order.StationOrders.Single(stationOrder => stationOrder.StationId == _kitchenId);
        StationOrder barTicket = order.StationOrders.Single(stationOrder => stationOrder.StationId == _barIndoorId);

        Assert.Multiple(() =>
        {
            Assert.That(kitchenTicket.StationOrderNumber, Is.EqualTo(42));
            Assert.That(barTicket.StationOrderNumber, Is.EqualTo(7));
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
            TableName = "Tisch 12",
            CreatedAtUtc = _now,
        };
        A.CallTo(() => _orderRepository.FindByClientOrderIdAsync(_clientOrderId, A<CancellationToken>._))
            .Returns(Task.FromResult<Order?>(existing));

        Result<OrderAcceptanceResult, OrderValidationFailure> result =
            await _service.AcceptAsync(RequestWith([ItemFor(_bratwurstId)]), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Order, Is.SameAs(existing));
            Assert.That(result.Value.WasAlreadyAccepted, Is.True);
        });
        AssertNothingWasAllocatedOrStored();
    }

    [Test]
    public async Task AcceptAsync_StaleChosenStation_RoutesToTheFallbackAndKeepsTheChoice()
    {
        GivenAssignments(_beerId, [_barIndoorId, _barOutdoorId]);

        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([ItemFor(_beerId, _barOutdoorId)]),
            CancellationToken.None);

        Order order = result.Value.Order;

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(order.StationOrders[0].StationId, Is.EqualTo(_barIndoorId));
        });
    }

    [Test]
    public async Task AcceptAsync_ValidRequest_GivesEveryCreatedRowItsOwnNonEmptyIdentifier()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([ItemFor(_bratwurstId), ItemFor(_beerId)]),
            CancellationToken.None);

        Order order = result.Value.Order;
        List<Guid> everyId =
        [
            order.Id,
            .. ItemsOf(order).Select(item => item.Id),
            .. order.StationOrders.Select(stationOrder => stationOrder.Id),
        ];

        Assert.Multiple(() =>
        {
            Assert.That(everyId, Has.None.EqualTo(Guid.Empty));
            Assert.That(everyId.Distinct().Count(), Is.EqualTo(everyId.Count));
            Assert.That(ItemsOf(order).Select(item => item.StationOrderId), Is.SubsetOf(order.StationOrders.Select(stationOrder => stationOrder.Id)));
            Assert.That(order.StationOrders.Select(stationOrder => stationOrder.OrderId), Is.All.EqualTo(order.Id));
        });
    }

    [Test]
    public async Task AcceptAsync_ValidRequest_StampsEveryCreatedRowWithTheClocksTime()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([ItemFor(_bratwurstId), ItemFor(_beerId)]),
            CancellationToken.None);

        Order order = result.Value.Order;

        Assert.Multiple(() =>
        {
            Assert.That(order.CreatedAtUtc, Is.EqualTo(_now));
            Assert.That(order.StationOrders.Select(stationOrder => stationOrder.PrintJobs[0].CreatedAtUtc), Is.All.EqualTo(_now));
        });
    }

    [Test]
    public async Task AcceptAsync_ValidRequest_StartsEveryTicketQueuedWithNoReprints()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith([ItemFor(_bratwurstId), ItemFor(_beerId)]),
            CancellationToken.None);

        Order order = result.Value.Order;

        Assert.Multiple(() =>
        {
            Assert.That(order.StationOrders.Select(stationOrder => stationOrder.PrintJobs[0].Status), Is.All.EqualTo(PrintJobStatus.Queued));
            Assert.That(order.StationOrders.Select(stationOrder => stationOrder.PrintJobs[0].CopyNumber), Is.All.EqualTo(0));
        });
    }

    [Test]
    public async Task AcceptAsync_ValidRequest_KeepsTheNameFromTheCatalogAndThePriceThePhoneShowed()
    {
        Result<OrderAcceptanceResult, OrderValidationFailure> result = await _service.AcceptAsync(
            RequestWith(
            [
                ItemFor(_bratwurstId, unitPriceCents: 350),
                ItemFor(_bratwurstId, unitPriceCents: 350),
                ItemFor(_beerId, unitPriceCents: 400),
            ]),
            CancellationToken.None);

        Order order = result.Value.Order;
        OrderItem bratwurstLine = ItemsOf(order).First(item => item.CatalogItemId == _bratwurstId);
        OrderItem beerLine = ItemsOf(order).Single(item => item.CatalogItemId == _beerId);

        Assert.Multiple(() =>
        {
            Assert.That(bratwurstLine.ItemName, Is.EqualTo("Bratwurst"));
            Assert.That(bratwurstLine.UnitPriceCents, Is.EqualTo(350));
            Assert.That(ItemsOf(order).Count(item => item.CatalogItemId == _bratwurstId), Is.EqualTo(2));
            Assert.That(beerLine.ItemName, Is.EqualTo("Bier"));
            Assert.That(beerLine.UnitPriceCents, Is.EqualTo(400));
        });
    }

    private async Task<OrderValidationFailureReason> ReasonProducedByAsync(OrderValidationFailureReason scenario)
    {
        OrderAcceptanceRequest request = scenario switch
        {
            OrderValidationFailureReason.NoItems => RequestWith([]),
            OrderValidationFailureReason.TooManyItems =>
                RequestWith([.. Enumerable.Range(0, 201).Select(_ => ItemFor(_bratwurstId))]),
            OrderValidationFailureReason.PriceOutOfRange =>
                RequestWith([ItemFor(_bratwurstId, unitPriceCents: -1)]),
            OrderValidationFailureReason.TableNameMissing =>
                RequestWith([ItemFor(_bratwurstId)], string.Empty),
            OrderValidationFailureReason.TableNameTooLong =>
                RequestWith([ItemFor(_bratwurstId)], new string('T', 41)),
            OrderValidationFailureReason.UnknownCatalogItemId => RequestWith([ItemFor(UnknownItemId())]),
            OrderValidationFailureReason.StationRequired => RequestWith([ItemFor(AmbiguouslyRoutedItemId())]),
            OrderValidationFailureReason.StationNotAssignedToItem =>
                RequestWith([ItemFor(_bratwurstId, _barIndoorId)]),
            OrderValidationFailureReason.ItemHasNoStation =>
                RequestWith([ItemFor(ItemWithNoActiveStationId())]),
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
        A.CallTo(() => _stationRepository.FindActiveAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyCollection<Station>>(
            [
                StationOf(_barIndoorId, "Theke innen", 2),
                StationOf(_barOutdoorId, "Theke aussen", 3),
            ]));

        return _beerId;
    }

    private Guid ItemWithNoActiveStationId()
    {
        GivenAssignments(_beerId, [_barIndoorId]);
        A.CallTo(() => _stationRepository.FindActiveAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyCollection<Station>>([]));

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
