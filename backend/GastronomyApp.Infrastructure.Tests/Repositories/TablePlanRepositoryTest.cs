using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class TablePlanRepositoryTest
{
  [Test]
  public async Task FindAreaAsync_AnAreaOfAnotherFestival_ReturnsNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    TablePlanSeeder seeder = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var otherFestivalId = await seeder.AddFestivalAsync(fixture.DbContext);
    var strangerAreaId = await seeder.AddAreaAsync(fixture.DbContext, otherFestivalId, "Garten", 1, false);

    var area = await new TablePlanRepository(fixture.DbContext).FindAreaAsync(seeded.FestivalId, strangerAreaId, TestContext.CurrentContext.CancellationToken);

    Assert.That(area, Is.Null);
  }

  [Test]
  public async Task FindTableAsync_ATableOfAnotherFestival_ReturnsNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    TablePlanSeeder seeder = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var otherFestivalId = await seeder.AddFestivalAsync(fixture.DbContext);
    var strangerAreaId = await seeder.AddAreaAsync(fixture.DbContext, otherFestivalId, "Garten", 1, false);
    var strangerTableId = await seeder.AddTableAsync(fixture.DbContext, otherFestivalId, strangerAreaId, "Tisch 1", false);

    var table = await new TablePlanRepository(fixture.DbContext).FindTableAsync(seeded.FestivalId, strangerTableId, TestContext.CurrentContext.CancellationToken);

    Assert.That(table, Is.Null);
  }

  [Test]
  public async Task AddStationShapeAsync_AStationAtTheFestival_IsFoundByItsLinkAfterTheChangesAreSaved()
  {
    using SqliteInMemoryFixture fixture = new();
    TablePlanSeeder seeder = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var gardenId = await seeder.AddAreaAsync(fixture.DbContext, seeded.FestivalId, "Garten", 1, false);
    var link = await fixture.DbContext.FestivalStations.SingleAsync(candidate => candidate.FestivalId == seeded.FestivalId && candidate.StationId == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    TablePlanRepository repository = new(fixture.DbContext);

    await repository.AddStationShapeAsync(new()
                                          {
                                            FestivalStationId = link.Id,
                                            FestivalId = seeded.FestivalId,
                                            AreaId = gardenId,
                                            X = 100,
                                            Y = 50,
                                            Width = 60,
                                            Height = 60,
                                            Rotation = -30.5
                                          },
                                          TestContext.CurrentContext.CancellationToken);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();
    var shape = await new TablePlanRepository(readContext).FindStationShapeAsync(link.Id, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(shape?.AreaId, Is.EqualTo(gardenId));
                      Assert.That(shape?.Rotation, Is.EqualTo(-30.5));
                    });
  }

  [Test]
  public async Task FindStationShapeAsync_AStationNotOnThePlan_ReturnsNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    TablePlanSeeder seeder = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var link = await fixture.DbContext.FestivalStations.SingleAsync(candidate => candidate.FestivalId == seeded.FestivalId && candidate.StationId == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    var shape = await new TablePlanRepository(fixture.DbContext).FindStationShapeAsync(link.Id, TestContext.CurrentContext.CancellationToken);

    Assert.That(shape, Is.Null);
  }

  [Test]
  public async Task RemoveStationShape_AStationOnThePlan_TakesItOffThePlanWhenTheChangesAreSaved()
  {
    using SqliteInMemoryFixture fixture = new();
    TablePlanSeeder seeder = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var gardenId = await seeder.AddAreaAsync(fixture.DbContext, seeded.FestivalId, "Garten", 1, false);
    await seeder.PlaceAsync(fixture.DbContext, seeded.FestivalId, seeded.KitchenStationId, gardenId);
    var link = await fixture.DbContext.FestivalStations.SingleAsync(candidate => candidate.FestivalId == seeded.FestivalId && candidate.StationId == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    TablePlanRepository repository = new(fixture.DbContext);

    repository.RemoveStationShape((await repository.FindStationShapeAsync(link.Id, TestContext.CurrentContext.CancellationToken))!);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();
    var shapesLeft = await readContext.FestivalStationShapes.CountAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(shapesLeft, Is.Zero);
  }
}
