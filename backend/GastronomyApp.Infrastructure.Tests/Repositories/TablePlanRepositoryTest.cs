using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class TablePlanRepositoryTest
{
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
}
