using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class FestivalAdministrationServiceTest
{
  private readonly DateTime _start = new(2030, 6, 1, 14, 0, 0, DateTimeKind.Utc);

  [Test]
  public async Task CreateAsync_TwoStoredIngredients_StoresAnUnlimitedStockRowForEach()
  {
    using SqliteInMemoryFixture fixture = new();
    var ingredientIds = await AddIngredientsAsync(fixture.DbContext);

    ErrorOr<Festival> created = await Compose(fixture.DbContext).CreateAsync("Sommerfest", _start, _start.AddHours(10), TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    List<FestivalIngredient> stored = await fixture.DbContext.FestivalIngredients.Where(stock => stock.FestivalId == created.Value.Id).ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(stored.Select(stock => stock.IngredientId), Is.EquivalentTo(ingredientIds));
                      Assert.That(stored.All(stock => stock.AvailableAmount == null), Is.True);
                    });
  }

  [Test]
  public async Task CopyAsync_TheSourceHasAnAvailableAmount_StoresUnlimitedRowsForTheCopy()
  {
    using SqliteInMemoryFixture fixture = new();
    var ingredientIds = await AddIngredientsAsync(fixture.DbContext);
    var service = Compose(fixture.DbContext);
    ErrorOr<Festival> source = await service.CreateAsync("Sommerfest", _start, _start.AddHours(10), TestContext.CurrentContext.CancellationToken);
    var sourceRow = await fixture.DbContext.FestivalIngredients.FirstAsync(stock => stock.FestivalId == source.Value.Id, TestContext.CurrentContext.CancellationToken);
    sourceRow.AvailableAmount = 250;
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<Festival> copy = await service.CopyAsync(source.Value.Id, "Herbstfest", _start.AddDays(90), _start.AddDays(90).AddHours(10), TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    List<FestivalIngredient> stored = await fixture.DbContext.FestivalIngredients.Where(stock => stock.FestivalId == copy.Value.Id).ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(stored.Select(stock => stock.IngredientId), Is.EquivalentTo(ingredientIds));
                      Assert.That(stored.All(stock => stock.AvailableAmount == null), Is.True);
                    });
  }

  private FestivalAdministrationService Compose(GastronomyAppDbContext dbContext)
  {
    FestivalRepository festivalRepository = new(dbContext, new());

    return new(festivalRepository, new IngredientRepository(dbContext), new(), new(festivalRepository, new(), TimeProvider.System));
  }

  private async Task<IReadOnlyList<Guid>> AddIngredientsAsync(GastronomyAppDbContext dbContext)
  {
    List<Ingredient> ingredients =
    [
      new()
      {
        Id = Guid.NewGuid(),
        Name = "Senf",
        Unit = IngredientUnit.Gram,
        IsActive = true
      },
      new()
      {
        Id = Guid.NewGuid(),
        Name = "Brötchen",
        Unit = IngredientUnit.Piece,
        IsActive = false
      }
    ];
    dbContext.Ingredients.AddRange(ingredients);
    await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return ingredients.Select(ingredient => ingredient.Id).ToList();
  }
}
