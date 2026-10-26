using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed class TablePlanSeeder
{
  private readonly DateTime _createdAtUtc = new(2026, 8, 27, 18, 30, 0, DateTimeKind.Utc);

  private readonly OrderItemStatusSeeder _statusSeeder = new();

  public async Task<Guid> AddFestivalAsync(GastronomyAppDbContext dbContext)
  {
    var festivalId = Guid.NewGuid();

    dbContext.Festivals.Add(new()
                            {
                              Id = festivalId,
                              Name = "Herbstfest",
                              StartsAtUtc = new(2030, 10, 1, 12, 0, 0, DateTimeKind.Utc),
                              EndsAtUtc = new(2030, 10, 2, 12, 0, 0, DateTimeKind.Utc),
                              NextOrderNumber = 1,
                              IsHidden = false
                            });
    await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return festivalId;
  }

  public async Task<Guid> AddAreaAsync(GastronomyAppDbContext dbContext, Guid festivalId, string name, int sortOrder, bool isHidden)
  {
    var areaId = Guid.NewGuid();

    dbContext.Areas.Add(new()
                        {
                          Id = areaId,
                          FestivalId = festivalId,
                          Name = name,
                          SortOrder = sortOrder,
                          IsHidden = isHidden
                        });
    await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return areaId;
  }

  public async Task<Guid> AddTableAsync(GastronomyAppDbContext dbContext, Guid festivalId, Guid areaId, string name, bool isHidden)
  {
    var tableId = Guid.NewGuid();

    dbContext.Tables.Add(new()
                         {
                           Id = tableId,
                           FestivalId = festivalId,
                           AreaId = areaId,
                           Name = name,
                           X = 10,
                           Y = 20,
                           Width = 80,
                           Height = 40,
                           Rotation = 0,
                           IsHidden = isHidden
                         });
    await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return tableId;
  }

  public async Task AddOrderAsync(GastronomyAppDbContext dbContext, SeededDomain seeded, Guid tableId, int globalOrderNumber, DateTime? handedOutAtUtc, DateTime? settledAtUtc, Guid? festivalId = null)
  {
    var orderId = Guid.NewGuid();
    var orderFestivalId = festivalId ?? seeded.FestivalId;
    var stationOrderId = Guid.NewGuid();
    Order order = new()
                  {
                    Id = orderId,
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = orderFestivalId,
                    GlobalOrderNumber = globalOrderNumber,
                    StaffMemberId = seeded.StaffMemberId,
                    TableId = tableId,
                    CreatedAtUtc = _createdAtUtc
                  };
    StationOrder stationOrder = new()
                                {
                                  Id = stationOrderId,
                                  OrderId = orderId,
                                  FestivalId = orderFestivalId,
                                  StationId = seeded.KitchenStationId,
                                  StationOrderNumber = globalOrderNumber,
                                  DeliveryMode = DeliveryMode.Together
                                };
    OrderItem item = new()
                     {
                       Id = Guid.NewGuid(),
                       StationOrderId = stationOrderId,
                       CatalogItemId = seeded.SausageItemId,
                       ItemName = "Bratwurst",
                       UnitPriceCents = 350,
                       SettledAtUtc = settledAtUtc,
                       ChargedPriceCents = settledAtUtc is null ? null : 350,
                       SettledByStaffMemberId = settledAtUtc is null ? null : seeded.StaffMemberId
                     };
    stationOrder.Items.Add(_statusSeeder.OpenAndHandOutOnSaveWhenAHandOutTimeIsGiven(item, seeded.StaffMemberId, _createdAtUtc, seeded.KitchenStationId, handedOutAtUtc));
    order.StationOrders.Add(stationOrder);
    dbContext.Orders.Add(order);
    await _statusSeeder.SaveAsync(dbContext, TestContext.CurrentContext.CancellationToken);
  }

  public async Task AddOrderAtKitchenAndBarAsync(GastronomyAppDbContext dbContext, SeededDomain seeded, Guid tableId, int globalOrderNumber, DateTime? kitchenHandedOutAtUtc)
  {
    var orderId = Guid.NewGuid();
    Order order = new()
                  {
                    Id = orderId,
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = seeded.FestivalId,
                    GlobalOrderNumber = globalOrderNumber,
                    StaffMemberId = seeded.StaffMemberId,
                    TableId = tableId,
                    CreatedAtUtc = _createdAtUtc
                  };
    order.StationOrders.Add(StationOrderWithOneItem(seeded, orderId, globalOrderNumber, seeded.KitchenStationId, seeded.SausageItemId, "Bratwurst", kitchenHandedOutAtUtc));
    order.StationOrders.Add(StationOrderWithOneItem(seeded, orderId, globalOrderNumber, seeded.BarStationId, seeded.LemonadeItemId, "Limonade", null));
    dbContext.Orders.Add(order);
    await _statusSeeder.SaveAsync(dbContext, TestContext.CurrentContext.CancellationToken);
  }

  private StationOrder StationOrderWithOneItem(SeededDomain seeded, Guid orderId, int stationOrderNumber, Guid stationId, Guid catalogItemId, string itemName, DateTime? handedOutAtUtc)
  {
    StationOrder stationOrder = new()
                                {
                                  Id = Guid.NewGuid(),
                                  OrderId = orderId,
                                  FestivalId = seeded.FestivalId,
                                  StationId = stationId,
                                  StationOrderNumber = stationOrderNumber,
                                  DeliveryMode = DeliveryMode.Together
                                };
    OrderItem item = new()
                     {
                       Id = Guid.NewGuid(),
                       StationOrderId = stationOrder.Id,
                       CatalogItemId = catalogItemId,
                       ItemName = itemName,
                       UnitPriceCents = 350
                     };
    stationOrder.Items.Add(_statusSeeder.OpenAndHandOutOnSaveWhenAHandOutTimeIsGiven(item, seeded.StaffMemberId, _createdAtUtc, stationId, handedOutAtUtc));

    return stationOrder;
  }

  public async Task PlaceAsync(GastronomyAppDbContext dbContext, Guid festivalId, Guid stationId, Guid areaId)
  {
    var link = await dbContext.FestivalStations.SingleAsync(candidate => candidate.FestivalId == festivalId && candidate.StationId == stationId, TestContext.CurrentContext.CancellationToken);

    dbContext.FestivalStationShapes.Add(new()
                                        {
                                          FestivalStationId = link.Id,
                                          FestivalId = festivalId,
                                          AreaId = areaId,
                                          X = 100,
                                          Y = 50,
                                          Width = 60,
                                          Height = 60,
                                          Rotation = 0
                                        });
    await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }
}
