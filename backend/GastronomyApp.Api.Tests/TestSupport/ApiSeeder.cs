using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.TestSupport;

public sealed class ApiSeeder
{
  private readonly DateTime _baseline = new(2026, 8, 26, 19, 40, 0, DateTimeKind.Utc);

  public async Task<SeededWorld> SeedAsync(GastronomyAppDbContext context, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(context);

    SeededWorld world = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    context.Festivals.Add(new()
                          {
                            Id = world.FestivalId,
                            Name = "Sommerfest",
                            StartsAtUtc = DateTime.UtcNow.AddDays(-1),
                            EndsAtUtc = DateTime.UtcNow.AddYears(1),
                            NextOrderNumber = 1,
                            IsHidden = false
                          });

    context.StaffMembers.Add(new()
                             {
                               Id = world.StaffMemberId,
                               Name = "Anna",
                               IsActive = true,
                               CreatedAtUtc = _baseline
                             });

    AddStation(context, world, world.KitchenStationId, "Kueche", 1);
    AddStation(context, world, world.BarStationId, "Bar", 2);

    AddCategory(context, world.FoodCategoryId, "Essen", "#C62828", 1);
    AddCategory(context, world.DrinkCategoryId, "Getraenke", "#1565C0", 2);

    AddItem(context, world, world.BratwurstItemId, "Bratwurst mit Brot", world.FoodCategoryId, 350, 1, world.KitchenStationId);
    AddItem(context, world, world.BeerItemId, "Bier", world.DrinkCategoryId, 300, 2, world.BarStationId);

    AddTable(context, world);

    await context.SaveChangesAsync(cancellationToken);
    return world;
  }

  public async Task<Guid> SeedTableAsync(GastronomyAppDbContext context, Guid festivalId, string tableName, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(context);

    var existingTable = await context.Tables.FirstOrDefaultAsync(table => table.FestivalId == festivalId && table.Name == tableName, cancellationToken);

    if (existingTable is not null)
      return existingTable.Id;

    var areaId = Guid.NewGuid();
    var tableId = Guid.NewGuid();

    context.Areas.Add(new()
                      {
                        Id = areaId,
                        FestivalId = festivalId,
                        Name = $"Bereich von {tableName}",
                        SortOrder = 99,
                        IsHidden = true
                      });
    context.Tables.Add(new()
                       {
                         Id = tableId,
                         FestivalId = festivalId,
                         AreaId = areaId,
                         Name = tableName,
                         IsHidden = false,
                         Width = 80,
                         Height = 80
                       });

    await context.SaveChangesAsync(cancellationToken);
    return tableId;
  }

  private void AddTable(GastronomyAppDbContext context, SeededWorld world)
  {
    var areaId = Guid.NewGuid();

    context.Areas.Add(new()
                      {
                        Id = areaId,
                        FestivalId = world.FestivalId,
                        Name = "Festzelt",
                        SortOrder = 99,
                        IsHidden = true
                      });

    context.Tables.Add(new()
                       {
                         Id = world.TableId,
                         FestivalId = world.FestivalId,
                         AreaId = areaId,
                         Name = "Tisch 12",
                         IsHidden = false,
                         Width = 80,
                         Height = 80
                       });
  }

  private void AddStation(GastronomyAppDbContext context, SeededWorld world, Guid stationId, string name, int sortOrder)
  {
    context.Stations.Add(new()
                         {
                           Id = stationId,
                           Name = name,
                           SortOrder = sortOrder,
                           IsActive = true
                         });

    context.FestivalStations.Add(new()
                                 {
                                   Id = Guid.NewGuid(),
                                   FestivalId = world.FestivalId,
                                   StationId = stationId,
                                   NextStationOrderNumber = 1
                                 });
  }

  private void AddCategory(GastronomyAppDbContext context, Guid categoryId, string name, string colourHex, int sortOrder)
  {
    context.CatalogCategories.Add(new()
                                  {
                                    Id = categoryId,
                                    Name = name,
                                    ColourHex = colourHex,
                                    SortOrder = sortOrder,
                                    IsActive = true
                                  });
  }

  private void AddItem(GastronomyAppDbContext context, SeededWorld world, Guid itemId, string name, Guid categoryId, int priceCents, int sortOrder, Guid stationId)
  {
    context.CatalogItems.Add(new()
                             {
                               Id = itemId,
                               Name = name,
                               CategoryId = categoryId,
                               IsActive = true,
                               SortOrder = sortOrder
                             });

    context.FestivalCatalogItems.Add(new()
                                     {
                                       Id = Guid.NewGuid(),
                                       FestivalId = world.FestivalId,
                                       CatalogItemId = itemId,
                                       PriceCents = priceCents,
                                       IsAvailable = true
                                     });

    context.ItemStationAssignments.Add(new()
                                       {
                                         Id = Guid.NewGuid(),
                                         FestivalId = world.FestivalId,
                                         CatalogItemId = itemId,
                                         StationId = stationId
                                       });
  }
}
