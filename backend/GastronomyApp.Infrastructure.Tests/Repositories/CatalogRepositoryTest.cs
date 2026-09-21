using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class CatalogRepositoryTest
{
  [Test]
  public async Task FindWithMenuAsync_TheSeededFestival_CarriesItsStationsCategoriesAndPricedItems()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    CatalogRepository repository = new(fixture.DbContext);

    var festival = (await repository.FindWithMenuAsync(seeded.FestivalId, OrderableItemsOf(seeded), TestContext.CurrentContext.CancellationToken))!;

    var sausage = festival.CatalogItems.Single(menuRow => menuRow.CatalogItemId == seeded.SausageItemId);

    Assert.Multiple(() =>
                    {
                      Assert.That(festival.Name, Is.EqualTo("Sommerfest"));
                      Assert.That(festival.Stations.OrderBy(link => link.Station.SortOrder).Select(link => link.StationId),
                                  Is.EqualTo(new[]
                                             {
                                               seeded.KitchenStationId,
                                               seeded.BarStationId
                                             }));
                      Assert.That(festival.CatalogItems.Select(menuRow => menuRow.CatalogItem.Category.Id).Distinct().Order(),
                                  Is.EqualTo(new[]
                                             {
                                               seeded.FoodCategoryId,
                                               seeded.DrinkCategoryId
                                             }.Order()));
                      Assert.That(sausage.PriceCents, Is.EqualTo(350));
                      Assert.That(sausage.CatalogItem.StationAssignments.Select(assignment => assignment.StationId), Is.EqualTo(new[] { seeded.KitchenStationId }));
                    });
  }

  [Test]
  public async Task FindWithMenuAsync_AnItemWhoseCategoryIsSwitchedOff_LeavesTheItemOut()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    var drinks = await fixture.DbContext.CatalogCategories.FirstAsync(category => category.Id == seeded.DrinkCategoryId, TestContext.CurrentContext.CancellationToken);
    drinks.IsActive = false;
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    CatalogRepository repository = new(fixture.DbContext);

    var festival = (await repository.FindWithMenuAsync(seeded.FestivalId, OrderableItemsOf(seeded), TestContext.CurrentContext.CancellationToken))!;

    Assert.That(festival.CatalogItems.Select(menuRow => menuRow.CatalogItemId), Is.EqualTo(new[] { seeded.SausageItemId }));
  }

  [Test]
  public async Task FindWithMenuAsync_AnItemAssignedToAStationThatIsSwitchedOff_LeavesThatStationOut()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    var kitchen = await fixture.DbContext.Stations.FirstAsync(station => station.Id == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    kitchen.IsActive = false;
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    CatalogRepository repository = new(fixture.DbContext);

    var festival = (await repository.FindWithMenuAsync(seeded.FestivalId, OrderableItemsOf(seeded), TestContext.CurrentContext.CancellationToken))!;

    Assert.That(festival.CatalogItems.Single(menuRow => menuRow.CatalogItemId == seeded.SausageItemId).CatalogItem.StationAssignments, Is.Empty);
  }

  [Test]
  public async Task FindWithMenuAsync_AnItemTheWaiterCannotOrder_LeavesItOut()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    CatalogRepository repository = new(fixture.DbContext);

    var festival = (await repository.FindWithMenuAsync(seeded.FestivalId, [seeded.SausageItemId], TestContext.CurrentContext.CancellationToken))!;

    Assert.That(festival.CatalogItems.Select(menuRow => menuRow.CatalogItemId), Is.EqualTo(new[] { seeded.SausageItemId }));
  }

  private IReadOnlyCollection<Guid> OrderableItemsOf(SeededDomain seeded)
  {
    return
    [
      seeded.SausageItemId,
      seeded.LemonadeItemId
    ];
  }
}
