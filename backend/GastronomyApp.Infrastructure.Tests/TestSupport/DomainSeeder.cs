namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed record SeededDomain
{
  public required Guid StaffMemberId { get; init; }

  public required Guid DeviceId { get; init; }

  public required Guid KitchenStationId { get; init; }

  public required Guid BarStationId { get; init; }

  public required Guid SausageItemId { get; init; }

  public required Guid LemonadeItemId { get; init; }
}

public sealed class DomainSeeder
{
  public async Task<SeededDomain> SeedAsync(GastronomyAppDbContext dbContext, CancellationToken cancellationToken)
  {
    SeededDomain seeded = new()
                          {
                            StaffMemberId = Guid.NewGuid(),
                            DeviceId = Guid.NewGuid(),
                            KitchenStationId = Guid.NewGuid(),
                            BarStationId = Guid.NewGuid(),
                            SausageItemId = Guid.NewGuid(),
                            LemonadeItemId = Guid.NewGuid()
                          };

    DateTime now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);

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
                             IsActive = true,
                             NextStationOrderNumber = 1
                           });

    dbContext.Stations.Add(new()
                           {
                             Id = seeded.BarStationId,
                             Name = "Theke",
                             SortOrder = 2,
                             IsActive = true,
                             NextStationOrderNumber = 1
                           });

    dbContext.CatalogItems.Add(new()
                               {
                                 Id = seeded.SausageItemId,
                                 Name = "Bratwurst",
                                 CategoryName = "Speisen",
                                 PriceCents = 350,
                                 SortOrder = 1,
                                 IsActive = true,
                                 IsAvailable = true
                               });

    dbContext.CatalogItems.Add(new()
                               {
                                 Id = seeded.LemonadeItemId,
                                 Name = "Limonade",
                                 CategoryName = "Getraenke",
                                 PriceCents = 250,
                                 SortOrder = 2,
                                 IsActive = true,
                                 IsAvailable = true
                               });

    dbContext.ItemStationAssignments.Add(new()
                                         {
                                           Id = Guid.NewGuid(),
                                           CatalogItemId = seeded.SausageItemId,
                                           StationId = seeded.KitchenStationId
                                         });

    dbContext.ItemStationAssignments.Add(new()
                                         {
                                           Id = Guid.NewGuid(),
                                           CatalogItemId = seeded.LemonadeItemId,
                                           StationId = seeded.BarStationId
                                         });

    await dbContext.SaveChangesAsync(cancellationToken);

    return seeded;
  }
}
