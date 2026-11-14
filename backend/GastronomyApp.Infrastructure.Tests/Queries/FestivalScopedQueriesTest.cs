using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Queries;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Queries;

[TestFixture]
public sealed class FestivalScopedQueriesTest
{
  [Test]
  public async Task WhereFestivalIs_RowsOfEveryKindAtTwoFestivals_KeepsOnlyTheRowsOfTheOneAskedFor()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var otherFestivalId = await AddFestivalAsync(fixture);
    await AddARowOfEveryKindAsync(fixture, seeded, seeded.FestivalId, "Grill", "Pommes", "Salz", "Prise");
    await AddARowOfEveryKindAsync(fixture, seeded, otherFestivalId, "Kaffeebar", "Kuchen", "Mehl", "Löffel");
    await using var readContext = fixture.CreateContext();

    Dictionary<string, IReadOnlyList<Guid>> festivalsOfTheRowsKept = new()
                                                                     {
                                                                       [nameof(Area)] = await FestivalsOfTheRowsKeptAsync<Area>(readContext, otherFestivalId),
                                                                       [nameof(Table)] = await FestivalsOfTheRowsKeptAsync<Table>(readContext, otherFestivalId),
                                                                       [nameof(FestivalStationShape)] = await FestivalsOfTheRowsKeptAsync<FestivalStationShape>(readContext, otherFestivalId),
                                                                       [nameof(FestivalStation)] = await FestivalsOfTheRowsKeptAsync<FestivalStation>(readContext, otherFestivalId),
                                                                       [nameof(FestivalCatalogItem)] = await FestivalsOfTheRowsKeptAsync<FestivalCatalogItem>(readContext, otherFestivalId),
                                                                       [nameof(ItemStationAssignment)] = await FestivalsOfTheRowsKeptAsync<ItemStationAssignment>(readContext, otherFestivalId),
                                                                       [nameof(FestivalIngredient)] = await FestivalsOfTheRowsKeptAsync<FestivalIngredient>(readContext, otherFestivalId),
                                                                       [nameof(Order)] = await FestivalsOfTheRowsKeptAsync<Order>(readContext, otherFestivalId),
                                                                       [nameof(StationOrder)] = await FestivalsOfTheRowsKeptAsync<StationOrder>(readContext, otherFestivalId)
                                                                     };

    Assert.That(festivalsOfTheRowsKept,
                Is.EqualTo(new Dictionary<string, IReadOnlyList<Guid>>
                           {
                             [nameof(Area)] = [otherFestivalId],
                             [nameof(Table)] = [otherFestivalId],
                             [nameof(FestivalStationShape)] = [otherFestivalId],
                             [nameof(FestivalStation)] = [otherFestivalId],
                             [nameof(FestivalCatalogItem)] = [otherFestivalId],
                             [nameof(ItemStationAssignment)] = [otherFestivalId],
                             [nameof(FestivalIngredient)] = [otherFestivalId],
                             [nameof(Order)] = [otherFestivalId],
                             [nameof(StationOrder)] = [otherFestivalId]
                           }));
  }

  private async Task<IReadOnlyList<Guid>> FestivalsOfTheRowsKeptAsync<TEntity>(GastronomyAppDbContext readContext, Guid festivalId) where TEntity : class, IFestivalScoped
  {
    return await readContext.Set<TEntity>().WhereFestivalIs(festivalId).Select(row => row.FestivalId).ToListAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<Guid> AddFestivalAsync(SqliteInMemoryFixture fixture)
  {
    var festivalId = Guid.NewGuid();

    fixture.DbContext.Festivals.Add(new()
                                    {
                                      Id = festivalId,
                                      Name = "Herbstfest",
                                      StartsAtUtc = new(2030, 9, 1, 12, 0, 0, DateTimeKind.Utc),
                                      EndsAtUtc = new(2030, 9, 2, 3, 0, 0, DateTimeKind.Utc),
                                      NextOrderNumber = 1,
                                      IsHidden = false
                                    });
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return festivalId;
  }

  private async Task AddARowOfEveryKindAsync(SqliteInMemoryFixture fixture, SeededDomain seeded, Guid festivalId, string stationName, string itemName, string ingredientName, string unitName)
  {
    var cancellationToken = TestContext.CurrentContext.CancellationToken;
    var tableId = await new DomainSeeder().SeedTableAsync(fixture.DbContext, festivalId, "Tisch 1", cancellationToken);
    var areaId = await fixture.DbContext.Tables.Where(table => table.Id == tableId).Select(table => table.AreaId).SingleAsync(cancellationToken);
    Station station = new()
                      {
                        Id = Guid.NewGuid(),
                        Name = stationName,
                        SortOrder = 3,
                        IsActive = true
                      };
    FestivalStation link = new()
                           {
                             Id = Guid.NewGuid(),
                             FestivalId = festivalId,
                             StationId = station.Id,
                             NextStationOrderNumber = 1
                           };
    CatalogItem item = new()
                       {
                         Id = Guid.NewGuid(),
                         Name = itemName,
                         CategoryId = seeded.FoodCategoryId,
                         SortOrder = 3,
                         IsActive = true
                       };
    Unit unit = new()
                {
                  Id = Guid.NewGuid(),
                  Name = unitName
                };
    Ingredient ingredient = new()
                            {
                              Id = Guid.NewGuid(),
                              Name = ingredientName,
                              UnitId = unit.Id,
                              Unit = unit
                            };
    Order order = new()
                  {
                    Id = Guid.NewGuid(),
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = festivalId,
                    GlobalOrderNumber = 1,
                    StaffMemberId = seeded.StaffMemberId,
                    TableId = tableId,
                    CreatedAtUtc = new(2030, 9, 1, 18, 0, 0, DateTimeKind.Utc)
                  };
    order.StationOrders.Add(new()
                            {
                              Id = Guid.NewGuid(),
                              OrderId = order.Id,
                              FestivalId = festivalId,
                              StationId = station.Id,
                              StationOrderNumber = 1,
                              DeliveryMode = DeliveryMode.Together
                            });

    fixture.DbContext.Stations.Add(station);
    fixture.DbContext.FestivalStations.Add(link);
    fixture.DbContext.FestivalStationShapes.Add(new()
                                                {
                                                  FestivalStationId = link.Id,
                                                  FestivalId = festivalId,
                                                  AreaId = areaId,
                                                  X = 100,
                                                  Y = 50,
                                                  Width = 60,
                                                  Height = 30,
                                                  Rotation = 0
                                                });
    fixture.DbContext.CatalogItems.Add(item);
    fixture.DbContext.FestivalCatalogItems.Add(new()
                                               {
                                                 Id = Guid.NewGuid(),
                                                 FestivalId = festivalId,
                                                 CatalogItemId = item.Id,
                                                 PriceCents = 300,
                                                 IsAvailable = true
                                               });
    fixture.DbContext.ItemStationAssignments.Add(new()
                                                 {
                                                   Id = Guid.NewGuid(),
                                                   FestivalId = festivalId,
                                                   CatalogItemId = item.Id,
                                                   StationId = station.Id
                                                 });
    fixture.DbContext.Units.Add(unit);
    fixture.DbContext.Ingredients.Add(ingredient);
    fixture.DbContext.FestivalIngredients.Add(new()
                                              {
                                                Id = Guid.NewGuid(),
                                                FestivalId = festivalId,
                                                IngredientId = ingredient.Id,
                                                AvailableAmount = null
                                              });
    fixture.DbContext.Orders.Add(order);
    await fixture.DbContext.SaveChangesAsync(cancellationToken);
  }
}
