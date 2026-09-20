using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class CatalogRepositoryTest
{
  [Test]
  public async Task ReadAtFestivalAsync_TheSeededFestival_CarriesItsStationsCategoriesAndPricedItems()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    CatalogRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    var catalog = await repository.ReadAtFestivalAsync(seeded.FestivalId, "Sommerfest", TestContext.CurrentContext.CancellationToken);

    var sausage = catalog.Items.Single(item => item.ItemId == seeded.SausageItemId);

    Assert.Multiple(() =>
                    {
                      Assert.That(catalog.FestivalName, Is.EqualTo("Sommerfest"));
                      Assert.That(catalog.Stations.Select(station => station.StationId),
                                  Is.EqualTo(new[]
                                             {
                                               seeded.KitchenStationId,
                                               seeded.BarStationId
                                             }));
                      Assert.That(catalog.Categories.Select(category => category.CategoryId),
                                  Is.EqualTo(new[]
                                             {
                                               seeded.FoodCategoryId,
                                               seeded.DrinkCategoryId
                                             }));
                      Assert.That(sausage.PriceCents, Is.EqualTo(350));
                      Assert.That(sausage.StationIds, Is.EqualTo(new[] { seeded.KitchenStationId }));
                    });
  }

  [Test]
  public async Task ReadAtFestivalAsync_AnItemWhoseCategoryIsSwitchedOff_LeavesTheItemOut()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    var drinks = await fixture.DbContext.CatalogCategories.FirstAsync(category => category.Id == seeded.DrinkCategoryId, TestContext.CurrentContext.CancellationToken);
    drinks.IsActive = false;
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    CatalogRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    var catalog = await repository.ReadAtFestivalAsync(seeded.FestivalId, "Sommerfest", TestContext.CurrentContext.CancellationToken);

    Assert.That(catalog.Items.Select(item => item.ItemId), Is.EqualTo(new[] { seeded.SausageItemId }));
  }

  [Test]
  public async Task ReadAtFestivalAsync_AnItemAssignedToAStationThatIsSwitchedOff_LeavesThatStationOut()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    var kitchen = await fixture.DbContext.Stations.FirstAsync(station => station.Id == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    kitchen.IsActive = false;
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    CatalogRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    var catalog = await repository.ReadAtFestivalAsync(seeded.FestivalId, "Sommerfest", TestContext.CurrentContext.CancellationToken);

    Assert.That(catalog.Items.Single(item => item.ItemId == seeded.SausageItemId).StationIds, Is.Empty);
  }
}
