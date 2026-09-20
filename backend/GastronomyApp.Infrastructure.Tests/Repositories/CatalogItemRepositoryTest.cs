using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

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

    IReadOnlyCollection<ItemStationAssignment> found = await repository.FindAssignmentsAsync(seeded.FestivalId, seeded.SausageItemId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(found, Has.Count.EqualTo(1));
                      Assert.That(found.Single().StationId, Is.EqualTo(seeded.KitchenStationId));
                    });
  }

  [Test]
  public async Task FindAllOrderedAsync_TheSeededItems_ReturnsThemBySortOrder()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    CatalogItemRepository repository = new(fixture.DbContext);

    IReadOnlyList<CatalogItem> found = await repository.FindAllOrderedAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(found.Select(item => item.Id),
                Is.EqualTo(new[]
                           {
                             seeded.SausageItemId,
                             seeded.LemonadeItemId
                           }));
  }

  [Test]
  public async Task IsNameTakenAsync_TheNameOfAnotherItem_AnswersTrue()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    CatalogItemRepository repository = new(fixture.DbContext);

    Assert.Multiple(async () =>
                    {
                      Assert.That(await repository.IsNameTakenAsync("Bratwurst", null, TestContext.CurrentContext.CancellationToken), Is.True);
                      Assert.That(await repository.IsNameTakenAsync("Bratwurst", seeded.SausageItemId, TestContext.CurrentContext.CancellationToken), Is.False);
                    });
  }

  [Test]
  public async Task FindMenuRowsAtFestivalAsync_TheSeededFestival_ReturnsThePricedRowsOfThatFestival()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    CatalogItemRepository repository = new(fixture.DbContext);

    IReadOnlyList<FestivalCatalogItem> found = await repository.FindMenuRowsAtFestivalAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(found, Has.Count.EqualTo(2));
                      Assert.That(found.Single(menuRow => menuRow.CatalogItemId == seeded.SausageItemId).PriceCents, Is.EqualTo(350));
                    });
  }

  [Test]
  public async Task FindAssignmentsAtFestivalAsync_TheSeededFestival_ReturnsOneAssignmentPerItem()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    CatalogItemRepository repository = new(fixture.DbContext);

    IReadOnlyList<ItemStationAssignment> found = await repository.FindAssignmentsAtFestivalAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(found.Select(assignment => assignment.StationId),
                Is.EquivalentTo(new[]
                                {
                                  seeded.KitchenStationId,
                                  seeded.BarStationId
                                }));
  }

  [Test]
  public async Task AddAsync_ANewItem_StoresItWhenTheChangesAreSaved()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    CatalogItemRepository repository = new(fixture.DbContext);
    var itemId = Guid.NewGuid();

    await repository.AddAsync(new()
                              {
                                Id = itemId,
                                Name = "Currywurst",
                                CategoryId = seeded.FoodCategoryId,
                                SortOrder = 3,
                                IsActive = true
                              },
                              TestContext.CurrentContext.CancellationToken);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();

    Assert.That(await readContext.CatalogItems.AnyAsync(item => item.Id == itemId, TestContext.CurrentContext.CancellationToken), Is.True);
  }
}
