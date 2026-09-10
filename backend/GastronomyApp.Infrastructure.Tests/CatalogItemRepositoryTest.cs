using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;

namespace GastronomyApp.Infrastructure.Tests;

[TestFixture]
public sealed class CatalogItemRepositoryTest
{
  [Test]
  public async Task FindAssignmentsAsync_TheSameItemPreparedElsewhereAtAnotherFestival_ReturnsOnlyTheAskedFestival()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    var lastYearId = Guid.NewGuid();
    fixture.DbContext.Festivals.Add(new()
                                    {
                                      Id = lastYearId,
                                      Name = "Sommerfest im letzten Jahr",
                                      StartsAtUtc = DateTime.UtcNow.AddYears(-1),
                                      EndsAtUtc = DateTime.UtcNow.AddYears(-1).AddDays(2),
                                      NextOrderNumber = 1,
                                      IsHidden = false
                                    });

    fixture.DbContext.ItemStationAssignments.Add(new()
                                                 {
                                                   Id = Guid.NewGuid(),
                                                   FestivalId = lastYearId,
                                                   CatalogItemId = seeded.SausageItemId,
                                                   StationId = seeded.BarStationId
                                                 });

    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    CatalogItemRepository repository = new(fixture.DbContext);

    IReadOnlyCollection<ItemStationAssignment> found =
      await repository.FindAssignmentsAsync(seeded.FestivalId,
                                            seeded.SausageItemId,
                                            TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(found, Has.Count.EqualTo(1));
                      Assert.That(found.Single().StationId, Is.EqualTo(seeded.KitchenStationId));
                    });
  }
}
