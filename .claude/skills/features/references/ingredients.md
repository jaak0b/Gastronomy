# Ingredients and recipes

The admin gives each article a recipe: how much of each ingredient one portion uses. Ingredients
live only in the recipe popup that "Zutaten bearbeiten" opens on an article row of the Artikel tab:
its left column is the recipe, its right column lists only the ingredients the recipe does not hold
yet, under "Verfügbare Zutaten" ("Available ingredients"), with a search, the form to create or edit
one, and the plus that adds one with amount 1 and focuses the new line's amount. An ingredient that
no recipe uses can be deleted for good; one that any recipe uses cannot. An amount is a plain number
in the ingredient's unit; nothing converts it. A recipe line always carries an amount above zero: a
new line starts at 1, and a cleared Menge becomes 1 again.

## Where the code lives

Backend, under `backend/`:

- `GastronomyApp.Core/Entities/Ingredient.cs` (name, unit, stock rows, recipe lines; no active flag)
  and `CatalogItemIngredient.cs` (one recipe line: article,
  ingredient, `Amount`). Both raise `ConfigurationChanged`. The name is unique without regard to case
  (`IngredientConfiguration`, collation `NOCASE`), and so is one line per article and ingredient
  (`CatalogItemIngredientConfiguration`). The ingredient carries `UnitId` and its `Unit` navigation;
  the unit entity and its list are on the `units` page.
- `GastronomyApp.Core/Services/IngredientService.cs` holds every ingredient action and reads and
  writes through `IGastronomyDatabase`: `ListAsync`, `CreateAsync`, `UpdateAsync` and
  `DeleteAsync`. Create and update take the `SaveIngredientRequest`; the unit only has to
  exist, and changing the unit of an existing ingredient is allowed at any time and leaves the
  stored amounts alone. Whether an ingredient is in use is asked in one place,
  `IngredientQueries.UsedByItemNames` in `Core/Queries` (a `[Projectable]` check): the list projects
  with `ProjectToType<AdminIngredientView>()` and names those articles through it in SQL
  (`GastronomyApp.Core/Mapping/IngredientMapping.cs`), and `DeleteAsync` asks the same question
  before it removes the ingredient together with its festival stock rows in one save.
- `GastronomyApp.Core/Services/CatalogItemService.cs` is the service of the Artikel tab and holds the
  recipe: `SetRecipeLineAsync` sets the amount of a line (and adds the line when there is none) and
  `RemoveRecipeLineAsync` removes one. Its `ActivateAsync` is the check that keeps an article off
  while its category is switched off (so it is not the
  generic `ActivationService` action). `ListAsync(festivalId)` reads the articles with their recipe
  lines, then the festival's menu rows as `ItemMenuEntry` (a second query, because a menu row with its
  station list cannot be a subquery of the article on SQLite) and fills `AtTheFestival`.
- Refusals: `GastronomyApp.Core/Refusals/Refusal.Ingredients.cs` and `Refusal.CatalogItem.cs`.
- Routes: `GastronomyApp.Api/Endpoints/AdminIngredientEndpoints.cs` (`/api/admin/ingredients`: list,
  create, `PUT /{ingredientId}`, `DELETE /{ingredientId}`), which call `IngredientService`
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
- Migration `20261030103243_RemoveIngredientActiveFlag` drops the ingredients' active flag and
  deletes no ingredient: one that was switched off, used or not, stays as an ordinary ingredient with
  its recipe lines and stock rows.

Frontend, under `frontend/src/`:

- `admin/components/items/ItemIngredientsDialog.vue` is the recipe popup, opened from an article
  row in `ItemsListCategorySection.vue` through `ItemsList.vue`, on `BaseFormDialog` (wide,
  close-only) with the shared `AdminLoadFailureBar` at its top. Two `v-col` columns: "Im Rezept",
  the recipe lines as `BaseListRow`s with their amount field, edit pencil and remove trash, and
  "Verfügbare Zutaten", a search field over `BaseListRow`s of the ingredients this recipe does not
  hold (the name, then
  `admin.ingredients.labels.unitAfterName` in medium emphasis) with the pencil, the trash of
  `BaseDeleteAction`, and the plus (`BaseIconAction`, "Zutat hinzufügen") on every listed row. The
  plus sends `items.setIngredientAmount` with amount 1 once, is disabled while that request is in
  flight, and shows a refusal under the row on failure; on success the reloaded item carries the new
  line, whose amount field is focused with the 1 selected, and the ingredient leaves the available
  list. "Neue Zutat" under the list opens the form through the same `useEditSession` as the pencil.
