# Ingredients and recipes

The admin keeps a list of ingredients on the Zutaten tab (a name and a unit, chosen from the unit
chips of the ingredient's form, where the admin also adds, renames and deletes units) and gives each
article a recipe: how much of each ingredient one portion uses. An
ingredient can be switched off, which keeps it out of new recipes. An amount is a plain number in the
ingredient's unit; nothing converts it.

## Where the code lives

Backend, under `backend/`:

- `GastronomyApp.Core/Entities/Ingredient.cs` and `CatalogItemIngredient.cs` (one recipe line: article,
  ingredient, `Amount`). Both raise `ConfigurationChanged`. The name is unique without regard to case
  (`IngredientConfiguration`, collation `NOCASE`), and so is one line per article and ingredient
  (`CatalogItemIngredientConfiguration`). The ingredient carries `UnitId` and its `Unit` navigation;
  the unit entity and its list are on the `units` page.
- `GastronomyApp.Core/Services/IngredientService.cs` holds every ingredient action and reads and
  writes through `IGastronomyDatabase`: `ListAsync`, `CreateAsync`, `UpdateAsync` and
  `DeactivateAsync`. Create and update take the `SaveIngredientRequest`; the unit only has to
  exist, and changing the unit of an existing ingredient is allowed at any time and leaves the
  stored amounts alone. Switching an ingredient back on is `ActivationService.ActivateAsync` (the
  generic action over `IActivatable`). The list projects with `ProjectToType<AdminIngredientView>()`;
  the active article names (`ActiveItemNames`) are a query expression in
  `GastronomyApp.Core/Mapping/IngredientMapping.cs`, and the deactivation check asks the same question
  of the recipe lines with `Active()` from `ActivatableQueries`.
- `GastronomyApp.Core/Services/CatalogItemService.cs` is the service of the Artikel tab and holds the
  recipe: `SetRecipeLineAsync` sets the amount of a line (and adds the line when there is none) and
  `RemoveRecipeLineAsync` removes one. Its `ActivateAsync` is the check that keeps an article off
  while its category is switched off or its recipe holds a deactivated ingredient (so it is not the
  generic `ActivationService` action). `ListAsync(festivalId)` reads the articles with their recipe
  lines, then the festival's menu rows as `ItemMenuEntry` (a second query, because a menu row with its
  station list cannot be a subquery of the article on SQLite) and fills `AtTheFestival`.
- Refusals: `GastronomyApp.Core/Refusals/Refusal.Ingredients.cs` and `Refusal.CatalogItem.cs`.
- Routes: `GastronomyApp.Api/Endpoints/AdminIngredientEndpoints.cs` (`/api/admin/ingredients`: list,
  create, `PUT /{id}`, `POST /{id}/activate`, `POST /{id}/deactivate`), which call the services
  directly, and `PUT` and `DELETE
  /api/admin/items/{itemId}/ingredients/{ingredientId}` in `AdminItemEndpoints.cs`, which call
  `CatalogItemService` directly.
- Wire records: `GastronomyApp.Contracts/Admin/Ingredients` (`SaveIngredientRequest`,
  `AdminIngredientView`, `AdminIngredientListView`, `SavedIngredientView`) and `Admin/Catalog`
  (`SaveItemIngredientRequest`, `AdminItemIngredientView`, carried in `AdminItemView.Ingredients`
  ordered by ingredient name). A recipe line carries the ingredient's current name and its unit id
  and unit name beside its id and amount. Mapping:
  `GastronomyApp.Core/Mapping/IngredientMapping.cs` (the ingredient views and the festival stock
  view) and `GastronomyApp.Core/Mapping/CatalogItemMapping.cs`, which maps a
  `CatalogItemIngredient` to its line and orders the lines by ingredient name in SQL. The queries behind those views include the unit navigation.

Frontend, under `frontend/src/`:

- `admin/components/ingredients/IngredientsList.vue` (the tab), `IngredientDialog.vue` (the form) and
  `IngredientAmountField.vue` (a text field with the static unit name beside it).
  `admin/core/ingredientAmounts.ts` parses and formats the number without a unit or a conversion, and
  `admin/composables/useIngredientAmountText.ts` writes the stored number with the unit name.
