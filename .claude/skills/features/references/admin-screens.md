# The admin on the laptop

The admin works on the laptop: the festivals with their stations, table plan, articles and stock,
and the global lists of stations, articles and their categories, ingredients, units and waiters,
together with setting up the waiters' phones and the stations' tablets. Its theme follows the device,
light or dark, both palettes soft.

## Where the code lives

Frontend, under `frontend/src/`:

- `admin/views/AdminShell.vue` chooses between a global tab, the festival page and the editor of one
  area. `shared/router/route.ts` reads `/admin/<section>` and holds the tabs in `ADMIN_SECTIONS`, in
  the order Übersicht, Feste, Ausgabestellen, Artikel, Zutaten, Einheiten, Kellner. The festival
  list is the admin's first screen: switching tabs replaces the history entry, and back from any
  tab or from a festival's page lands on the festival list (see the `back-button` page).
- `admin/components/overview/AdminOverview.vue` is Übersicht, the report on the running festival.
- `admin/components/AdminLoadFailureBar.vue` is the one load failure bar, mounted by `AdminShell.vue`
  directly below the tabs. It reads `showsLoadFailure` from `admin/stores/adminLoadState.ts`.
- The global tabs: `festivals/FestivalsList.vue` (see the `festivals` page),
  `stations/StationsList.vue`, `items/ItemsList.vue`, `ingredients/IngredientsList.vue` (see the
  `ingredients` page), `units/UnitsList.vue` (see the `units` page) and `staff/StaffList.vue`. How a
  global tab is built is on the `admin-list-screen` page of `how-to`.
- `shared/theme.ts` holds both palettes, with `defaultTheme: 'system'`.
- `admin/styles/adminSizes.ts` and `adminSizes.css` hold every admin size: the Vuetify defaults
  `AdminShell.vue` provides to the admin and the CSS values its components read. The phone and the
  station tablet have their own, larger sizes. See the `surface-sizes` page of `how-to`.

## Invariants

- **The global tabs are plain lists** with no festival controls. The Artikel tab groups its articles
  under their categories and lists every category, a deactivated one with its chip, whatever
  "Deaktivierte anzeigen" says, so a category's arrows count every category.
  The backend for categories is `GastronomyApp.Core/Services/CatalogCategoryService.cs` (`ListAsync`, `CreateAsync`,
  `UpdateAsync`, `MoveAsync`, `DeactivateAsync`; switching on again is `ActivationService`), mapped in
  `Core/Mapping/CatalogCategoryMapping.cs`, with the routes in `Api/Endpoints/AdminCategoryEndpoints.cs`. A name is
  trimmed and compared in SQL without regard to case (`NamedQueries.Namesakes`); deactivating is refused while
  an active article is in the category.
- **Every row is the same row**: the global tabs and the festival page draw each entry with
  `BaseListRow.vue`, so names, chips, the line between rows and the tint on every second row look
  alike everywhere.
- **Every row offers its actions the same way**: text buttons first, then the pencil "Bearbeiten"
  for the edit dialog, then switching off, hiding or removing, with the up and down arrows before
  the pencil where a list is ordered by hand. Showing or switching on again is the open eye named
  by its own word. The shared pieces and the order are on the `hideable-rows` page of `how-to`.
- **A switch off the laptop would refuse is disabled before the click**, with the refusal's text as
  its tooltip: an article on an active festival's menu on the Artikel tab, a station with items to
  hand out at the active festival or whose articles would be left without a station on the
  Ausgabestellen tab, an ingredient still in a switched-on article on the Zutaten tab, and a unit an
  ingredient still uses on the Einheiten tab. The units list names those ingredients in the refusal
  text.
- **One bar reports every load failure, the same on every screen.** Directly below the tabs, full
  width and in the flow of the page, it shows `admin.common.errors.loadFailed` while the last load
  of any admin list the current screen shows failed, or while the hub connection to the laptop is
  reconnecting or closed. It is hidden while the first connection is still starting. No screen
  shows a load failure of its own. A list counts while its screen listens to it: every admin store
  listens through `useAdminLoadStateStore().listenToTheLaptop(events, reload, loadFailed)`, which
  registers the store's `loadFailed` until the screen closes, so a list a former screen failed to
  load never keeps the bar up. After the laptop restarts, the reconnect's `GroupsJoined` reloads
  every listening list, each successful load clears its `loadFailed`, and the bar goes away
  without a click. Übersicht listens to its four lists like every other screen.
- **Everything about one festival is on its page** (see the `festivals` page), and an unknown
  festival id returns to Übersicht without a word.

## Tests

`frontend/tests/admin/views/AdminShell.spec.ts`, `shared/router/adminRoute.spec.ts`,
`admin/components/AdminLoadFailureBar.spec.ts` (the bar on several tabs, once on the festival page,
on a lost connection and gone after the reconnect), `admin/stores/adminLoadState.spec.ts` (the
derivation, and that every admin store listens through it),
`admin/components/overview/AdminOverview.spec.ts`, `shared/theme.spec.ts` and
`admin/views/AdminShell.compactSizes.spec.ts` (compact controls inside the admin, the phone's left as they are). The disabled switch off
of the Ausgabestellen tab, with both reasons and the one named when both apply, is in
`admin/components/stations/StationsList.switchingOff.spec.ts`. The Einheiten tab is covered by
`admin/components/units/UnitsList.*.spec.ts`, `UnitDialog.spec.ts` and `admin/stores/units.spec.ts`;
the backend rules they display are in `AdminUnitEndpointsTest` and
`UnitServiceTest`.
