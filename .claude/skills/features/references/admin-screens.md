# The admin on the laptop

The admin works on the laptop: the festivals with their stations, table plan, articles and stock,
and the global lists of stations, articles and their categories, ingredients and waiters, together
with setting up the waiters' phones and the stations' tablets. Its theme follows the device, light
or dark, both palettes soft.

## Where the code lives

Frontend, under `frontend/src/`:

- `admin/views/AdminShell.vue` chooses between a global tab, the festival page and the editor of one
  area. `shared/router/route.ts` reads `/admin/<section>` and holds the tabs in `ADMIN_SECTIONS`, in
  the order Übersicht, Feste, Ausgabestellen, Artikel, Zutaten, Kellner.
- `admin/components/overview/AdminOverview.vue` is Übersicht, the report on the running festival.
- The global tabs: `festivals/FestivalsList.vue` (see the `festivals` page),
  `stations/StationsList.vue`, `items/ItemsList.vue`, `ingredients/IngredientsList.vue` (see the
  `ingredients` page) and `staff/StaffList.vue`. How a global tab is built is on the
  `admin-list-screen` page of `how-to`.
- `shared/theme.ts` holds both palettes, with `defaultTheme: 'system'`.

## Invariants

- **The global tabs are plain lists** with no festival controls. The Artikel tab groups its articles
  under their categories and lists every category, a deactivated one with its chip, whatever
  "Deaktivierte anzeigen" says, so a category's arrows count every category.
- **Every row offers its actions the same way**: text buttons first, then the pencil "Bearbeiten"
  for the edit dialog, then switching off, hiding or removing, with the up and down arrows before
  the pencil where a list is ordered by hand. Showing or switching on again is the open eye named
  by its own word. The shared pieces and the order are on the `hideable-rows` page of `how-to`.
- **A switch off the laptop would refuse is disabled before the click**, with the refusal's text as
  its tooltip: an article on an active festival's menu on the Artikel tab, a station with items to
  hand out at the active festival or whose articles would be left without a station on the
  Ausgabestellen tab, an ingredient still in a switched-on article on the Zutaten tab.
- **Everything about one festival is on its page** (see the `festivals` page), and an unknown
  festival id returns to Übersicht without a word.

## Tests

`frontend/tests/admin/views/AdminShell.spec.ts`, `shared/router/adminRoute.spec.ts`,
`admin/components/overview/AdminOverview.spec.ts` and `shared/theme.spec.ts`. The disabled switch off
of the Ausgabestellen tab, with both reasons and the one named when both apply, is in
`admin/components/stations/StationsList.switchingOff.spec.ts`.