- The unit chips in `IngredientDialog.vue` come from `admin/stores/units.ts`: they offer every unit
  by `unitId` and keep the ingredient's current unit with its carried `unitName` even when the store
  does not contain it. How the chips add, rename and delete a unit is on the `units` page.
  `IngredientsList.vue` and `ItemsList.vue` load and listen to that store, because the recipe dialog
  can create an ingredient there.
- `admin/components/items/ItemIngredientsDialog.vue` is the recipe, opened from an article row in
  `ItemsListCategorySection.vue` through `ItemsList.vue`. Each line and the add row show the
  `unitName` the wire view carries.
- Stores: `admin/stores/ingredients.ts` (on `defineAdminList` with `withCreating` and `withActivation`) and `items.ts` (`setIngredientAmount`,
  `removeIngredient`). Texts: `admin.ingredients.*`, `errors.admin.ingredients.*` and
  `errors.admin.items.ingredientIsDeactivated` in `shared/i18n/de.json` and `en.json`.

## Invariants

- A deactivated ingredient never sits in an active article's recipe. Three checks keep it so:
  `DeactivateAsync` refuses while an active article uses the ingredient, `SetAmountAsync` refuses a
  new line for a deactivated ingredient (an existing line may still change its amount), and
  `ActivateAsync` on an article refuses while its recipe holds one, with one refusal per ingredient
  ordered by name, of which the first is answered.
- The first two refusals are guards for what the screens already prevent, so they answer
  `common.errors.actionFailed`: an ingredient that an active article uses cannot be switched off from
  the Zutaten tab, which names those articles, and a recipe can only add an active ingredient.
- A name is compared without regard to case, in SQL through `NamedQueries.Namesakes`. Creating or renaming to the name of a deactivated
  ingredient is refused with `errors.admin.ingredients.nameBelongsToDeactivatedIngredient` and the
  parameter `name`; an active namesake gets `nameTaken`.
- A unit only has to exist when an ingredient is created, and when an existing ingredient changes
  to another unit. The screen offers only existing units, so a missing or unknown unit answers
  `common.errors.actionFailed`; an edit keeps the ingredient's current unit available from its
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

- `GastronomyApp.Infrastructure.Tests/Services`: `IngredientServiceTest` and `ActivationServiceTest`,
  on the real DbContext with in-memory SQLite; `Queries/ActivatableQueriesTest`.
- `GastronomyApp.Infrastructure.Tests/Services/CatalogItemServiceTest`: the recipe cases and the
  activation cases of an article, on the real DbContext. The unit cases live in
  `GastronomyApp.Infrastructure.Tests/Services/UnitServiceTest`.
- `GastronomyApp.Infrastructure.Tests`: `Configurations/IngredientNameIndexTest` and
  `CatalogItemIngredientIndexTest` (the unique indexes), `UnitNameIndexTest`.
- `GastronomyApp.Api.Tests/Endpoints`: `AdminIngredientEndpointsTest` and
  `AdminItemIngredientEndpointsTest` (including the name, unit id and unit name of every listed
  recipe line, the new name after a rename, and a unit change that keeps the amount); the
  announcement is in `Announcers/ConfigurationChangedAnnouncementTest`.
- `frontend/tests/admin`: `stores/ingredients.spec.ts` and `stores/items.spec.ts`;
  `components/ingredients/` (`IngredientsList.*.spec.ts` on `ingredientsListFixture.ts`, which also
  answers the units route, `IngredientDialog.spec.ts` and its unit specs on
  `ingredientDialogFixture.ts` (see the `units` page), `IngredientAmountField.spec.ts`);
  `components/items/`
  (`ItemIngredientsDialog.*.spec.ts` on `itemIngredientsDialogFixture.ts`, whose recipe lines spec
  also opens the dialog before the ingredient list has loaded and after it failed;
  `ItemsList.ingredients.spec.ts`, `ItemsList.renamedIngredient.spec.ts` for a rename behind the
  open recipe, and the activation refusal in `ItemsList.items.spec.ts`);
  `core/ingredientAmounts.spec.ts` and `composables/useIngredientAmountText.spec.ts`. Fixtures come
  from `anAdminIngredient`, `anAdminItem`, `anAdminItemIngredient` and `anAdminUnit` in
  `tests/support/wireViews.ts`.
