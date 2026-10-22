using GastronomyApp.Api.Mapping;
using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using MapsterMapper;

namespace GastronomyApp.Api.Tests.Mapping;

[TestFixture]
public sealed class AdminItemMappingTest
{
  [SetUp]
  public void SetUp()
  {
    IMappingRegistration[] registrations =
    [
      new AdminCategoryMapping(),
      new AdminFestivalMapping(new()),
      new AdminItemMapping(),
      new AdminStaffMembersMapping(),
      new AdminStationMapping(new()),
      new CatalogMapping(),
      new EnrolmentMapping(),
      new EstimateMapping(new()),
      new OpenItemMapping(),
      new OrderMapping(new()),
      new SettlementMapping(),
      new StationQueueMapping(new())
    ];

    _mapper = new Mapper(new MappingConfiguration(registrations).Build());
  }

  private IMapper _mapper = null!;

  [Test]
  public void Map_AnArticleWithTwoRecipeLines_CarriesTheNameAndUnitOfEveryIngredientInNameOrder()
  {
    var bratwurst = Article();
    var mustard = IngredientNamed("Senf", IngredientUnit.Gram);
    var bun = IngredientNamed("Brötchen", IngredientUnit.Piece);
    bratwurst.Ingredients.Add(RecipeLineOf(bratwurst, mustard, 20));
    bratwurst.Ingredients.Add(RecipeLineOf(bratwurst, bun, 1));

    var view = _mapper.Map<AdminItemView>(bratwurst);

    Assert.That(view.Ingredients,
                Is.EqualTo(new[]
                           {
                             new AdminItemIngredientView(bun.Id, "Brötchen", IngredientUnit.Piece, 1),
                             new AdminItemIngredientView(mustard.Id, "Senf", IngredientUnit.Gram, 20)
                           }));
  }

  private CatalogItem Article()
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = "Bratwurst",
             CategoryId = Guid.NewGuid(),
             SortOrder = 1,
             IsActive = true
           };
  }

  private Ingredient IngredientNamed(string name, IngredientUnit unit)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = name,
             Unit = unit,
             IsActive = true
           };
  }

  private CatalogItemIngredient RecipeLineOf(CatalogItem article, Ingredient ingredient, double amount)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             CatalogItemId = article.Id,
             IngredientId = ingredient.Id,
             Amount = amount,
             CatalogItem = article,
             Ingredient = ingredient
           };
  }
}
