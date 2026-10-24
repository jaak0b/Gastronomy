using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Configurations;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Configurations;

[TestFixture]
public sealed class FestivalStationShapeConfigurationTest
{
  [Test]
  public void Configure_NullBuilder_ThrowsArgumentNullException()
  {
    FestivalStationShapeConfiguration configuration = new();

    Assert.That(() => configuration.Configure(null!), Throws.ArgumentNullException);
  }

  [Test]
  public async Task Save_AShapeInAnAreaOfAnotherFestivalThanItsStationLink_IsRefusedByTheDatabase()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var linkId = await FindKitchenLinkIdAsync(fixture.DbContext, seeded);
    var otherFestivalId = await AddFestivalAsync(fixture.DbContext);
    var strangerAreaId = await AddAreaAsync(fixture.DbContext, otherFestivalId);

    await using var second = fixture.CreateContext();
    second.FestivalStationShapes.Add(BuildShape(linkId, seeded.FestivalId, strangerAreaId));

    var refused = Assert.ThrowsAsync<DbUpdateException>(async () => await second.SaveChangesAsync(TestContext.CurrentContext.CancellationToken));

    Assert.That(refused!.InnerException!.Message, Does.Contain("FOREIGN KEY"));
  }

  [Test]
  public async Task Save_AShapeThatClaimsTheFestivalOfItsAreaButNotOfItsStationLink_IsRefusedByTheDatabase()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var linkId = await FindKitchenLinkIdAsync(fixture.DbContext, seeded);
    var otherFestivalId = await AddFestivalAsync(fixture.DbContext);
    var strangerAreaId = await AddAreaAsync(fixture.DbContext, otherFestivalId);

    await using var second = fixture.CreateContext();
    second.FestivalStationShapes.Add(BuildShape(linkId, otherFestivalId, strangerAreaId));

    var refused = Assert.ThrowsAsync<DbUpdateException>(async () => await second.SaveChangesAsync(TestContext.CurrentContext.CancellationToken));

    Assert.That(refused!.InnerException!.Message, Does.Contain("FOREIGN KEY"));
  }

  [Test]
  public async Task Save_AShapeInAnAreaOfItsOwnFestival_IsStored()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var linkId = await FindKitchenLinkIdAsync(fixture.DbContext, seeded);
    var gardenId = await AddAreaAsync(fixture.DbContext, seeded.FestivalId);

    await using var second = fixture.CreateContext();
    second.FestivalStationShapes.Add(BuildShape(linkId, seeded.FestivalId, gardenId));

    Assert.DoesNotThrowAsync(async () => await second.SaveChangesAsync(TestContext.CurrentContext.CancellationToken));
  }

  private async Task<Guid> FindKitchenLinkIdAsync(GastronomyAppDbContext dbContext, SeededDomain seeded)
  {
    var link = await dbContext.FestivalStations.SingleAsync(candidate => candidate.FestivalId == seeded.FestivalId && candidate.StationId == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    return link.Id;
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

  private async Task<Guid> AddAreaAsync(GastronomyAppDbContext dbContext, Guid festivalId)
  {
    var areaId = Guid.NewGuid();

    dbContext.Areas.Add(new()
                        {
                          Id = areaId,
                          FestivalId = festivalId,
                          Name = "Garten",
                          SortOrder = 1,
                          IsHidden = false
                        });
    await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return areaId;
  }

  private FestivalStationShape BuildShape(Guid linkId, Guid festivalId, Guid areaId)
  {
    return new()
           {
             FestivalStationId = linkId,
             FestivalId = festivalId,
             AreaId = areaId,
             X = 100,
             Y = 50,
             Width = 60,
             Height = 60,
             Rotation = 0
           };
  }
}
