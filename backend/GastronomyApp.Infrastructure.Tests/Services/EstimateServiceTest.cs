using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class EstimateServiceTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _clock = new(new(_now));
    await MoveTheFestivalAsync(_now.AddHours(-5), _now.AddHours(5));
    await SetMinutesAsync(_seeded.SausageItemId, 4, false);
    _service = new(_fixture.DbContext, new CoreMappingFactory().Create(), new(_fixture.DbContext, new(), _clock), new());
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private readonly DateTime _now = new(2026, 9, 5, 20, 15, 0, DateTimeKind.Utc);
  private readonly OrderItemStatusSeeder _statusSeeder = new();

  private int _orderNumber;
  private FakeTimeProvider _clock = null!;
  private SqliteInMemoryFixture _fixture = null!;
  private SeededDomain _seeded = null!;
  private EstimateService _service = null!;

  [Test]
  public async Task ListAsync_NoFestivalIsRunning_ReportsNothing()
  {
    await MoveTheFestivalAsync(_now.AddDays(5), _now.AddDays(6));

    Assert.That(await _service.ListAsync(TestContext.CurrentContext.CancellationToken), Is.Empty);
  }

  [Test]
  public async Task ListAsync_AnArticleWithAndOneWithoutProductionMinutes_ReportsOnlyTheTimedAssignment()
  {
    var estimates = await _service.ListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(estimates, Is.EqualTo(new[] { new ItemEstimateView(_seeded.SausageItemId, _seeded.KitchenStationId, 4) }));
  }

  [Test]
  public async Task ListAsync_AnArticleThatWaitsInTheQueue_ReportsTheOpenSharedQueuePlusItsOwnMinutes()
  {
    var schnitzel = await AddArticleAsync("Schnitzel", 5, false, false);
    var pizza = await AddArticleAsync("Pizza", 30, true, false);
    await PlaceOpenItemsAsync(_seeded.KitchenStationId, _seeded.FestivalId, schnitzel, pizza);

    var estimates = await _service.ListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(estimates.Single(estimate => estimate.CatalogItemId == _seeded.SausageItemId).ReadyInMinutes, Is.EqualTo(9));
  }

  [Test]
  public async Task ListAsync_AnIndependentArticle_ReportsWhatIsStillOpenOfThatArticleAlonePlusItsOwnMinutes()
  {
    var fries = await AddArticleAsync("Pommes", 3, true, true);
    var schnitzel = await AddArticleAsync("Schnitzel", 10, false, false);
    var pizza = await AddArticleAsync("Pizza", 20, true, false);
    await PlaceOpenItemsAsync(_seeded.KitchenStationId, _seeded.FestivalId, schnitzel, fries, fries, pizza);

    var estimates = await _service.ListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(estimates.Single(estimate => estimate.CatalogItemId == fries).ReadyInMinutes, Is.EqualTo(9));
  }

  [Test]
  public async Task ListAsync_AnItemTheStationHandedOut_StaysOutOfTheQueue()
  {
    var schnitzel = await AddArticleAsync("Schnitzel", 5, false, false);
    var steak = await AddArticleAsync("Steak", 7, false, false);
    var itemIds = await PlaceOpenItemsAsync(_seeded.KitchenStationId, _seeded.FestivalId, schnitzel, steak);
    await HandOutAsync(itemIds[1]);

    var estimates = await _service.ListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(estimates.Single().ReadyInMinutes, Is.EqualTo(9));
  }

  [Test]
  public async Task ListAsync_ItemsOfTwoStationOrdersAtOneStation_SumsAcrossBothOfThem()
  {
    var schnitzel = await AddArticleAsync("Schnitzel", 5, false, false);
    var steak = await AddArticleAsync("Steak", 3, false, false);
    await PlaceOpenItemsAsync(_seeded.KitchenStationId, _seeded.FestivalId, schnitzel);
    await PlaceOpenItemsAsync(_seeded.KitchenStationId, _seeded.FestivalId, steak);

    var estimates = await _service.ListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(estimates.Single().ReadyInMinutes, Is.EqualTo(12));
  }

  [Test]
  public async Task ListAsync_OpenItemsOfAnotherFestivalAndAnotherStation_StayOutOfTheQueue()
  {
    var otherFestivalId = await AddOtherFestivalAsync();
    var schnitzel = await AddArticleAsync("Schnitzel", 50, false, false);
    var steak = await AddArticleAsync("Steak", 60, false, false);
    await PlaceOpenItemsAsync(_seeded.KitchenStationId, otherFestivalId, schnitzel);
    await PlaceOpenItemsAsync(_seeded.BarStationId, _seeded.FestivalId, steak);

    var estimates = await _service.ListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(estimates.Single().ReadyInMinutes, Is.EqualTo(4));
  }

  [Test]
  public async Task ListAsync_AStationThatIsSwitchedOff_IsLeftOut()
  {
    var kitchen = await _fixture.DbContext.Stations.SingleAsync(station => station.Id == _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    kitchen.IsActive = false;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(await _service.ListAsync(TestContext.CurrentContext.CancellationToken), Is.Empty);
  }

  [Test]
  public async Task ListAsync_TwoTimedArticles_ListsThemByTheirPlaceInTheMenuThenByStation()
  {
    var pizza = await AddArticleAsync("Pizza", 15, false, true);
    await AssignAsync(pizza, _seeded.BarStationId);

    var estimates = await _service.ListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(estimates.Select(estimate => (estimate.CatalogItemId, estimate.StationId)),
                Is.EqualTo(new[]
                           {
                             (pizza, _seeded.KitchenStationId),
                             (pizza, _seeded.BarStationId),
                             (_seeded.SausageItemId, _seeded.KitchenStationId)
                           }));
  }

  [Test]
  public async Task QuoteAsync_LinesAtTwoStations_AnswersOnceForEachStationInTheOrderOfTheRequest()
  {
    await SetMinutesAsync(_seeded.LemonadeItemId, 1, false);
    EstimateQuoteRequest request = RequestOf(Line(_seeded.LemonadeItemId, _seeded.BarStationId, 1), Line(_seeded.SausageItemId, _seeded.KitchenStationId, 2), Line(_seeded.SausageItemId, _seeded.BarStationId, 1));

    var quote = await _service.QuoteAsync(request, TestContext.CurrentContext.CancellationToken);

    Assert.That(quote.Value.Stations,
                Is.EqualTo(new[]
                           {
                             new StationQuoteView(_seeded.BarStationId, 5),
                             new StationQuoteView(_seeded.KitchenStationId, 8)
                           }));
  }

  [Test]
  public async Task QuoteAsync_ALineWithoutProductionMinutes_AnswersWithoutANumber()
  {
    var quote = await _service.QuoteAsync(RequestOf(Line(_seeded.LemonadeItemId, _seeded.BarStationId, 2)), TestContext.CurrentContext.CancellationToken);

    Assert.That(quote.Value.Stations, Is.EqualTo(new[] { new StationQuoteView(_seeded.BarStationId, null) }));
  }

  [Test]
  public async Task QuoteAsync_OpenWorkAtTheStation_AddsTheQueueToTheSharedLane()
  {
    var schnitzel = await AddArticleAsync("Schnitzel", 10, false, false);
    await PlaceOpenItemsAsync(_seeded.KitchenStationId, _seeded.FestivalId, schnitzel);

    var quote = await _service.QuoteAsync(RequestOf(Line(_seeded.SausageItemId, _seeded.KitchenStationId, 2)), TestContext.CurrentContext.CancellationToken);

    Assert.That(quote.Value.Stations.Single().ReadyInMinutes, Is.EqualTo(18));
  }

  [Test]
  public async Task QuoteAsync_AnIndependentArticleBesideAnOpenQueue_AnswersWithTheLongerLane()
  {
    var pizza = await AddArticleAsync("Pizza", 15, true, true);
    var schnitzel = await AddArticleAsync("Schnitzel", 2, false, false);
    await PlaceOpenItemsAsync(_seeded.KitchenStationId, _seeded.FestivalId, schnitzel, pizza);

    var quote = await _service.QuoteAsync(RequestOf(Line(_seeded.SausageItemId, _seeded.KitchenStationId, 1), Line(pizza, _seeded.KitchenStationId, 1)), TestContext.CurrentContext.CancellationToken);

    Assert.That(quote.Value.Stations.Single().ReadyInMinutes, Is.EqualTo(30));
  }

  [Test]
  public async Task QuoteAsync_NoLines_AnswersNoStations()
  {
    var quote = await _service.QuoteAsync(RequestOf(), TestContext.CurrentContext.CancellationToken);

    Assert.That(quote.Value.Stations, Is.Empty);
  }

  [Test]
  public async Task QuoteAsync_AnUnknownArticle_IsRefused()
  {
    var unknownArticleId = Guid.NewGuid();

    var quote = await _service.QuoteAsync(RequestOf(Line(unknownArticleId, _seeded.KitchenStationId, 1)), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(quote.IsError, Is.True);
                      Assert.That(quote.FirstError.Description, Does.Contain(unknownArticleId.ToString()));
                    });
  }

  [Test]
  public async Task QuoteAsync_AStationThatIsNotAtTheRunningFestival_IsRefused()
  {
    var unknownStationId = Guid.NewGuid();

    var quote = await _service.QuoteAsync(RequestOf(Line(_seeded.SausageItemId, unknownStationId, 1)), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(quote.IsError, Is.True);
                      Assert.That(quote.FirstError.Description, Does.Contain(unknownStationId.ToString()));
                    });
  }

  [Test]
  public async Task QuoteAsync_NoFestivalIsRunning_RefusesTheStation()
  {
    await MoveTheFestivalAsync(_now.AddDays(5), _now.AddDays(6));

    var quote = await _service.QuoteAsync(RequestOf(Line(_seeded.SausageItemId, _seeded.KitchenStationId, 1)), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(quote.IsError, Is.True);
                      Assert.That(quote.FirstError.Description, Does.Contain(_seeded.KitchenStationId.ToString()));
                    });
  }

  [Test]
  public async Task QuoteAsync_AStationThatIsSwitchedOff_IsRefused()
  {
    var kitchen = await _fixture.DbContext.Stations.SingleAsync(station => station.Id == _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    kitchen.IsActive = false;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var quote = await _service.QuoteAsync(RequestOf(Line(_seeded.SausageItemId, _seeded.KitchenStationId, 1)), TestContext.CurrentContext.CancellationToken);

    Assert.That(quote.IsError, Is.True);
  }

  private EstimateQuoteRequest RequestOf(params EstimateQuoteLine[] lines)
  {
    return new() { Lines = lines };
  }

  private EstimateQuoteLine Line(Guid catalogItemId, Guid stationId, int units)
  {
    return new()
           {
             CatalogItemId = catalogItemId,
             StationId = stationId,
             Units = units
           };
  }

  private async Task MoveTheFestivalAsync(DateTime startsAtUtc, DateTime endsAtUtc)
  {
    var festival = await _fixture.DbContext.Festivals.SingleAsync(row => row.Id == _seeded.FestivalId, TestContext.CurrentContext.CancellationToken);
    festival.StartsAtUtc = startsAtUtc;
    festival.EndsAtUtc = endsAtUtc;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task SetMinutesAsync(Guid catalogItemId, double? productionMinutes, bool isQueueIndependent)
  {
    var item = await _fixture.DbContext.CatalogItems.SingleAsync(row => row.Id == catalogItemId, TestContext.CurrentContext.CancellationToken);
    item.ProductionMinutes = productionMinutes;
    item.IsQueueIndependent = isQueueIndependent;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<Guid> AddArticleAsync(string name, double productionMinutes, bool isQueueIndependent, bool atTheKitchen)
  {
    CatalogItem item = new()
                       {
                         Id = Guid.NewGuid(),
                         Name = name,
                         CategoryId = _seeded.FoodCategoryId,
                         SortOrder = atTheKitchen ? 0 : 5,
                         IsActive = true,
                         ProductionMinutes = productionMinutes,
                         IsQueueIndependent = isQueueIndependent
                       };
    _fixture.DbContext.CatalogItems.Add(item);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    if (atTheKitchen)
      await AssignAsync(item.Id, _seeded.KitchenStationId);

    return item.Id;
  }

  private async Task AssignAsync(Guid catalogItemId, Guid stationId)
  {
    _fixture.DbContext.ItemStationAssignments.Add(new()
                                                  {
                                                    Id = Guid.NewGuid(),
                                                    FestivalId = _seeded.FestivalId,
                                                    CatalogItemId = catalogItemId,
                                                    StationId = stationId
                                                  });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<Guid> AddOtherFestivalAsync()
  {
    var festivalId = Guid.NewGuid();
    _fixture.DbContext.Festivals.Add(new()
                                     {
                                       Id = festivalId,
                                       Name = "Herbstfest",
                                       StartsAtUtc = _now.AddDays(30),
                                       EndsAtUtc = _now.AddDays(31),
                                       NextOrderNumber = 1,
                                       IsHidden = false
                                     });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return festivalId;
  }

  private async Task HandOutAsync(Guid orderItemId)
  {
    var item = await _fixture.DbContext.OrderItems.Include(row => row.CurrentStatus).Include(row => row.StationOrder).SingleAsync(row => row.Id == orderItemId, TestContext.CurrentContext.CancellationToken);
    _statusSeeder.HandOut(item, item.StationOrder.StationId, _now);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<IReadOnlyList<Guid>> PlaceOpenItemsAsync(Guid stationId, Guid festivalId, params Guid[] catalogItemIds)
  {
    var tableId = await new DomainSeeder().SeedTableAsync(_fixture.DbContext, festivalId, "Tisch 12", TestContext.CurrentContext.CancellationToken);
    var orderId = Guid.NewGuid();
    var stationOrderId = Guid.NewGuid();

    Order order = new()
                  {
                    Id = orderId,
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = festivalId,
                    GlobalOrderNumber = ++_orderNumber,
                    StaffMemberId = _seeded.StaffMemberId,
                    TableId = tableId,
                    CreatedAtUtc = _now
                  };

    StationOrder stationOrder = new()
                                {
                                  Id = stationOrderId,
                                  OrderId = orderId,
                                  FestivalId = festivalId,
                                  StationId = stationId,
                                  StationOrderNumber = _orderNumber,
                                  DeliveryMode = DeliveryMode.Together
                                };

    foreach (var catalogItemId in catalogItemIds)
    {
      OrderItem item = new()
                       {
                         Id = Guid.NewGuid(),
                         StationOrderId = stationOrderId,
                         CatalogItemId = catalogItemId,
                         ItemName = "Artikel",
                         UnitPriceCents = 350
                       };
      stationOrder.Items.Add(_statusSeeder.Open(item, _seeded.StaffMemberId, _now));
    }

    order.StationOrders.Add(stationOrder);
    _fixture.DbContext.Orders.Add(order);
    await _statusSeeder.SaveAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    return stationOrder.Items.Select(item => item.Id).ToList();
  }
}
