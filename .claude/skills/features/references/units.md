# Units

The admin keeps the units of measure on the Einheiten tab: Stück, g, kg, ml and l start the list,
and the admin can add, rename, deactivate and reactivate a unit there. A unit is global, not tied to
a festival, and its name is data rather than a translated label. Every ingredient points at one unit,
and the amount an ingredient carries is a plain number in that unit.

## Where the code lives

Backend, under `backend/`:

- `GastronomyApp.Core/Entities/Unit.cs` (name, active flag, ingredients) and
  `GastronomyApp.Core/Entities/Ingredient.cs` (`UnitId` and the `Unit` navigation). Both raise
  `ConfigurationChanged`.
- `UnitConfiguration`: the key, the required name with `NOCASE` collation and its unique index, and
  the active flag. `IngredientConfiguration` carries the ingredient's unit foreign key with
  `Restrict` and the index on `UnitId`.
- `GastronomyApp.Core/Services/UnitService.cs` lists, creates, renames and deactivates units through
  `IGastronomyDatabase`; activating goes through the shared `ActivationService`. The list view and
  the deactivation check both name the ingredients that use a unit, sorted by name.
- `GastronomyApp.Core/Services/IngredientService.cs` takes a `Guid?` unit id on create and update. A new unit must exist and be active, while changing the unit of an existing ingredient
  is allowed at any time and leaves every stored amount untouched.
- Refusals: `GastronomyApp.Core/Refusals/Refusal.Units.cs`, and
  `Refusal.Ingredients.UnitUnavailable(Guid?)` for a missing, unknown or switched-off unit.
- Routes: `GastronomyApp.Api/Endpoints/AdminUnitEndpoints.cs` (`/api/admin/units`: list, create,
  `PUT /{unitId}`, `POST /{unitId}/activate`, `POST /{unitId}/deactivate`), which call the services
  directly.
- Wire records: `GastronomyApp.Contracts/Admin/Units` (`AdminUnitView`, `AdminUnitListView`,
  `SavedUnitView`, `SaveUnitRequest`). Mapping: `GastronomyApp.Core/Mapping/UnitMapping.cs`;
  `Core/Mapping/IngredientMapping.cs` and `AdminItemMapping.cs` read the unit id and name from the
  navigation.
- Migration `20261025095050_AddUnits` seeds Stück, g, kg, ml and l with fixed ids and maps the old
  ingredient unit values 1, 2 and 3 to Stück, g and ml. The released-database upgrade test builds a
  database with ingredients in all three old units, a recipe line and a stock amount, migrates it and
  proves that names, amounts and units survive.

Frontend, under `frontend/src/`:

- `admin/components/units/UnitsList.vue` is the Einheiten tab and `UnitDialog.vue` its create and
  rename form. Both are the second complete copy of the Zutaten tab and use the same shared pieces
  (`defineAdminList`, `useEditSession`, `BaseHeadingWithCheckbox`, `BaseFormDialog`,
  `BaseConfirmDialog`, `BaseActionWithRefusalReason`).
- `admin/stores/units.ts` sits on `defineAdminList` at `/api/admin/units` and reloads on
  `ConfigurationChanged`. `shared/router/route.ts` carries the `units` section after `ingredients`,
  and `admin/views/AdminShell.vue` mounts the tab and its title.
- The unit picker in `admin/components/ingredients/IngredientDialog.vue` offers the active units of
  that store by `unitId`, and keeps the ingredient's current unit with its carried `unitName` when
  the store lacks it or failed. `IngredientsList.vue`, the recipe dialog and the stock rows show
  `unitName` from the wire view.
- `admin/core/ingredientAmounts.ts` parses and formats a plain amount with no conversion, and
  `admin/composables/useIngredientAmountText.ts` writes the stored number with the unit name.
- Texts: `admin.units.*` and `errors.admin.units.*` in `shared/i18n/de.json` and `en.json`.

## Invariants

- Units are global. The list returns every unit, active and inactive; the screen lists an active unit
  and lists an inactive one only while "Deaktivierte anzeigen" is ticked.
- A unit name is unique without regard to case. Creating or renaming to the name of an active unit is
  refused with `errors.admin.units.nameTaken`; the name of a deactivated unit is refused with
  `errors.admin.units.nameBelongsToDeactivatedUnit` and the parameter `name`.
- A unit cannot be deactivated while an ingredient uses it. The server refuses with
  `errors.admin.actionFailed` and problem code `UnitUsedByIngredients`; the list disables the button
  and names the ingredients, because the screen already prevents the call.
- An ingredient may change its unit at any time. The numeric amounts stay exactly as they were
  entered, and no conversion happens anywhere: a recipe line or stock amount is the number in the
  unit the ingredient carries.
- A renamed unit is data. Every recipe line and stock row reads the current name through the
  ingredient's navigation, so a rename shows up without touching the stored amounts.
- Every unit write raises `ConfigurationChanged`, derived by the commit from the entity.

## Tests

- `GastronomyApp.Core.Tests/Services`: the ingredient tests that construct an ingredient with a unit.
- `GastronomyApp.Infrastructure.Tests/Repositories`: `IngredientRepositoryTest`,
  `IngredientStockRepositoryTest` (the unit navigation), `CatalogItemRepositoryTest` and
  `CatalogItemIngredientRepositoryTest`.
- `GastronomyApp.Infrastructure.Tests/Queries`: `NamedQueriesTest` (the namesake check of units and
  ingredients, in SQL).
- `GastronomyApp.Infrastructure.Tests/Services`: `UnitServiceTest` (every unit action on the real
  DbContext with in-memory SQLite), `Configurations/UnitNameIndexTest` (the unique name index),
  `IngredientServiceTest` (including a unit change that
  leaves a recipe amount unchanged) and `FestivalAdministrationServiceTest`.
- `GastronomyApp.Infrastructure.Tests/Persistence`: `ReleasedDatabaseUpgradeTest` covers the seeded
  units and the migration of the old unit values, and `GastronomyAppDbContextTest` covers the new
  table.
- `GastronomyApp.Api.Tests/Endpoints`: `AdminUnitEndpointsTest`, `AdminIngredientEndpointsTest`,
  `AdminItemIngredientEndpointsTest` and `AdminFestivalIngredientEndpointsTest` cover the routes, the
  unit id and name on every view, the deactivation guard and an ingredient unit change that keeps
  the amounts.
- `GastronomyApp.Api.Tests/Mapping`: `AdminItemMappingTest` covers the unit id and name of a recipe
  line; `MappingConfigurationTest` covers the new mapping registration.
- `GastronomyApp.Api.Tests/Announcers`: `ConfigurationChangedAnnouncementTest` covers create, update,
  activate and deactivate on the units routes.
- `frontend/tests`: `admin/stores/units.spec.ts`, `admin/components/units/` (`UnitsList.listing.spec.ts`,
  `UnitsList.editing.spec.ts` and `UnitsList.switchingOff.spec.ts` on `unitsListFixture.ts`, plus
  `UnitDialog.spec.ts`), `admin/stores/screenListening.spec.ts`, `admin/views/AdminShell.spec.ts` and
  `shared/router/adminRoute.spec.ts`. Fixtures come from `anAdminUnit` in `tests/support/wireViews.ts`.
