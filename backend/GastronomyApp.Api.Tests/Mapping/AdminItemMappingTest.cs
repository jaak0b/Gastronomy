using GastronomyApp.Api.Mapping;
using GastronomyApp.Contracts.Admin.Catalog;
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
    var gram = UnitNamed("Gramm");
    var piece = UnitNamed("Stück");
    var mustard = IngredientNamed("Senf", gram);
    var bun = IngredientNamed("Brötchen", piece);
    bratwurst.Ingredients.Add(RecipeLineOf(bratwurst, mustard, 20));
    bratwurst.Ingredients.Add(RecipeLineOf(bratwurst, bun, 1));

    var view = _mapper.Map<AdminItemView>(bratwurst);

    Assert.That(view.Ingredients,
                Is.EqualTo(new[]
                           {
                             new AdminItemIngredientView(bun.Id, "Brötchen", piece.Id, "Stück", 1),
                             new AdminItemIngredientView(mustard.Id, "Senf", gram.Id, "Gramm", 20)
                           }));
  }

  [Test]
  public void Map_IngredientNamesInMixedCase_ComeBackWithCapitalsIgnoredAndUmlautsAfterZ()
  {
    var bratwurst = Article();
    var gram = UnitNamed("Gramm");

    foreach (var name in new[]
                         {
                           "Zwiebel",
                           "Äpfel",
                           "apfel",
                           "Birne"
                         })
      bratwurst.Ingredients.Add(RecipeLineOf(bratwurst, IngredientNamed(name, gram), 1));

    var view = _mapper.Map<AdminItemView>(bratwurst);

    Assert.That(view.Ingredients.Select(ingredient => ingredient.Name),
                Is.EqualTo(new[]
                           {
                             "apfel",
                             "Birne",
                             "Zwiebel",
                             "Äpfel"
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

  private Ingredient IngredientNamed(string name, Unit unit)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = name,
             UnitId = unit.Id,
             Unit = unit,
             IsActive = true
           };
  }

  private Unit UnitNamed(string name)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = name,
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
