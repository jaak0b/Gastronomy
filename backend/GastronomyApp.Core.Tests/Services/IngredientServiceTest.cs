using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class IngredientServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _mustard = new()
               {
                 Id = Guid.NewGuid(),
                 Name = "Senf",
                 Unit = IngredientUnit.Gram,
                 IsActive = true
               };
  }

  private readonly IngredientService _service = new();
  private Ingredient _mustard = null!;

  [Test]
  public void ActiveItemNamesOf_RecipesOfActiveAndSwitchedOffArticles_NamesOnlyTheActiveArticlesSortedByName()
  {
    _mustard.CatalogItems.Add(BuildRecipeLine("Currywurst", true));
    _mustard.CatalogItems.Add(BuildRecipeLine("Pommes", false));
    _mustard.CatalogItems.Add(BuildRecipeLine("Bratwurst", true));

    IReadOnlyList<string> activeItemNames = _service.ActiveItemNamesOf(_mustard);

    Assert.That(activeItemNames,
                Is.EqualTo(new[]
                           {
                             "Bratwurst",
                             "Currywurst"
                           }));
  }

  [Test]
  public void ActiveItemNamesOf_OnlyASwitchedOffArticleUsesIt_NamesNothing()
  {
    _mustard.CatalogItems.Add(BuildRecipeLine("Pommes", false));

    IReadOnlyList<string> activeItemNames = _service.ActiveItemNamesOf(_mustard);

    Assert.That(activeItemNames, Is.Empty);
  }

  private CatalogItemIngredient BuildRecipeLine(string itemName, bool isTheItemActive)
  {
    CatalogItem item = new()
                       {
                         Id = Guid.NewGuid(),
                         Name = itemName,
                         CategoryId = Guid.NewGuid(),
                         SortOrder = 1,
                         IsActive = isTheItemActive
                       };

    return new()
           {
             Id = Guid.NewGuid(),
             CatalogItemId = item.Id,
             IngredientId = _mustard.Id,
             Amount = 20,
             CatalogItem = item,
             Ingredient = _mustard
           };
  }
}
