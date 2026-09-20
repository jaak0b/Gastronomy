using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests;

[TestFixture]
public sealed class FestivalMenuRepositoryTest
{
  [Test]
  public async Task FindMenuRowAsync_AnItemOnTheFestivalsMenu_CarriesThePriceItCostsThere()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    FestivalMenuRepository repository = new(fixture.DbContext);

    FestivalCatalogItem? menuRow = await repository.FindMenuRowAsync(seeded.FestivalId,
                                                                     seeded.SausageItemId,
                                                                     TestContext.CurrentContext.CancellationToken);

    Assert.That(menuRow?.PriceCents, Is.EqualTo(350));
  }

  [Test]
  public async Task FindMenuRowAsync_AnItemThatIsNotOnTheMenu_ReturnsNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    FestivalMenuRepository repository = new(fixture.DbContext);

    Assert.That(await repository.FindMenuRowAsync(seeded.FestivalId,
                                                  Guid.NewGuid(),
                                                  TestContext.CurrentContext.CancellationToken),
                Is.Null);
  }

  [Test]
  public async Task CatalogItemExistsAsync_AnArticleNobodyEverCreated_AnswersFalse()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    FestivalMenuRepository repository = new(fixture.DbContext);

    Assert.Multiple(async () =>
                    {
                      Assert.That(await repository.CatalogItemExistsAsync(seeded.SausageItemId,
                                                                          TestContext.CurrentContext.CancellationToken),
                                  Is.True);
                      Assert.That(await repository.CatalogItemExistsAsync(Guid.NewGuid(),
                                                                          TestContext.CurrentContext.CancellationToken),
                                  Is.False);
                    });
  }

  [Test]
  public async Task FindStationIdsAtFestivalAsync_AFestivalWithTwoStations_NamesBothOfThem()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    FestivalMenuRepository repository = new(fixture.DbContext);

    IReadOnlyList<Guid> stationIds =
      await repository.FindStationIdsAtFestivalAsync(seeded.FestivalId,
                                                     TestContext.CurrentContext.CancellationToken);

    Assert.That(stationIds, Is.EquivalentTo(new[] { seeded.KitchenStationId, seeded.BarStationId }));
  }

  [Test]
  public async Task RemoveMenuRow_AnItemComingOffTheMenu_TakesTheRowOutWhenTheChangesAreSaved()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    FestivalMenuRepository repository = new(fixture.DbContext);

    FestivalCatalogItem menuRow = (await repository.FindMenuRowAsync(seeded.FestivalId,
                                                                     seeded.SausageItemId,
                                                                     TestContext.CurrentContext.CancellationToken))!;

    repository.RemoveMenuRow(menuRow);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();

    Assert.That(await readContext.FestivalCatalogItems.AnyAsync(row => row.Id == menuRow.Id,
                                                                TestContext.CurrentContext.CancellationToken),
                Is.False);
  }

  [Test]
  public async Task RemoveAssignments_TheStationsAnItemHad_TakesThemAllOut()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    FestivalMenuRepository repository = new(fixture.DbContext);

    IReadOnlyList<ItemStationAssignment> assignments =
      await repository.FindAssignmentsAsync(seeded.FestivalId,
                                            seeded.SausageItemId,
                                            TestContext.CurrentContext.CancellationToken);

    repository.RemoveAssignments(assignments);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();

    Assert.That(await readContext.ItemStationAssignments
                                 .AnyAsync(assignment => assignment.CatalogItemId == seeded.SausageItemId,
                                           TestContext.CurrentContext.CancellationToken),
                Is.False);
  }

  [Test]
  public void RemoveAssignments_NullAssignments_ThrowsArgumentNullException()
  {
    using SqliteInMemoryFixture fixture = new();

    FestivalMenuRepository repository = new(fixture.DbContext);

    Assert.That(() => repository.RemoveAssignments(null!), Throws.ArgumentNullException);
  }

  [Test]
  public async Task AddAssignmentAsync_ANewStationForAnItem_StoresItWhenTheChangesAreSaved()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    FestivalMenuRepository repository = new(fixture.DbContext);
    var assignmentId = Guid.NewGuid();

    await repository.AddAssignmentAsync(new()
                                        {
                                          Id = assignmentId,
                                          FestivalId = seeded.FestivalId,
                                          CatalogItemId = seeded.SausageItemId,
                                          StationId = seeded.BarStationId
                                        },
                                        TestContext.CurrentContext.CancellationToken);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();

    Assert.That(await readContext.ItemStationAssignments.AnyAsync(assignment => assignment.Id == assignmentId,
                                                                  TestContext.CurrentContext.CancellationToken),
                Is.True);
  }
}
