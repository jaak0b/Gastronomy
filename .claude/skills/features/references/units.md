# Units

The admin keeps the units of measure inside the form of an ingredient ("Neue Zutat", "Zutat
bearbeiten"), where every unit is a chip: the laptop starts with Stück, g, kg, ml and l, a tap
chooses the unit of the ingredient, and the chips add, rename and delete units. A unit is global, not tied to
a festival, and its name is data rather than a translated label. Every ingredient points at one unit,
and the amount an ingredient carries is a plain number in that unit.

## Where the code lives

Backend, under `backend/`:

- `GastronomyApp.Core/Entities/Unit.cs` (name, ingredients; no active flag) and
  `GastronomyApp.Core/Entities/Ingredient.cs` (`UnitId` and the `Unit` navigation). Both raise
  `ConfigurationChanged`.
- `UnitConfiguration`: the key and the required name with `NOCASE` collation and its unique index.
  `IngredientConfiguration` carries the ingredient's unit foreign key with `Restrict` and the index
  on `UnitId`.
- `GastronomyApp.Core/Services/UnitService.cs` lists, creates, renames and deletes units through
  `IGastronomyDatabase`. Whether a unit is in use is asked in one place,
  `UnitQueries.UsedByIngredientNames` in `Core/Queries` (a `[Projectable]` check): the list view
  names those ingredients through it in SQL, and the delete guard asks the same question.
- `GastronomyApp.Core/Services/IngredientService.cs` takes a `Guid?` unit id on create and update.
  The unit only has to exist; changing the unit of an existing ingredient is allowed at any time and
  leaves every stored amount untouched.
- Refusals: `GastronomyApp.Core/Refusals/Refusal.Units.cs`, and
  `Refusal.Ingredients.UnitMissingOrUnknown(Guid?)` for a missing or unknown unit.
- Routes: `GastronomyApp.Api/Endpoints/AdminUnitEndpoints.cs` (`/api/admin/units`: list, create,
  `PUT /{unitId}`, `DELETE /{unitId}`), which call `UnitService` directly.
- Wire records: `GastronomyApp.Contracts/Admin/Units` (`AdminUnitView`, `AdminUnitListView`,
  `SavedUnitView`, `SaveUnitRequest`). Mapping: `GastronomyApp.Core/Mapping/UnitMapping.cs`;
  `Core/Mapping/IngredientMapping.cs` and `CatalogItemMapping.cs` read the unit id and name from the
  navigation.
- Migration `20261025095050_AddUnits` seeds Stück, g, kg, ml and l with fixed ids and maps the old
  ingredient unit values 1, 2 and 3 to Stück, g and ml. The released-database upgrade test builds a
  database with ingredients in all three old units, a recipe line and a stock amount, migrates it and
  proves that names, amounts and units survive.
- Migration `20261030090803_RemoveUnitActiveFlag` drops the units' active flag and deletes no unit:
  a unit that was switched off, used or not, stays as an ordinary unit. The upgrade test proves it
  on a database holding a switched-off unused unit, a switched-off unit an ingredient uses, and
  active units with ingredients, recipe lines and stock amounts.

Frontend, under `frontend/src/`:

- `admin/components/ingredients/IngredientDialog.vue` shows the units of that store as Vuetify's own
  `v-chip-group` (`filter`, `mandatory`, `column`), in the order the laptop sends them, which is by
  name without regard to case (`UnitService.ListAsync`: g, kg, l, ml, Stück for the seeded units): a tap
  chooses a unit, the chosen chip shows the check mark, and tapping it again keeps it. The
  ingredient's current unit stays a chip from its carried `unitName` when the store lacks it, has
  not loaded or failed. The chosen chip carries the pencil (`BaseEditButton`), which opens
  `admin/components/units/UnitDialog.vue` through `useEditSession` to rename the unit for every
  ingredient at once, without waiting for the ingredient's Speichern. The chip "Neue Einheit" after
  the group opens the same form empty, and the unit it creates becomes the chosen one
  (`afterSaving`). Every chip of the list carries the trash of `BaseDeleteAction`, hidden with
  `opacity` until the chip is hovered or the trash has keyboard focus; it keeps its space, so the
  chip does not change width. On a unit whose `usedByIngredientNames` is empty it asks first and
  deletes on confirm; on a unit in use it is disabled and its tooltip names
  those ingredients (`admin.units.messages.usedByIngredients`). The pencil on the chosen chip stays
  visible. The chip known only from the ingredient's carried `unitName` has no list data and shows no
  trash button. A refused create or rename shows in the unit form, a refused delete under the chips
  (`BaseRefusalAlert`, `data-test="unit-refusal"`); choosing a chip or opening the form clears it.
  The pencil and the trash button inside a chip take their size from `ADMIN_CHIP_BUTTON_DEFAULTS` in
  `admin/styles/adminSizes.ts`. The form shows `AdminLoadFailureBar` at its top, so a failed load of
  the units is not hidden behind the dimmed page.
- `admin/stores/units.ts` sits on `defineAdminList` with `withCreating` and `withDeleting` at
  `/api/admin/units`, hands the layer's `deleteEntry` out as `deleteUnit`, and reloads on
  `ConfigurationChanged`. `ItemsList.vue` loads and listens to it, so a failed load of the units
  shows in the admin's load failure bar like every other list.
- The recipe popup (both columns) and the stock rows show `unitName` from the wire view.
- `admin/core/ingredientAmounts.ts` parses and formats a plain amount with no conversion, and
  `admin/composables/useIngredientAmountText.ts` writes the stored number with the unit name.
- Texts: `admin.units.*` (the unit form, "Neue Einheit", the question before a delete and the
  reason on a disabled trash button) and
  `errors.admin.units.*` in `shared/i18n/de.json` and `en.json`.

## Invariants

- Units are global and have no active flag. The list returns every unit.
- A unit name is unique without regard to case. Creating or renaming to the name of another unit is
  refused with `errors.admin.units.nameTaken` and the parameter `name`.
- A unit is in use when any ingredient points at it, and a unit in use is
  never deleted. The list view names every such ingredient, and the screen enables delete
  exactly when that list is empty. The delete route asks the same question and refuses with
  `common.errors.actionFailed` and problem code `UnitUsedByIngredients`, because the screen already
  prevents the call; the log names the ingredients.
- An ingredient may change its unit at any time. The numeric amounts stay exactly as they were
  entered, and no conversion happens anywhere: a recipe line or stock amount is the number in the
  unit the ingredient carries.
- A renamed unit is data. Every recipe line and stock row reads the current name through the
  ingredient's navigation, so a rename shows up without touching the stored amounts.
- Every unit write, the delete included, raises `ConfigurationChanged`, derived by the commit from
  the entity.
- The screen never chooses a unit in the admin's place: a new ingredient starts with none, and
  deleting the chosen unit leaves none chosen.

## Tests

- `GastronomyApp.Core.Tests/Services`: the ingredient tests that construct an ingredient with a unit.
- `GastronomyApp.Infrastructure.Tests/Queries`: `NamedQueriesTest` (the namesake check of units and
  ingredients, in SQL) and `UnitQueriesTest` (the names of every ingredient that uses a unit, sorted
  in SQL).
- `GastronomyApp.Infrastructure.Tests/Services`: `UnitServiceTest` (every unit action on the real
  DbContext with in-memory SQLite), `Configurations/UnitNameIndexTest` (the unique name index),
  `IngredientServiceTest` (including a unit change that
  leaves a recipe amount unchanged) and `FestivalServiceTest` (the stock rows of a new festival).
- `GastronomyApp.Infrastructure.Tests/Persistence`: `ReleasedDatabaseUpgradeTest` covers the seeded
  units, the migration of the old unit values and the dropped active flag, and
  `GastronomyAppDbContextTest` covers the new table.
- `GastronomyApp.Api.Tests/Endpoints`: `AdminUnitEndpointsTest`, `AdminIngredientEndpointsTest`,
  `AdminItemIngredientEndpointsTest` and `AdminFestivalIngredientEndpointsTest` cover the routes, the
  unit id and name on every view, the delete route with its guard and an ingredient unit change that
  keeps the amounts.
- `GastronomyApp.Infrastructure.Tests/Services/CatalogItemServiceTest` covers the unit id and name of
  a recipe line; `GastronomyApp.Api.Tests/Mapping/MappingConfigurationTest` covers the new mapping
  registration.
- `GastronomyApp.Api.Tests/Announcers`: `ConfigurationChangedAnnouncementTest` covers create, update
  and delete on the units routes.
- `frontend/tests`: `admin/stores/units.spec.ts` (the units routes, the delete and its refusal),
  `admin/stores/adminList.spec.ts` (the `withDeleting` layer), `admin/components/ingredients/`
  (`IngredientDialog.spec.ts`, `IngredientDialog.unitChoice.spec.ts` for choosing and the carried
  current unit, `IngredientDialog.unitNames.spec.ts` for renaming and creating, and
  `IngredientDialog.unitDeleting.spec.ts` for the trash button, its hover-reveal marker, its enabled
  or disabled state with the ingredients named, and the delete, all on `ingredientDialogFixture.ts`),
  `admin/components/items/ItemsList.ingredients.spec.ts` for the trash reached through the recipe
  popup and the ingredient form on the Artikel tab,
  `admin/components/units/UnitDialog.spec.ts`, `admin/stores/screenListening.spec.ts` and
  `admin/components/AdminLoadFailureBar.spec.ts` (a failed unit load on the Artikel tab, and the bar
  inside the ingredient form). Fixtures come from `anAdminUnit`, `PIECE_UNIT`, `GRAM_UNIT`,
  `MILLILITRE_UNIT` and `KISTE_UNIT` in `tests/support/wireViews.ts`. jsdom cannot compute `:hover`,
  so the spec asserts the trash stays in the DOM with its `unit-trash` marker; the opacity reveal
  itself is visual CSS and is not asserted.