- `admin/components/ingredients/IngredientDialog.vue` is the form (`nameToStartWith` gives a new
  ingredient its first name), and `IngredientAmountField.vue` a text field with the static unit name
  inside it at its right end (Vuetify's `append-inner` slot), on the recipe and the festival stock
  rows alike. `amountWhenCleared` (null by default) makes an empty field restore that number on blur
  or Enter and send it unless it already equals the saved amount; every recipe line passes 1, the
  stock rows pass nothing. `focusOnMount` focuses the input and selects its text once and emits
  `focused`. `admin/core/nameSearch.ts` (`nameMatchesTheSearch`) is the search: a part of the name,
  without regard to case. `admin/core/ingredientAmounts.ts` parses and formats the number without a
  unit or a conversion, and `admin/composables/useIngredientAmountText.ts` writes the stored number
  with the unit name.
- The unit chips in `IngredientDialog.vue` come from `admin/stores/units.ts`: they offer every unit
  by `unitId` and keep the ingredient's current unit with its carried `unitName` even when the store
  does not contain it. How the chips add, rename and delete a unit is on the `units` page.
  `ItemsList.vue` loads and listens to the ingredients and units stores, because the recipe popup
  lists the ingredients and creates them there.
- Stores: `admin/stores/ingredients.ts` (on `defineAdminList` with `withCreating` and `withDeleting`,
  handed out as `deleteIngredient`) and `items.ts` (`setIngredientAmount`, `removeIngredient`).
  Texts: `admin.ingredients.*` and `errors.admin.ingredients.*` in `shared/i18n/de.json` and
  `en.json`.

## Invariants

- An ingredient has no active flag, and the list returns every ingredient. Any existing ingredient
  can be added to a recipe.
- An ingredient is in use when any recipe line of any article points at it, whether the article is
  switched on or off, and an ingredient in use is never deleted. The list view names every such
  article (`usedByItemNames`, sorted by name in SQL), and the screen offers delete exactly when that
  list is empty. The delete route asks the same question and refuses with
  `common.errors.actionFailed` and problem code `IngredientUsedByItems`, because the screen already
  prevents the call; the log names the articles.
- Deleting an ingredient deletes its festival stock rows in the same save, and every ingredient
  write, the delete included, raises `ConfigurationChanged`, derived by the commit from the entities.
- A name is compared without regard to case, in SQL through `NamedQueries.Namesakes`. Creating or
  renaming to the name of another ingredient is refused with `errors.admin.ingredients.nameTaken`.
- A unit only has to exist when an ingredient is created, and when an existing ingredient changes
  to another unit. The screen offers only existing units, so a missing or unknown unit answers
  `common.errors.actionFailed`; an edit keeps the ingredient's current unit available from its
  carried `unitName`. A unit change never converts an amount: the recipe line and the stock row keep
  the number the admin entered, because the amount field has no unit of its own and no conversion
  helper exists.
- An amount is a finite number above zero. The request attribute requires the number and
  `SetRecipeLineAsync` refuses any other with `errors.admin.ingredients.amountInvalid`.
- The recipe popup shows every recipe line from the item view itself, with the name and unit the
  line carries, even when the ingredient list has not loaded or failed to load. It never drops a
  line and never takes the line's name or unit from the ingredients store; that store feeds the
  right column and the line's edit pencil. A renamed ingredient raises `ConfigurationChanged`, the
  items store reloads, and the open popup follows because `ItemsList.vue` hands it the item from
  that store.
- Each recipe line carries the same edit pencil as the available row, between its Menge field and
  its remove trash. The Menge field, the pencil and the trash stay on one line inside the row. A
  long ingredient name truncates with an ellipsis in both columns, and the edit form shows the full
  name. It opens the same ingredient form with the store's full ingredient, found by
  `line.ingredientId`; it appears only when the ingredients store holds that ingredient, so a list
  that is loading or failed leaves the line with its name, amount and remove action but no pencil.
  The form's save path is the existing one: the ingredient is updated, `ConfigurationChanged`
  reloads the item, and the line shows the new name and unit.
- The right column lists only the ingredients this recipe does not hold, matching a part of the name
  without regard to case and to spaces before or after the search text. Every listed row carries the
  plus, because every listed ingredient can be added. The trash (`BaseDeleteAction`) asks first, and
  it is enabled exactly when `usedByItemNames` is empty, with a disabled tooltip naming those
  articles. The ingredients store's used-by list follows a recipe change only when the ingredients
  are read again on `ConfigurationChanged`; a stale tooltip is possible until then, and the delete
  route remains the guard with `common.errors.actionFailed` under that row.
- Adding sends `items.setIngredientAmount` with amount 1 from the plus of every listed ingredient.
  The plus is disabled while that row's request is in flight, so a double press sends once. On
  success the line appears on the left from the reloaded item with amount 1, its amount field
  focused and the 1 selected; on failure the refusal shows under the ingredient row and the plus is
  usable again.
- Clearing a recipe line's Menge and leaving the field sets it back to 1 and sends 1. When the saved
  amount already is 1, the clearing only restores the displayed 1 and sends nothing. Invalid text
  shows `errors.admin.ingredients.amountInvalid` under that line and sends nothing.
- "Keine Einträge" shows in the right column only when the laptop lists no ingredient at all
  (`showsTheEmptyListText` over the whole list), never because of the search or because the recipe
  already holds every ingredient.
- "Neue Zutat" starts the form's name with the search text, exactly as typed, when the search shows
  no ingredient, and empty otherwise. A saved new ingredient appears in the available list and is
  not added to the recipe; the search is cleared first when it would hide that row (`afterSaving` of
  `useEditSession`).
- A recipe is read and changed only in that popup. The article row on the Artikel tab shows no
  recipe line.
- Removing a recipe line asks first (`BaseConfirmDialog`). A warning under a recipe line or under an
  ingredient row clears as soon as that row is edited again (see the row warnings page of `how-to`).
- The popup reloads on `ConfigurationChanged` through the stores `ItemsList.vue` listens to.

## Tests

- `GastronomyApp.Infrastructure.Tests/Services/IngredientServiceTest`, on the real DbContext with
  in-memory SQLite: the list with the articles that use each ingredient, switched on or off, and the
  delete with its stock rows and its guard for a switched-on and a switched-off article;
  `Queries/IngredientQueriesTest` (`UsedByItemNames` in a projection, sorted in SQL).
- `GastronomyApp.Infrastructure.Tests/Services/CatalogItemServiceTest`: the recipe cases and the
  activation cases of an article, on the real DbContext. The unit cases live in
  `GastronomyApp.Infrastructure.Tests/Services/UnitServiceTest`.
- `GastronomyApp.Infrastructure.Tests`: `Configurations/IngredientNameIndexTest` and
  `CatalogItemIngredientIndexTest` (the unique indexes), `UnitNameIndexTest`;
  `Persistence/ReleasedDatabaseUpgradeTest` (a switched-off ingredient no recipe uses, one a
  switched-off article's recipe uses, and active ones keep every recipe line, amount and stock row).
- `GastronomyApp.Api.Tests/Endpoints`: `AdminIngredientEndpointsTest` (the delete route with its stock
  row and its guard, and the article names of the list) and
  `AdminItemIngredientEndpointsTest` (including the name, unit id and unit name of every listed
  recipe line, the new name after a rename, and a unit change that keeps the amount); the
  announcements, the delete included, are in `Announcers/ConfigurationChangedAnnouncementTest`.
- `frontend/tests/admin`: `stores/ingredients.spec.ts` and `stores/items.spec.ts`;
  `components/ingredients/` (`IngredientDialog.spec.ts` and its unit specs on
  `ingredientDialogFixture.ts` (see the `units` page), `IngredientAmountField.spec.ts` for the
  cleared amount and the focus on mount);
  `components/items/` (`ItemIngredientsDialog.*.spec.ts` on `itemIngredientsDialogFixture.ts`:
  `.recipeLines` also opens the popup before the ingredient list has loaded and after it failed,
  `.availableIngredients` the available column, its search and its empty text, `.adding` the single
  amount-1 request, the focused new line, its refusal and a cleared amount, `.ingredientForm` the
  pencil on the available list and on a recipe line, "Neue Zutat" and its search clearing,
  `.ingredientDeleting` the trash;
  `ItemsList.ingredients.spec.ts` for a line added through the Artikel tab with amount 1, the unit
  trash reached through the open ingredient form and a recipe line renamed through its pencil,
  `ItemsList.renamedIngredient.spec.ts` for a rename behind the open recipe and
  `ItemsList.recipeReload.spec.ts` for the available list that follows an added or removed line once
  `ConfigurationChanged` reloads the ingredients); `components/BaseDeleteAction.spec.ts`;
  `components/AdminLoadFailureBar.spec.ts` (the bar inside the popup); `core/nameSearch.spec.ts`,
  `core/ingredientAmounts.spec.ts` and `composables/useIngredientAmountText.spec.ts`. Fixtures come
  from `anAdminIngredient`, `anAdminItem`, `anAdminItemIngredient` and `anAdminUnit` in
  `tests/support/wireViews.ts`.
