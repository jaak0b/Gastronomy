# Ingredients and recipes

The admin keeps a list of ingredients on the Zutaten tab (a name and a unit: pieces, grams or
millilitres) and gives each article a recipe: how much of each ingredient one portion uses. An
ingredient can be switched off, which keeps it out of new recipes.

## Where the code lives

Backend, under `backend/`:

- `GastronomyApp.Core/Entities/Ingredient.cs` and `CatalogItemIngredient.cs` (one recipe line: article,
  ingredient, `Amount`). Both raise `ConfigurationChanged`. The name is unique without regard to case
  (`IngredientConfiguration`, collation `NOCASE`), and so is one line per article and ingredient
  (`CatalogItemIngredientConfiguration`).
- `IngredientAdministrationService` lists, creates, renames, activates and deactivates ingredients.
  `IngredientService.ActiveItemNamesOf` names the active articles that use an ingredient: the list
  view carries the names as `ActiveItemNames` and the deactivation check calls the same method.
- `CatalogItemRecipeService` sets the amount of a line (`SetAmountAsync`, which adds the line when
  there is none) and removes one (`RemoveAsync`).
- `CatalogItemAdministrationService.ActivateAsync` is the check that keeps an article off while its
  recipe holds a deactivated ingredient.
- Refusals: `GastronomyApp.Core/Refusals/Refusal.Ingredients.cs` and `Refusal.CatalogItem.cs`.
- Routes: `GastronomyApp.Api/Endpoints/AdminIngredientEndpoints.cs` (`/api/admin/ingredients`: list,
  create, `PUT /{id}`, `POST /{id}/activate`, `POST /{id}/deactivate`) with
  `GastronomyApp.Api/Handlers/AdminIngredientHandler.cs`, and `PUT` and `DELETE
  /api/admin/items/{itemId}/ingredients/{ingredientId}` in `AdminItemEndpoints.cs` with
  `AdminItemRecipeHandler.cs` beside them.
- Wire records: `GastronomyApp.Contracts/Admin/Ingredients` (`SaveIngredientRequest`,
  `AdminIngredientView`, `AdminIngredientListView`, `SavedIngredientView`) and `Admin/Catalog`
  (`SaveItemIngredientRequest`, `AdminItemIngredientView`, carried in `AdminItemView.Ingredients`).
  Mapping: `GastronomyApp.Api/Mapping/AdminIngredientMapping.cs` and `AdminItemMapping.cs`.

Frontend, under `frontend/src/`:

- `admin/components/ingredients/IngredientsList.vue` (the tab), `IngredientDialog.vue` (the form) and
  `IngredientAmountField.vue`; `admin/core/ingredientAmounts.ts` holds the amount maths (the field
  also offers kilograms and litres and sends grams and millilitres).
- `admin/components/items/ItemIngredientsDialog.vue` is the recipe, opened from an article row in
  `ItemsListCategorySection.vue` through `ItemsList.vue`.
- Stores: `admin/stores/ingredients.ts` (on `defineAdminList`) and `items.ts` (`setIngredientAmount`,
  `removeIngredient`). Texts: `admin.ingredients.*`, `errors.admin.ingredients.*` and
  `errors.admin.items.ingredientIsDeactivated` in `shared/i18n/de.json` and `en.json`.

## Invariants

- A deactivated ingredient never sits in an active article's recipe. Three checks keep it so:
  `DeactivateAsync` refuses while an active article uses the ingredient, `SetAmountAsync` refuses a
  new line for a deactivated ingredient (an existing line may still change its amount), and
  `ActivateAsync` on an article refuses while its recipe holds one, with one refusal per ingredient
  ordered by name, of which the first is answered.
- The first two refusals are guards for what the screens already prevent, so they answer
  `errors.admin.actionFailed`: an ingredient that an active article uses cannot be switched off from
  the Zutaten tab, which names those articles, and a recipe can only add an active ingredient.
- A name is compared without regard to case. Creating or renaming to the name of a deactivated
  ingredient is refused with `errors.admin.ingredients.nameBelongsToDeactivatedIngredient` and the
  parameter `name`; an active namesake gets `nameTaken`.
- An amount is a finite number above zero. The request attribute requires the number and
  `SetAmountAsync` refuses any other with `errors.admin.ingredients.amountInvalid`.
- The recipe dialog only selects. A new ingredient comes from `IngredientDialog.vue` and is chosen for
  adding as soon as it is saved (`afterSaving` of `useEditSession`).
- A recipe is read and changed only in that dialog. The article row on the Artikel tab shows no
  recipe line.
- Removing a recipe line asks first (`BaseConfirmDialog`). A warning under a recipe line, or under
  the add row, clears as soon as that row is edited again (see the row warnings page of `how-to`).
- Both screens reload on `ConfigurationChanged`.

## Tests

- `GastronomyApp.Core.Tests/Services`: `IngredientAdministrationServiceTest`, `IngredientServiceTest`,
  `CatalogItemRecipeServiceTest` and the recipe cases of `CatalogItemAdministrationServiceTest`.
- `GastronomyApp.Infrastructure.Tests/Repositories`: `IngredientRepositoryTest` and
  `CatalogItemIngredientRepositoryTest` (the unique indexes).
- `GastronomyApp.Api.Tests/Endpoints`: `AdminIngredientEndpointsTest` and
  `AdminItemIngredientEndpointsTest`; `Announcers/ConfigurationChangedAnnouncementTest` covers the
  announcement.
- `frontend/tests/admin`: `stores/ingredients.spec.ts` and `stores/items.spec.ts`;
  `components/ingredients/` (`IngredientsList.*.spec.ts` on `ingredientsListFixture.ts`,
  `IngredientDialog.spec.ts`, `IngredientAmountField.spec.ts`); `components/items/`
  (`ItemIngredientsDialog.*.spec.ts` on `itemIngredientsDialogFixture.ts`, `ItemsList.ingredients.spec.ts`,
  and the activation refusal in `ItemsList.items.spec.ts`); `core/ingredientAmounts.spec.ts`. Fixtures
  come from `anAdminIngredient` and `anAdminItem` in `tests/support/wireViews.ts`.
