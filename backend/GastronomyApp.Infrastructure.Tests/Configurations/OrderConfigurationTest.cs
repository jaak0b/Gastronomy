using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Configurations;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Configurations;

[TestFixture]
public sealed class OrderConfigurationTest
{
  [Test]
  public void Configure_NullBuilder_ThrowsArgumentNullException()
  {
    OrderConfiguration configuration = new();

    Assert.That(() => configuration.Configure(null!), Throws.ArgumentNullException);
  }

  [Test]
  public async Task Save_AnOrderWhoseFestivalDiffersFromItsTables_IsRefusedByTheDatabase()
  {
    using SqliteInMemoryFixture fixture = new();
    DomainSeeder seeder = new();
    var seeded = await seeder.SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var otherFestivalId = await AddFestivalAsync(fixture.DbContext);
    var otherFestivalsTableId = await seeder.SeedTableAsync(fixture.DbContext, otherFestivalId, "Tisch 1", TestContext.CurrentContext.CancellationToken);

    await using var second = fixture.CreateContext();
    second.Orders.Add(BuildOrder(seeded, otherFestivalsTableId));

    var refused = Assert.ThrowsAsync<DbUpdateException>(async () => await second.SaveChangesAsync(TestContext.CurrentContext.CancellationToken));

    Assert.That(refused!.InnerException!.Message, Does.Contain("FOREIGN KEY"));
  }

  [Test]
  public async Task Save_AnOrderAtATableOfItsOwnFestival_ReadsBackWithThatTable()
  {
    using SqliteInMemoryFixture fixture = new();
    DomainSeeder seeder = new();
    var seeded = await seeder.SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var tableId = await seeder.SeedTableAsync(fixture.DbContext, seeded.FestivalId, "Tisch 7", TestContext.CurrentContext.CancellationToken);
    var order = BuildOrder(seeded, tableId);
    fixture.DbContext.Orders.Add(order);
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();
    var stored = await readContext.Orders.Include(candidate => candidate.Table).SingleAsync(candidate => candidate.Id == order.Id, TestContext.CurrentContext.CancellationToken);

    Assert.That(stored.Table.Name, Is.EqualTo("Tisch 7"));
  }

  [Test]
  public async Task Save_DeletingATableThatHasAnOrder_IsRefusedByTheDatabase()
  {
    using SqliteInMemoryFixture fixture = new();
    DomainSeeder seeder = new();
    var seeded = await seeder.SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var tableId = await seeder.SeedTableAsync(fixture.DbContext, seeded.FestivalId, "Tisch 7", TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.Orders.Add(BuildOrder(seeded, tableId));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var second = fixture.CreateContext();
    second.Tables.Remove(await second.Tables.SingleAsync(table => table.Id == tableId, TestContext.CurrentContext.CancellationToken));

    var refused = Assert.ThrowsAsync<DbUpdateException>(async () => await second.SaveChangesAsync(TestContext.CurrentContext.CancellationToken));

    Assert.That(refused!.InnerException!.Message, Does.Contain("FOREIGN KEY"));
  }

  private async Task<Guid> AddFestivalAsync(GastronomyAppDbContext dbContext)
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

  private Order BuildOrder(SeededDomain seeded, Guid tableId)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             ClientOrderId = Guid.NewGuid(),
             FestivalId = seeded.FestivalId,
             GlobalOrderNumber = 1,
             StaffMemberId = seeded.StaffMemberId,
             TableId = tableId,
             CreatedAtUtc = new(2026, 8, 27, 18, 30, 0, DateTimeKind.Utc)
           };
  }
}
