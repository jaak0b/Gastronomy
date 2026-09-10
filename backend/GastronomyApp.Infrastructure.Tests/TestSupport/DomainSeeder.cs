using GastronomyApp.Core.Services;

namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed record SeededDomain
{
  public required Guid StaffMemberId { get; init; }

  public required Guid DeviceId { get; init; }

  public required Guid KitchenStationId { get; init; }

  public required Guid BarStationId { get; init; }

  public required Guid FoodCategoryId { get; init; }

  public required Guid DrinkCategoryId { get; init; }

  public required Guid SausageItemId { get; init; }

  public required Guid LemonadeItemId { get; init; }

  public required Guid FestivalId { get; init; }
}

public sealed class DomainSeeder
{
  private readonly CatalogCategoryNaming _naming = new();

  public async Task<SeededDomain> SeedAsync(GastronomyAppDbContext dbContext, CancellationToken cancellationToken)
  {
    SeededDomain seeded = new()
                          {
                            StaffMemberId = Guid.NewGuid(),
                            DeviceId = Guid.NewGuid(),
                            KitchenStationId = Guid.NewGuid(),
                            BarStationId = Guid.NewGuid(),
                            FoodCategoryId = Guid.NewGuid(),
                            DrinkCategoryId = Guid.NewGuid(),
                            SausageItemId = Guid.NewGuid(),
                            LemonadeItemId = Guid.NewGuid(),
                            FestivalId = Guid.NewGuid()
                          };

    DateTime now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);

    dbContext.Festivals.Add(new()
                            {
                              Id = seeded.FestivalId,
                              Name = "Sommerfest",
                              StartsAtUtc = DateTime.UtcNow.AddDays(-1),
                              EndsAtUtc = DateTime.UtcNow.AddYears(1),
                              NextOrderNumber = 1,
                              IsHidden = false
                            });

    dbContext.StaffMembers.Add(new()
                               {
                                 Id = seeded.StaffMemberId,
                                 Name = "Anna",
                                 IsActive = true,
                                 CreatedAtUtc = now
                               });

    dbContext.Stations.Add(new()
                           {
                             Id = seeded.KitchenStationId,
                             Name = "Kueche",
                             SortOrder = 1,
                             IsActive = true
                           });

    dbContext.Stations.Add(new()
                           {
                             Id = seeded.BarStationId,
                             Name = "Theke",
                             SortOrder = 2,
                             IsActive = true
                           });

    dbContext.FestivalStations.Add(new()
                                   {
                                     Id = Guid.NewGuid(),
                                     FestivalId = seeded.FestivalId,
                                     StationId = seeded.KitchenStationId,
                                     NextStationOrderNumber = 1
                                   });

    dbContext.FestivalStations.Add(new()
                                   {
                                     Id = Guid.NewGuid(),
                                     FestivalId = seeded.FestivalId,
                                     StationId = seeded.BarStationId,
                                     NextStationOrderNumber = 1
                                   });

    dbContext.CatalogCategories.Add(new()
                                    {
                                      Id = seeded.FoodCategoryId,
                                      Name = "Speisen",
                                      NormalizedName = _naming.Normalized("Speisen"),
                                      ColourHex = "#C62828",
                                      SortOrder = 1,
                                      IsActive = true
                                    });

    dbContext.CatalogCategories.Add(new()
                                    {
                                      Id = seeded.DrinkCategoryId,
                                      Name = "Getraenke",
                                      NormalizedName = _naming.Normalized("Getraenke"),
                                      ColourHex = "#1565C0",
                                      SortOrder = 2,
                                      IsActive = true
                                    });

    dbContext.CatalogItems.Add(new()
                               {
                                 Id = seeded.SausageItemId,
                                 Name = "Bratwurst",
                                 CategoryId = seeded.FoodCategoryId,
                                 SortOrder = 1,
                                 IsActive = true
                               });

    dbContext.CatalogItems.Add(new()
                               {
                                 Id = seeded.LemonadeItemId,
                                 Name = "Limonade",
                                 CategoryId = seeded.DrinkCategoryId,
                                 SortOrder = 2,
                                 IsActive = true
                               });

    dbContext.FestivalCatalogItems.Add(new()
                                       {
                                         Id = Guid.NewGuid(),
                                         FestivalId = seeded.FestivalId,
                                         CatalogItemId = seeded.SausageItemId,
                                         PriceCents = 350,
                                         IsAvailable = true
                                       });

    dbContext.FestivalCatalogItems.Add(new()
                                       {
                                         Id = Guid.NewGuid(),
                                         FestivalId = seeded.FestivalId,
                                         CatalogItemId = seeded.LemonadeItemId,
                                         PriceCents = 250,
                                         IsAvailable = true
                                       });

    dbContext.ItemStationAssignments.Add(new()
                                         {
                                           Id = Guid.NewGuid(),
                                           FestivalId = seeded.FestivalId,
                                           CatalogItemId = seeded.SausageItemId,
                                           StationId = seeded.KitchenStationId
                                         });

    dbContext.ItemStationAssignments.Add(new()
                                         {
                                           Id = Guid.NewGuid(),
                                           FestivalId = seeded.FestivalId,
                                           CatalogItemId = seeded.LemonadeItemId,
                                           StationId = seeded.BarStationId
                                         });

    await dbContext.SaveChangesAsync(cancellationToken);

    return seeded;
  }
}
