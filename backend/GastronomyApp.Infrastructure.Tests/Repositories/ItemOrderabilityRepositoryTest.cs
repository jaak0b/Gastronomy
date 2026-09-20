using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class ItemOrderabilityRepositoryTest
{
  [Test]
  public async Task FindActiveStationIdsAtFestivalAsync_AStationThatIsSwitchedOff_LeavesItOut()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    var bar = await fixture.DbContext.Stations.FirstAsync(station => station.Id == seeded.BarStationId, TestContext.CurrentContext.CancellationToken);
    bar.IsActive = false;
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ItemOrderabilityRepository repository = new(fixture.DbContext);

    IReadOnlyList<Guid> found = await repository.FindActiveStationIdsAtFestivalAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(found, Is.EqualTo(new[] { seeded.KitchenStationId }));
  }

  [Test]
  public async Task FindItemIdsPreparedByAsync_OneOfTheTwoStations_ReturnsOnlyWhatThatStationPrepares()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    ItemOrderabilityRepository repository = new(fixture.DbContext);

    IReadOnlyList<Guid> found = await repository.FindItemIdsPreparedByAsync(seeded.FestivalId, [seeded.KitchenStationId], TestContext.CurrentContext.CancellationToken);

    Assert.That(found, Is.EqualTo(new[] { seeded.SausageItemId }));
  }

  [Test]
  public async Task FindItemIdsPreparedByAsync_NoStationAtAll_ReturnsNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    ItemOrderabilityRepository repository = new(fixture.DbContext);

    IReadOnlyList<Guid> found = await repository.FindItemIdsPreparedByAsync(seeded.FestivalId, [], TestContext.CurrentContext.CancellationToken);

    Assert.That(found, Is.Empty);
  }

  [Test]
  public void FindItemIdsPreparedByAsync_NullStationIds_ThrowsArgumentNullException()
  {
    using SqliteInMemoryFixture fixture = new();
    ItemOrderabilityRepository repository = new(fixture.DbContext);

    Assert.That(async () => await repository.FindItemIdsPreparedByAsync(Guid.NewGuid(), null!, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public async Task FindActiveMenuItemIdsAsync_AnItemOnTheMenuThatIsSwitchedOff_LeavesItOut()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    var lemonade = await fixture.DbContext.CatalogItems.FirstAsync(item => item.Id == seeded.LemonadeItemId, TestContext.CurrentContext.CancellationToken);
    lemonade.IsActive = false;
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ItemOrderabilityRepository repository = new(fixture.DbContext);

    IReadOnlyList<Guid> found = await repository.FindActiveMenuItemIdsAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(found, Is.EqualTo(new[] { seeded.SausageItemId }));
  }
}
