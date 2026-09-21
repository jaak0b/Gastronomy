using FakeItEasy;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.OpenItems;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OrderAcceptanceServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _orderRepository = A.Fake<IOrderRepository>();
    _catalogItemRepository = A.Fake<ICatalogItemRepository>();
    _stationRepository = A.Fake<IStationRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _numberAllocator = A.Fake<INumberAllocator>();
    _clock = new FakeTimeProvider(new(_now));

    A.CallTo(() => _orderRepository.FindByClientOrderIdAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Order?>(null));
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(RunningFestival()));
    A.CallTo(() => _stationRepository.FindAtFestivalAsync(A<Guid>._, A<CancellationToken>._))
   .Returns(Task.FromResult<IReadOnlyCollection<Station>>([
                                                            BuildStation(_kitchenId, "Kueche", 1),
                                                            BuildStation(_barIndoorId, "Theke innen", 2)
                                                          ]));
    A.CallTo(() => _numberAllocator.AllocateGlobalOrderNumberAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult(137));
    A.CallTo(() => _numberAllocator.AllocateStationOrderNumberAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult(42));
    A.CallTo(() => _catalogItemRepository.FindMenuRowAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<FestivalCatalogItem?>(null));

    GivenCatalogItem(_bratwurstId, "Bratwurst", [_kitchenId]);
    GivenCatalogItem(_beerId, "Bier", [_barIndoorId]);

    _transactionRunner = A.Fake<ITransactionRunner>();
    A.CallTo(_transactionRunner).WithReturnType<Task<Result<Order, OrderValidationFailure>>>().ReturnsLazily(async call => (await call.GetArgument<Func<CancellationToken, Task<TransactionOutcome<Result<Order, OrderValidationFailure>>>>>(0)!(call.GetArgument<CancellationToken>(1))).Value);

    RunningFestivalLookup runningFestival = new(_festivalRepository, new(), _clock);

    _service = new(_orderRepository, runningFestival, _numberAllocator, new(_catalogItemRepository, _stationRepository, new()), new(A.Fake<IOpenItemRepository>(), runningFestival, _transactionRunner, _clock), _transactionRunner, _clock);
  }

  private readonly Guid _eventSessionId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
  private readonly Guid _staffMemberId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
  private readonly Guid _deviceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
  private readonly Guid _bratwurstId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
  private readonly Guid _beerId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
  private readonly Guid _kitchenId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
  private readonly Guid _barIndoorId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
  private readonly Guid _barOutdoorId = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
  private readonly Guid _clientOrderId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly DateTime _now = new(2026, 8, 26, 17, 42, 3, DateTimeKind.Utc);

  private IOrderRepository _orderRepository = null!;
  private ICatalogItemRepository _catalogItemRepository = null!;
  private IStationRepository _stationRepository = null!;
  private IFestivalRepository _festivalRepository = null!;
  private INumberAllocator _numberAllocator = null!;
  private TimeProvider _clock = null!;
  private ITransactionRunner _transactionRunner = null!;
  private OrderAcceptanceService _service = null!;

  private Festival RunningFestival()
  {
    return new()
           {
             Id = _festivalId,
             Name = "Sommerfest",
             StartsAtUtc = _now.AddHours(-5),
             EndsAtUtc = _now.AddHours(10),
             NextOrderNumber = 1,
             IsHidden = false
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

    A.CallTo(() => _catalogItemRepository.FindByIdAsync(id, A<CancellationToken>._)).Returns(Task.FromResult<CatalogItem?>(item));
    GivenAssignments(id, stationIds);
  }

  private void GivenAssignments(Guid catalogItemId, IReadOnlyCollection<Guid> stationIds)
  {
    List<ItemStationAssignment> assignments = stationIds.Select(stationId => new ItemStationAssignment
                                                                             {
                                                                               Id = Guid.NewGuid(),
                                                                               FestivalId = _festivalId,
                                                                               CatalogItemId = catalogItemId,
                                                                               StationId = stationId
                                                                             })
                                                        .ToList();

    A.CallTo(() => _catalogItemRepository.FindAssignmentsAsync(A<Guid>._, catalogItemId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyCollection<ItemStationAssignment>>(assignments));
  }

  private PlaceOrderRequest RequestWith(IReadOnlyList<OrderItemRequest> items, string tableName = "Tisch 12", IReadOnlyList<OrderDeliveryModeRequest>? deliveryModes = null)
  {
    return new()
           {
             ClientOrderId = _clientOrderId,
             TableName = tableName,
             Items = items,
             DeliveryModes = deliveryModes ?? []
           };
  }

  private OrderItemRequest ItemFor(Guid catalogItemId, Guid? stationId = null, int unitPriceCents = 350, OrderSettlementLineRequest? settlement = null)
  {
    return new()
           {
             CatalogItemId = catalogItemId,
             UnitPriceCents = unitPriceCents,
             Note = null,
             StationId = stationId,
             Settlement = settlement
           };
  }

  private static List<OrderItem> ReadOrderItems(Order order)
  {
    return order.StationOrders.SelectMany(stationOrder => stationOrder.Items).ToList();
  }

  private void AssertNothingWasAllocatedOrStored()
  {
    A.CallTo(_numberAllocator).MustNotHaveHappened();
    A.CallTo(() => _orderRepository.AddAsync(A<Order>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public void AcceptAsync_NullRequest_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _service.AcceptAsync(null!, _staffMemberId, CancellationToken.None), Throws.ArgumentNullException);
  }

  [Test]
  public async Task AcceptAsync_EmptyLines_FailsWithNoLinesAndAllocatesNothing()
  {
    Result<Order, OrderValidationFailure> result = await _service.AcceptAsync(RequestWith([]), _staffMemberId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.False);
                      Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.NoItems));
                    });
    AssertNothingWasAllocatedOrStored();
  }

  [Test]
  public async Task AcceptAsync_TheSameItemTwice_StoresOneRowPerItem()
  {
    Result<Order, OrderValidationFailure> result = await _service.AcceptAsync(RequestWith([
                                                                                            ItemFor(_bratwurstId),
                                                                                            ItemFor(_bratwurstId)
                                                                                          ]),
                                                                              _staffMemberId,
                                                                              CancellationToken.None);

    Assert.That(ReadOrderItems(result.Value), Has.Count.EqualTo(2));
  }

  [Test]
  public async Task AcceptAsync_EmptyTableName_FailsWithTableNameMissing()
  {
    Result<Order, OrderValidationFailure> result = await _service.AcceptAsync(RequestWith([ItemFor(_bratwurstId)], string.Empty), _staffMemberId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.False);
                      Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.TableNameMissing));
                    });
    AssertNothingWasAllocatedOrStored();
  }

  [Test]
  public async Task AcceptAsync_WhitespaceTableName_FailsWithTableNameMissing()
  {
    Result<Order, OrderValidationFailure> result = await _service.AcceptAsync(RequestWith([ItemFor(_bratwurstId)], "   "), _staffMemberId, CancellationToken.None);

    Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.TableNameMissing));
  }

  [Test]
  public async Task AcceptAsync_FirstTimeClientOrderId_AllocatesNumbersAndStoresTheOrderOnce()
  {
    Result<Order, OrderValidationFailure> result = await _service.AcceptAsync(RequestWith([
                                                                                            ItemFor(_bratwurstId),
                                                                                            ItemFor(_beerId)
                                                                                          ]),
                                                                              _staffMemberId,
                                                                              CancellationToken.None);

    var order = result.Value;

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.True);
                      Assert.That(order.GlobalOrderNumber, Is.EqualTo(137));
                      Assert.That(ReadOrderItems(order), Has.Count.EqualTo(2));
                      Assert.That(order.StationOrders, Has.Count.EqualTo(2));
                      Assert.That(order.StaffMemberId, Is.EqualTo(_staffMemberId));
                      Assert.That(order.ClientOrderId, Is.EqualTo(_clientOrderId));
                    });

    A.CallTo(() => _numberAllocator.AllocateGlobalOrderNumberAsync(A<Guid>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _numberAllocator.AllocateStationOrderNumberAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    A.CallTo(() => _orderRepository.AddAsync(order, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task AcceptAsync_TwoLinesAtOneStation_AllocatesOneSequenceNumberForOneTicket()
  {
    GivenCatalogItem(_beerId, "Bier", [_kitchenId]);

    Result<Order, OrderValidationFailure> result = await _service.AcceptAsync(RequestWith([
                                                                                            ItemFor(_bratwurstId),
                                                                                            ItemFor(_beerId)
                                                                                          ]),
                                                                              _staffMemberId,
                                                                              CancellationToken.None);

    var order = result.Value;

    Assert.Multiple(() =>
                    {
                      Assert.That(order.StationOrders, Has.Count.EqualTo(1));
                      Assert.That(ReadOrderItems(order), Has.Count.EqualTo(2));
                      Assert.That(ReadOrderItems(order).Select(item => item.StationOrderId).Distinct().Count(), Is.EqualTo(1));
                      Assert.That(ReadOrderItems(order)[0].StationOrderId, Is.EqualTo(order.StationOrders[0].Id));
                    });
    A.CallTo(() => _numberAllocator.AllocateStationOrderNumberAsync(A<Guid>._, _kitchenId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task AcceptAsync_LinesAtTwoStations_CreatesOneTicketPerStationWithItsOwnSequenceNumber()
  {
    A.CallTo(() => _numberAllocator.AllocateStationOrderNumberAsync(A<Guid>._, _kitchenId, A<CancellationToken>._)).Returns(Task.FromResult(42));
    A.CallTo(() => _numberAllocator.AllocateStationOrderNumberAsync(A<Guid>._, _barIndoorId, A<CancellationToken>._)).Returns(Task.FromResult(7));

    Result<Order, OrderValidationFailure> result = await _service.AcceptAsync(RequestWith([
                                                                                            ItemFor(_bratwurstId),
                                                                                            ItemFor(_beerId)
                                                                                          ]),
                                                                              _staffMemberId,
                                                                              CancellationToken.None);

    var order = result.Value;
    var kitchenTicket = order.StationOrders.Single(stationOrder => stationOrder.StationId == _kitchenId);
    var barTicket = order.StationOrders.Single(stationOrder => stationOrder.StationId == _barIndoorId);

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
                       FestivalId = _festivalId,
                       GlobalOrderNumber = 12,
                       StaffMemberId = _staffMemberId,
                       TableName = "Tisch 12",
                       CreatedAtUtc = _now
                     };
    A.CallTo(() => _orderRepository.FindByClientOrderIdAsync(_clientOrderId, A<CancellationToken>._)).Returns(Task.FromResult<Order?>(existing));

    Result<Order, OrderValidationFailure> result = await _service.AcceptAsync(RequestWith([ItemFor(_bratwurstId)]), _staffMemberId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.True);
                      Assert.That(result.Value, Is.SameAs(existing));
                    });
    AssertNothingWasAllocatedOrStored();
  }

  [Test]
  public async Task AcceptAsync_ValidRequest_GivesEveryCreatedRowItsOwnNonEmptyIdentifier()
  {
    Result<Order, OrderValidationFailure> result = await _service.AcceptAsync(RequestWith([
                                                                                            ItemFor(_bratwurstId),
                                                                                            ItemFor(_beerId)
                                                                                          ]),
                                                                              _staffMemberId,
                                                                              CancellationToken.None);

    var order = result.Value;
    List<Guid> everyId =
    [
      order.Id,
      .. ReadOrderItems(order).Select(item => item.Id),
      .. order.StationOrders.Select(stationOrder => stationOrder.Id)
    ];

    Assert.Multiple(() =>
                    {
                      Assert.That(everyId, Has.None.EqualTo(Guid.Empty));
                      Assert.That(everyId.Distinct().Count(), Is.EqualTo(everyId.Count));
                      Assert.That(ReadOrderItems(order).Select(item => item.StationOrderId), Is.SubsetOf(order.StationOrders.Select(stationOrder => stationOrder.Id)));
                      Assert.That(order.StationOrders.Select(stationOrder => stationOrder.OrderId), Is.All.EqualTo(order.Id));
                    });
  }

  [Test]
  public async Task AcceptAsync_ValidRequest_StampsEveryCreatedRowWithTheClocksTime()
  {
    Result<Order, OrderValidationFailure> result = await _service.AcceptAsync(RequestWith([
                                                                                            ItemFor(_bratwurstId),
                                                                                            ItemFor(_beerId)
                                                                                          ]),
                                                                              _staffMemberId,
                                                                              CancellationToken.None);

    var order = result.Value;

    Assert.That(order.CreatedAtUtc, Is.EqualTo(_now));
  }

  [Test]
  public async Task AcceptAsync_ValidRequest_LeavesEveryItemOpen()
  {
    Result<Order, OrderValidationFailure> result = await _service.AcceptAsync(RequestWith([
                                                                                            ItemFor(_bratwurstId),
                                                                                            ItemFor(_beerId)
                                                                                          ]),
                                                                              _staffMemberId,
                                                                              CancellationToken.None);

    Assert.That(ReadOrderItems(result.Value).Select(item => item.FulfilledAtUtc), Is.All.Null);
  }

  [Test]
  public async Task AcceptAsync_SettlementCoveringTheWholeOrder_ChargesEveryItemItsOwnPriceAndNamesTheCallerAsTheCollector()
  {
    Result<Order, OrderValidationFailure> result = await _service.AcceptAsync(RequestWith([
                                                                                            ItemFor(_bratwurstId, unitPriceCents: 350, settlement: new() { PaidPriceCents = 350 }),
                                                                                            ItemFor(_beerId, unitPriceCents: 400, settlement: new() { PaidPriceCents = 400 })
                                                                                          ]),
                                                                              _staffMemberId,
                                                                              CancellationToken.None);

    List<OrderItem> items = ReadOrderItems(result.Value);
    var bratwurstLine = items.Single(item => item.CatalogItemId == _bratwurstId);
    var beerLine = items.Single(item => item.CatalogItemId == _beerId);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.True);
                      Assert.That(bratwurstLine.ChargedPriceCents, Is.EqualTo(350));
                      Assert.That(beerLine.ChargedPriceCents, Is.EqualTo(400));
                      Assert.That(items.Select(item => item.SettledAtUtc), Is.All.EqualTo(_now));
                      Assert.That(items.Select(item => item.SettledByStaffMemberId), Is.All.EqualTo(_staffMemberId));
                      Assert.That(items.Select(item => item.PaymentNotice), Is.All.Null);
                      Assert.That(items.Select(item => item.FulfilledAtUtc), Is.All.Null);
                    });
  }

  [Test]
  public async Task AcceptAsync_OneLineSettledAndOneOpen_SettlesOnlyTheLineThatCarriesASettlement()
  {
    Result<Order, OrderValidationFailure> result = await _service.AcceptAsync(RequestWith([
                                                                                            ItemFor(_bratwurstId, unitPriceCents: 350, settlement: new() { PaidPriceCents = 350 }),
                                                                                            ItemFor(_beerId, unitPriceCents: 400)
                                                                                          ]),
                                                                              _staffMemberId,
                                                                              CancellationToken.None);

    List<OrderItem> items = ReadOrderItems(result.Value);
    var settledLine = items.Single(item => item.CatalogItemId == _bratwurstId);
    var openLine = items.Single(item => item.CatalogItemId == _beerId);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.True);
                      Assert.That(settledLine.ChargedPriceCents, Is.EqualTo(350));
                      Assert.That(settledLine.SettledAtUtc, Is.EqualTo(_now));
                      Assert.That(settledLine.SettledByStaffMemberId, Is.EqualTo(_staffMemberId));
                      Assert.That(openLine.ChargedPriceCents, Is.Null);
                      Assert.That(openLine.SettledAtUtc, Is.Null);
                      Assert.That(openLine.SettledByStaffMemberId, Is.Null);
                      Assert.That(openLine.PaymentNotice, Is.Null);
                    });
  }

  [Test]
  public async Task AcceptAsync_SettlementBelowTheTotalWithoutANotice_IsRefusedAndStoresNothing()
  {
    Result<Order, OrderValidationFailure> result = await _service.AcceptAsync(RequestWith([
                                                                                            ItemFor(_bratwurstId, settlement: new() { PaidPriceCents = 250 }),
                                                                                            ItemFor(_bratwurstId, settlement: new() { PaidPriceCents = 250 })
                                                                                          ]),
                                                                              _staffMemberId,
                                                                              CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.False);
                      Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.SettlementCannotBeProcessed));
                      Assert.That(result.Failure.SettlementFailureReason, Is.EqualTo(SettlementFailureReason.PaymentNoticeMissing));
                    });
    A.CallTo(() => _orderRepository.AddAsync(A<Order>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task AcceptAsync_SettlementSendingOnePricePerLine_StoresExactlyThePricesThePhoneSent()
  {
    Result<Order, OrderValidationFailure> result = await _service.AcceptAsync(RequestWith([
                                                                                            ItemFor(_bratwurstId,
                                                                                                    unitPriceCents: 333,
                                                                                                    settlement: new()
                                                                                                                {
                                                                                                                  PaidPriceCents = 167,
                                                                                                                  PaymentNotice = "Der Tisch zahlt den Rest spaeter"
                                                                                                                }),
                                                                                            ItemFor(_bratwurstId,
                                                                                                    unitPriceCents: 333,
                                                                                                    settlement: new()
                                                                                                                {
                                                                                                                  PaidPriceCents = 167,
                                                                                                                  PaymentNotice = "Der Tisch zahlt den Rest spaeter"
                                                                                                                }),
                                                                                            ItemFor(_bratwurstId,
                                                                                                    unitPriceCents: 333,
                                                                                                    settlement: new()
                                                                                                                {
                                                                                                                  PaidPriceCents = 166,
                                                                                                                  PaymentNotice = "Der Tisch zahlt den Rest spaeter"
                                                                                                                })
                                                                                          ]),
                                                                              _staffMemberId,
                                                                              CancellationToken.None);

    List<OrderItem> items = ReadOrderItems(result.Value);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.True);
                      Assert.That(items.Select(item => item.ChargedPriceCents),
                                  Is.EqualTo(new[]
                                             {
                                               167,
                                               167,
                                               166
                                             }));
                      Assert.That(items.Select(item => item.PaymentNotice), Is.All.EqualTo("Der Tisch zahlt den Rest spaeter"));
                    });
  }

  [Test]
  public async Task AcceptAsync_NoSettlement_LeavesEveryItemUnsettled()
  {
    Result<Order, OrderValidationFailure> result = await _service.AcceptAsync(RequestWith([
                                                                                            ItemFor(_bratwurstId),
                                                                                            ItemFor(_beerId)
                                                                                          ]),
                                                                              _staffMemberId,
                                                                              CancellationToken.None);

    List<OrderItem> items = ReadOrderItems(result.Value);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.True);
                      Assert.That(items.Select(item => item.SettledAtUtc), Is.All.Null);
                      Assert.That(items.Select(item => item.ChargedPriceCents), Is.All.Null);
                      Assert.That(items.Select(item => item.SettledByStaffMemberId), Is.All.Null);
                      Assert.That(items.Select(item => item.PaymentNotice), Is.All.Null);
                    });
  }

  [Test]
  public async Task AcceptAsync_NoDeliveryModeNamed_SendsEveryStationOrderTogether()
  {
    Result<Order, OrderValidationFailure> result = await _service.AcceptAsync(RequestWith([
                                                                                            ItemFor(_bratwurstId),
                                                                                            ItemFor(_beerId)
                                                                                          ]),
                                                                              _staffMemberId,
                                                                              CancellationToken.None);

    Assert.That(result.Value.StationOrders.Select(stationOrder => stationOrder.DeliveryMode), Is.All.EqualTo(DeliveryMode.Together));
  }

  [Test]
  public async Task AcceptAsync_DeliveryModeNamedForOneStation_AppliesItToThatStationOrderOnly()
  {
    var request = RequestWith([
                                ItemFor(_bratwurstId),
                                ItemFor(_beerId)
                              ],
                              deliveryModes:
                              [
                                new()
                                {
                                  StationId = _barIndoorId,
                                  DeliveryMode = DeliveryMode.AsItComes
                                }
                              ]);

    Result<Order, OrderValidationFailure> result = await _service.AcceptAsync(request, _staffMemberId, CancellationToken.None);

    var order = result.Value;
    var kitchenStationOrder = order.StationOrders.Single(stationOrder => stationOrder.StationId == _kitchenId);
    var barStationOrder = order.StationOrders.Single(stationOrder => stationOrder.StationId == _barIndoorId);

    Assert.Multiple(() =>
                    {
                      Assert.That(kitchenStationOrder.DeliveryMode, Is.EqualTo(DeliveryMode.Together));
                      Assert.That(barStationOrder.DeliveryMode, Is.EqualTo(DeliveryMode.AsItComes));
                    });
  }

  [Test]
  public async Task AcceptAsync_ValidRequest_KeepsTheNameFromTheCatalogAndThePriceThePhoneShowed()
  {
    Result<Order, OrderValidationFailure> result = await _service.AcceptAsync(RequestWith([
                                                                                            ItemFor(_bratwurstId, unitPriceCents: 350),
                                                                                            ItemFor(_bratwurstId, unitPriceCents: 350),
                                                                                            ItemFor(_beerId, unitPriceCents: 400)
                                                                                          ]),
                                                                              _staffMemberId,
                                                                              CancellationToken.None);

    var order = result.Value;
    var bratwurstLine = ReadOrderItems(order).First(item => item.CatalogItemId == _bratwurstId);
    var beerLine = ReadOrderItems(order).Single(item => item.CatalogItemId == _beerId);

    Assert.Multiple(() =>
                    {
                      Assert.That(bratwurstLine.ItemName, Is.EqualTo("Bratwurst"));
                      Assert.That(bratwurstLine.UnitPriceCents, Is.EqualTo(350));
                      Assert.That(ReadOrderItems(order).Count(item => item.CatalogItemId == _bratwurstId), Is.EqualTo(2));
                      Assert.That(beerLine.ItemName, Is.EqualTo("Bier"));
                      Assert.That(beerLine.UnitPriceCents, Is.EqualTo(400));
                    });
  }

  private async Task<OrderValidationFailureReason> ReasonProducedByAsync(OrderValidationFailureReason scenario)
  {
    var request = scenario switch
                  {
                    OrderValidationFailureReason.NoItems => RequestWith([]),
                    OrderValidationFailureReason.PriceOutOfRange => RequestWith([ItemFor(_bratwurstId, unitPriceCents: -1)]),
                    OrderValidationFailureReason.TableNameMissing => RequestWith([ItemFor(_bratwurstId)], string.Empty),
                    OrderValidationFailureReason.UnknownCatalogItemId => RequestWith([ItemFor(UnknownItemId())]),
                    OrderValidationFailureReason.StationRequired => RequestWith([ItemFor(AmbiguouslyRoutedItemId())]),
                    OrderValidationFailureReason.StationNotAssignedToItem => RequestWith([ItemFor(_bratwurstId, _barIndoorId)]),
                    OrderValidationFailureReason.ItemHasNoStation => RequestWith([ItemFor(ItemWithNoActiveStationId())]),
                    OrderValidationFailureReason.ItemNotAvailable => RequestWith([ItemFor(SoldOutItemId())]),
                    OrderValidationFailureReason.ChosenStationNoLongerPreparesTheItem => RequestWith([ItemFor(ItemWithAStaleStationChoiceId(), _barOutdoorId)]),
                    OrderValidationFailureReason.NoRunningFestival => RequestWhileNoFestivalRuns(),
                    OrderValidationFailureReason.SettlementCannotBeProcessed => RequestWith([ItemFor(_bratwurstId, settlement: new() { PaidPriceCents = 1 })]),
                    _ => throw new InvalidOperationException($"No scenario covers {scenario}")
                  };

    Result<Order, OrderValidationFailure> result = await _service.AcceptAsync(request, _staffMemberId, CancellationToken.None);

    Assert.That(result.IsSuccess, Is.False, $"{scenario} should have been rejected");

    return result.Failure.Reason;
  }

  private PlaceOrderRequest RequestWhileNoFestivalRuns()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));

    return RequestWith([ItemFor(_bratwurstId)]);
  }

  private Guid UnknownItemId()
  {
    var unknownId = Guid.Parse("bbbbbbbb-0000-0000-0000-0000000000ff");
    A.CallTo(() => _catalogItemRepository.FindByIdAsync(unknownId, A<CancellationToken>._)).Returns(Task.FromResult<CatalogItem?>(null));

    return unknownId;
  }

  private Guid AmbiguouslyRoutedItemId()
  {
    GivenAssignments(_beerId,
                     [
                       _barIndoorId,
                       _barOutdoorId
                     ]);
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
    A.CallTo(() => _stationRepository.FindAtFestivalAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyCollection<Station>>([]));

    return _beerId;
  }

  private Guid ItemWithAStaleStationChoiceId()
  {
    GivenAssignments(_beerId,
                     [
                       _barIndoorId,
                       _barOutdoorId
                     ]);
    A.CallTo(() => _stationRepository.FindAtFestivalAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyCollection<Station>>([BuildStation(_barIndoorId, "Theke innen", 2)]));

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

  [Test]
  public async Task AcceptAsync_EveryValidationFailureReasonThisServiceDecides_IsProducedByARealRequest()
  {
    OrderValidationFailureReason[] reasonsDecidedByTheAcceptanceTransaction = [OrderValidationFailureReason.OrderNumberCouldNotBeAllocated];

    foreach (var reason in Enum.GetValues<OrderValidationFailureReason>().Except(reasonsDecidedByTheAcceptanceTransaction))
    {
      SetUp();

      Assert.That(await ReasonProducedByAsync(reason), Is.EqualTo(reason));
    }
  }
}
