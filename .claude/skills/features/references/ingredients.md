# Ingredients and recipes

The admin keeps a list of ingredients on the Zutaten tab (a name and a unit the admin manages on the
Einheiten tab) and gives each article a recipe: how much of each ingredient one portion uses. An
ingredient can be switched off, which keeps it out of new recipes. An amount is a plain number in the
ingredient's unit; nothing converts it.

## Where the code lives

Backend, under `backend/`:

- `GastronomyApp.Core/Entities/Ingredient.cs` and `CatalogItemIngredient.cs` (one recipe line: article,
  ingredient, `Amount`). Both raise `ConfigurationChanged`. The name is unique without regard to case
  (`IngredientConfiguration`, collation `NOCASE`), and so is one line per article and ingredient
  (`CatalogItemIngredientConfiguration`). The ingredient carries `UnitId` and its `Unit` navigation;
  the unit entity and its list are on the `units` page.
- `IngredientAdministrationService` lists, creates, renames, activates and deactivates ingredients.
  Create and update take a `Guid?` unit id; a new unit must exist and be active, while changing the
  unit of an existing ingredient is allowed at any time and leaves the stored amounts alone.
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
  (`SaveItemIngredientRequest`, `AdminItemIngredientView`, carried in `AdminItemView.Ingredients`
  ordered by ingredient name). A recipe line carries the ingredient's current name and its unit id
  and unit name beside its id and amount. Mapping:
  `GastronomyApp.Api/Mapping/AdminIngredientMapping.cs` and `AdminItemMapping.cs`, which maps a
  `CatalogItemIngredient` to its line. The queries behind those views include the unit navigation.

Frontend, under `frontend/src/`:

- `admin/components/ingredients/IngredientsList.vue` (the tab), `IngredientDialog.vue` (the form) and
  `IngredientAmountField.vue` (a text field with the static unit name beside it).
  `admin/core/ingredientAmounts.ts` parses and formats the number without a unit or a conversion, and
  `admin/composables/useIngredientAmountText.ts` writes the stored number with the unit name.
- The unit picker in `IngredientDialog.vue` comes from `admin/stores/units.ts`: it offers the active
  units by `unitId` and keeps the ingredient's current unit with its carried `unitName` even when the
  store does not contain it. `IngredientsList.vue` and `ItemsList.vue` load and listen to that store,
  because the recipe dialog can create an ingredient there.
- `admin/components/items/ItemIngredientsDialog.vue` is the recipe, opened from an article row in
  `ItemsListCategorySection.vue` through `ItemsList.vue`. Each line and the add row show the
  `unitName` the wire view carries.
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
- A unit must exist and be active when an ingredient is created, and when an existing ingredient
  changes to another unit. The picker offers only active units, so an unknown or switched-off unit
  answers `errors.admin.actionFailed`; an edit keeps the ingredient's current unit available from its
  carried `unitName`. A unit change never converts an amount: the recipe line and the stock row keep
  the number the admin entered, because the amount field has no unit of its own and no conversion
  helper exists.
- An amount is a finite number above zero. The request attribute requires the number and
  `SetAmountAsync` refuses any other with `errors.admin.ingredients.amountInvalid`.
- The recipe dialog shows every recipe line from the item view itself, with the name and unit the
  line carries, even when the ingredient list has not loaded or failed to load. It never looks a
  line up in the ingredients store and never drops one; the store feeds only the picker of the add
  row and the creation of a new ingredient. A renamed ingredient raises `ConfigurationChanged`, the
  items store reloads, and the open dialog follows because `ItemsList.vue` hands it the item from
  that store.
- The recipe dialog only selects. A new ingredient comes from `IngredientDialog.vue` and is chosen for
  adding as soon as it is saved (`afterSaving` of `useEditSession`).
- A recipe is read and changed only in that dialog. The article row on the Artikel tab shows no
  recipe line.
- Removing a recipe line asks first (`BaseConfirmDialog`). A warning under a recipe line, or under
  the add row, clears as soon as that row is edited again (see the row warnings page of `how-to`).
- Both screens reload on `ConfigurationChanged`.

## Tests

- `GastronomyApp.Core.Tests/Services`: `IngredientAdministrationServiceTest`, `IngredientServiceTest`,
  `CatalogItemRecipeServiceTest` and the recipe cases of `CatalogItemAdministrationServiceTest`. The
  unit cases live in `UnitAdministrationServiceTest` and `UnitServiceTest`.
- `GastronomyApp.Infrastructure.Tests/Repositories`: `IngredientRepositoryTest` and
  `CatalogItemIngredientRepositoryTest` (the unique indexes), and `UnitRepositoryTest`.
- `GastronomyApp.Api.Tests/Mapping/AdminItemMappingTest`: the name, unit id and unit name of every
  recipe line.
- `GastronomyApp.Api.Tests/Endpoints`: `AdminIngredientEndpointsTest` and
  `AdminItemIngredientEndpointsTest` (including the name, unit id and unit name of every listed
  recipe line, the new name after a rename, and a unit change that keeps the amount); the
  announcement is in `Announcers/ConfigurationChangedAnnouncementTest`.
- `frontend/tests/admin`: `stores/ingredients.spec.ts` and `stores/items.spec.ts`;
  `components/ingredients/` (`IngredientsList.*.spec.ts` on `ingredientsListFixture.ts`, which also
  answers the units route, `IngredientDialog.spec.ts` for the active unit picker and the carried
  current unit, `IngredientAmountField.spec.ts`); `components/items/`
  (`ItemIngredientsDialog.*.spec.ts` on `itemIngredientsDialogFixture.ts`, whose recipe lines spec
  also opens the dialog before the ingredient list has loaded and after it failed;
  `ItemsList.ingredients.spec.ts`, `ItemsList.renamedIngredient.spec.ts` for a rename behind the
  open recipe, and the activation refusal in `ItemsList.items.spec.ts`);
  `core/ingredientAmounts.spec.ts` and `composables/useIngredientAmountText.spec.ts`. Fixtures come
  from `anAdminIngredient`, `anAdminItem`, `anAdminItemIngredient` and `anAdminUnit` in
  `tests/support/wireViews.ts`.
