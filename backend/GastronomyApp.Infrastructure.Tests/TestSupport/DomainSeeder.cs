using GastronomyApp.Core.Entities;

namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed record SeededDomain
{
    public required Guid ServerPersonId { get; init; }
    public required Guid DeviceId { get; init; }
    public required Guid KitchenLocationId { get; init; }
    public required Guid BarLocationId { get; init; }
    public required Guid SausageItemId { get; init; }
    public required Guid LemonadeItemId { get; init; }
}

public sealed class DomainSeeder
{
    public async Task<SeededDomain> SeedAsync(GastronomyAppDbContext dbContext, CancellationToken cancellationToken)
    {
        SeededDomain seeded = new()
        {
            ServerPersonId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            KitchenLocationId = Guid.NewGuid(),
            BarLocationId = Guid.NewGuid(),
            SausageItemId = Guid.NewGuid(),
            LemonadeItemId = Guid.NewGuid(),
        };

        DateTime now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);

        dbContext.ServerPeople.Add(new ServerPerson
        {
            Id = seeded.ServerPersonId,
            Name = "Anna",
            IsActive = true,
            CreatedAtUtc = now,
        });

        dbContext.ProductionLocations.Add(new ProductionLocation
        {
            Id = seeded.KitchenLocationId,
            Name = "Kueche",
            StationAccessKey = Guid.NewGuid().ToString("N"),
            SlipLanguage = "de",
            SortOrder = 1,
            IsActive = true,
        });

        dbContext.ProductionLocations.Add(new ProductionLocation
        {
            Id = seeded.BarLocationId,
            Name = "Theke",
            StationAccessKey = Guid.NewGuid().ToString("N"),
            SlipLanguage = "de",
            SortOrder = 2,
            IsActive = true,
        });

        dbContext.CatalogItems.Add(new CatalogItem
        {
            Id = seeded.SausageItemId,
            Name = "Bratwurst",
            CategoryName = "Speisen",
            PriceCents = 350,
            SortOrder = 1,
            IsActive = true,
            IsAvailable = true,
        });

        dbContext.CatalogItems.Add(new CatalogItem
        {
            Id = seeded.LemonadeItemId,
            Name = "Limonade",
            CategoryName = "Getraenke",
            PriceCents = 250,
            SortOrder = 2,
            IsActive = true,
            IsAvailable = true,
        });

        dbContext.ItemLocationAssignments.Add(new ItemLocationAssignment
        {
            Id = Guid.NewGuid(),
            CatalogItemId = seeded.SausageItemId,
            ProductionLocationId = seeded.KitchenLocationId,
        });

        dbContext.ItemLocationAssignments.Add(new ItemLocationAssignment
        {
            Id = Guid.NewGuid(),
            CatalogItemId = seeded.LemonadeItemId,
            ProductionLocationId = seeded.BarLocationId,
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return seeded;
    }
}
