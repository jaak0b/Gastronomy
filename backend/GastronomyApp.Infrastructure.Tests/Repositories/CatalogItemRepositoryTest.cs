using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;

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

    IReadOnlyList<CatalogItem> found = await repository.FindAllOrderedAsync(null, TestContext.CurrentContext.CancellationToken);

    Assert.That(found.Select(item => item.Id),
                Is.EqualTo(new[]
                           {
                             seeded.SausageItemId,
                             seeded.LemonadeItemId
                           }));
  }

  [Test]
  public async Task FindWithRecipeAsync_AnArticleWithTwoIngredients_CarriesEveryRecipeLineWithItsIngredient()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    AddIngredientToTheRecipe(fixture, seeded.SausageItemId, "Senf", true);
    AddIngredientToTheRecipe(fixture, seeded.SausageItemId, "Zwiebeln", false);
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();
    CatalogItemRepository repository = new(fixture.DbContext);

    var found = await repository.FindWithRecipeAsync(seeded.SausageItemId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(found!.Ingredients.Select(recipeLine => recipeLine.Ingredient.Name), Is.EquivalentTo(new[] { "Senf", "Zwiebeln" }));
                      Assert.That(found.Ingredients.Single(recipeLine => recipeLine.Ingredient.Name == "Zwiebeln").Ingredient.IsActive, Is.False);
                    });
  }

  [Test]
  public async Task FindWithRecipeAsync_AnUnknownArticle_FindsNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    CatalogItemRepository repository = new(fixture.DbContext);

    var found = await repository.FindWithRecipeAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.That(found, Is.Null);
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
  public async Task FindAllOrderedAsync_AFestival_CarriesThePriceAndTheStationsOfThatFestival()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    CatalogItemRepository repository = new(fixture.DbContext);

    IReadOnlyList<CatalogItem> found = await repository.FindAllOrderedAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    var sausage = found.Single(item => item.Id == seeded.SausageItemId);

    Assert.Multiple(() =>
                    {
                      Assert.That(sausage.FestivalCatalogItems.Single().PriceCents, Is.EqualTo(350));
                      Assert.That(sausage.StationAssignments.Select(assignment => assignment.StationId), Is.EqualTo(new[] { seeded.KitchenStationId }));
                    });
  }

  [Test]
  public async Task FindAllOrderedAsync_NoFestival_CarriesNeitherAPriceNorAStation()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    CatalogItemRepository repository = new(fixture.DbContext);

    IReadOnlyList<CatalogItem> found = await repository.FindAllOrderedAsync(null, TestContext.CurrentContext.CancellationToken);

    var sausage = found.Single(item => item.Id == seeded.SausageItemId);

    Assert.Multiple(() =>
                    {
                      Assert.That(sausage.FestivalCatalogItems, Is.Empty);
                      Assert.That(sausage.StationAssignments, Is.Empty);
                    });
  }

  private void AddIngredientToTheRecipe(SqliteInMemoryFixture fixture, Guid catalogItemId, string name, bool isActive)
  {
    Ingredient ingredient = new()
    {
      Id = Guid.NewGuid(),
      Name = name,
      Unit = IngredientUnit.Gram,
      IsActive = isActive
    };
    fixture.DbContext.Ingredients.Add(ingredient);
    fixture.DbContext.CatalogItemIngredients.Add(new()
                                                 {
                                                   Id = Guid.NewGuid(),
                                                   CatalogItemId = catalogItemId,
                                                   IngredientId = ingredient.Id,
                                                   Amount = 1
                                                 });
  }
}
