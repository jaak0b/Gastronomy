# Table plan

A festival has areas ("Bereich" / "Area" on screen: a hall, a tent, the garden). Each area holds its
tables and the shapes of the festival's stations placed in it, each drawn as a rectangle the admin
can move and resize. Every order belongs to one table of the plan. A waiter's phone reads the
running festival's plan, where each table shows by its fill how far its orders are handed out and by
its border how far they are settled. The admin draws the plan in an editor of its own, opened from
the "Tischplan" card of the festival page. The phone opens on the plan: the list of areas, or the
plan itself when there is only one area, and tapping a table offers to take an order there or to
settle it. In code the whole thing is the table plan, never a layout.

## Where the code lives

Backend, under `backend/`:

- `GastronomyApp.Core/Entities/Area.cs`: `FestivalId`, `Name`, `SortOrder` (the order of the area
  buttons on the phone), `IsHidden`, with `Tables` and `StationShapes`.
- `AreaShape.cs` is the abstract base of whatever is drawn in an area: `FestivalId`, `AreaId`, `X`,
  `Y`, `Width`, `Height`, `Rotation` and the `Festival` and `Area` navigations. It is not an EF entity
  type: no inheritance mapping, no discriminator, no table of its own. Each derived class has its own
  table carrying these columns.
- `Table.cs` (`Id`, `Name`, `IsHidden`, with `Orders`) and `FestivalStationShape.cs`
  (`FestivalStationId`, which is both its key and its link to `FestivalStation`; a row means the
  station is on the plan) derive from it. All three entity types raise `ConfigurationChanged`.
- `Order.TableId` with its `Table` navigation names the table an order belongs to; the order keeps
  no name of its own.
- `Core/Services/AreaShapeService.cs` is the only code that writes the five geometry values
  (`Reshape`) and reads them back for a copy (`GeometryOf`). The table service, the station shape
  service and the festival copy all go through it.
- Configurations in `Infrastructure/Configurations`: `AreaConfiguration`, `TableConfiguration`
  (with the alternate key `(Id, FestivalId)`), `FestivalStationShapeConfiguration`, the alternate
  key `(Id, FestivalId)` in `FestivalStationConfiguration`, and the order's table key in
  `OrderConfiguration`. Migrations `AddTablePlan` (tables `Areas`, `Tables`,
  `FestivalStationShapes`) and `SwitchOrdersToTables` (see the invariants).
- Services: `AreaAdministrationService` (`ListTablePlanAsync`, create, update, hide, show, delete),
  `TableAdministrationService` (create, update including a move to another area, hide, show, delete)
  and `FestivalStationShapeService` (place or move, remove). Each takes its request record. Port:
  `ITablePlanRepository` (`TablePlanRepository`) owns every read and write of areas, tables and
  station shapes, and answers the order questions the hide and delete rules ask
  (`HasOrdersAtTableAsync`, `HasOrdersInAreaAsync`, `HasItemsToHandOutOrSettleAtTableAsync`,
  `HasItemsToHandOutOrSettleInAreaAsync`). `OrderAcceptanceService` looks the order's table up through
  `FindTableAsync`; the open items screen reads one table with its orders through
  `IOpenItemRepository.FindTableWithOrdersAsync` (see the `settling-a-table` page).
- The phone's plan: `TablePlanService.ReadRunningFestivalTablePlanAsync` asks `RunningFestivalLookup`
  for the running festival and reads its plan through
  `ITablePlanRepository.FindShownAreasWithItemFlagsAsync`. That is one SQL statement, so one
  snapshot, which projects into the records in `Core/Results`: `ShownArea` (the `Area` with its
  `Tables` and `StationShapes`), `ShownTable` (the `Table` and `ItemFlagsPerOrder`, one list of
  `OrderItemFlags` (`IsHandedOut`, `IsSettled`) per order, over all its stations) and
  `ShownStationShape` (the `FestivalStationShape` and its `StationName`). No order or item entity
  is loaded, because every phone reads this again on every `OrdersChanged`.
  `Core/Services/TableService.cs` derives a table's two colour states, `HandOutProgressOf` and
  `SettlementProgressOf`, each through `OrderService.ShareOfFlaggedItems`, the one function that
  turns item flags into `None`, `Some` or `All` and that `OrderService.StatusOf` uses as well.
- `FestivalStationService.RemoveAsync` deletes the station's shape together with its link through
  `ITablePlanRepository`, and `FestivalRepository.CopyContentsAsync` copies the plan (see the
  invariants).
- Refusals: `GastronomyApp.Core/Refusals/Refusal.TablePlan.cs`.
- Routes in `GastronomyApp.Api/Endpoints/AdminFestivalEndpoints.cs`, under
  `/api/admin/festivals/{festivalId}`: `GET /table-plan`, `POST /areas`, `PUT /areas/{areaId}`,
  `POST /areas/{areaId}/hide` and `/show`, `DELETE /areas/{areaId}` (`AdminAreaHandler`);
  `POST /tables`, `PUT /tables/{tableId}`, `POST /tables/{tableId}/hide` and `/show`,
  `DELETE /tables/{tableId}` (`AdminTableHandler`); `PUT` and `DELETE /stations/{stationId}/shape`
  (`AdminFestivalStationShapeHandler`, keyed by the station id, not the link id). The phone's
  `GET /api/table-plan` lives in `GastronomyApp.Api/Endpoints/TablePlanEndpoints.cs`
  (`TablePlanHandler`), for staff phones only, like the catalog and open items routes.
- Wire records: `AreaShapeGeometry` in `GastronomyApp.Contracts/TablePlan` (`X`, `Y`, `Width`,
  `Height`, `Rotation`, nested as `Geometry` in every admin and phone request and view that carries
  a shape, so the frontend hands it straight to Konva). In `GastronomyApp.Contracts/Admin/Festivals`:
  `SaveAreaRequest`, `SaveTableRequest` (names the area, so the
  same record creates and moves a table), `SaveStationShapeRequest`, `SavedAreaView`,
  `SavedTableView`, `AdminTablePlanView`, `AdminAreaView`, `AdminTableView`, `AdminStationShapeView`
  (`StationName`); a station shape answers with `SavedStationView`. Mapping:
  `GastronomyApp.Api/Mapping/AdminTablePlanMapping.cs`. The phone's records are in
  `GastronomyApp.Contracts/TablePlan`: `TablePlanView` (`Areas`), `TablePlanAreaView` (`AreaId`,
  `Name`, `Tables`, `StationShapes`), `TablePlanTableView` (`TableId`, `Name`, `Geometry`,
  `HandOutProgress`, `SettlementProgress`) and `TablePlanStationShapeView` (`StationName`,
  `Geometry`); both states are a `ShareState` (`Contracts/Enums`: `None`, `Some`, `All`, on the
  wire `none`, `some`, `all`, the same word the phone's open items code uses). Mapping:
  `GastronomyApp.Api/Mapping/TablePlanMapping.cs`, from the `Shown*` records, which calls
  `TableService` for the two states.
- Texts: `errors.admin.festivals.areaNameTaken`, `tableNameTaken`, `areaStaysWhileTheFestivalRuns`,
  `tableStaysWhileTheFestivalRuns`, `areaWithOrdersStays`, `tableWithOrdersStays`,
  `areaWithItemsToHandOutOrSettleStaysShown` and `tableWithItemsToHandOutOrSettleStaysShown` in
  `frontend/src/shared/i18n/de.json` and `en.json`.

Frontend, under `frontend/src/`:

- `admin/components/festivals/FestivalTablePlan.vue` is the "Tischplan" card of the festival page:
  each area with its number of tables, a hidden area with the "Ausgeblendet" chip, and the button
  "Tischplan bearbeiten". It warns only about what waiters see: no shown area
  (`admin.tablePlan.messages.noArea`), or shown areas without a shown table (`noTable`).
- The editor is its own page at `/admin/festivals/{festivalId}/table-plan`: `shared/router/route.ts`
  reads it as the admin route with `festivalSubpage: 'tablePlan'`, and `admin/views/AdminShell.vue`
  chooses `admin/components/tablePlan/TablePlanEditor.vue` and lets it fill the screen height. The
  editor owns the selected area (the first one when it opens) and the selected shape
  (`ShapeSelection` in `admin/core/tablePlanSelection.ts`) and lays out three columns:
  `TablePlanAreas.vue` (select, create, rename, move up and down, hide, show, delete),
  `TablePlanDrawing.vue` (the Konva drawing) and on the right `TablePlanShapeDetails.vue` above
  `TablePlanAdding.vue` ("Tisch hinzufügen" and the festival's stations on no plan yet).
  `TablePlanShapeDetails.vue` stays mounted while the selection changes and shows one block per shape:
  the selected one first, then every shape that carries a refusal or a typed name, each with its
  name field, hide, show and delete for a table, or "Aus dem Tischplan nehmen" for a station.
  Refusals are kept per shape (`useRowRefusals` keyed by the shape's Konva id) and typed names per
  table and per area (`admin/composables/useTypedNames.ts`). An unknown festival returns to the
  overview through `admin/composables/useOpenedFestival.ts`, the guard the festival page uses too.
- `admin/stores/tablePlan.ts` (`useAdminTablePlanStore`) holds the festival's `AdminAreaView` list
  and has one action per admin route; every action reloads the plan through the latest request gate,
  and the store reloads on `ConfigurationChanged`. `moveArea` writes the new `SortOrder` of every area
  whose place changes (`admin/core/areaOrder.ts` numbers them from 1 in the shown order) and reads the
  plan once at the end, or at the first refusal.
- `shared/core/tablePlanDrawing.ts` holds `TABLE_PLAN_DRAWING_WIDTH` (400 units, for the phone as
  well), `cornersOf` and `lowestEdgeOf`. `admin/core/tablePlanSnapping.ts` holds the snapping
  geometry adapted from Konva's Objects Snapping demo (`snapStopsBeside`, `snapDraggedBox`,
  `snapResizeCorner`, a grid of 10 units and a snap distance of 6), and
  `admin/core/tablePlanPlacement.ts` where a new shape goes, the smallest size, the room below the
  lowest shape and `fitsTheDrawing`.
- Colours: `shared/core/tablePlanColours.ts` builds the light and dark palette from tokens of
  `shared/theme.ts` through `shared/core/themeColours.ts`, and the drawing picks the one of the
  current theme with `shared/composables/useThemedValue.ts`, as the charts do. In the editor a table
  is `surface-light` with an `on-surface` border, a station is inverted (`primary` with `on-primary`
  lettering), a hidden table has a dashed muted border and the word "Ausgeblendet" under its name.
  On the phone `handOutFill` and `settlementBorder` take each `ShareState`'s status token from
  `shared/core/statusTints.ts`: the border takes the token itself, the fill takes `statusTintOf`.
  That module is the one home of the mapping (none `error`, some `warning`, all `success`) and of the
  tint strength (`STATUS_TINT_PERCENT`, thirty percent of the token mixed into `surface`), and the
  settle view's cards and rows take the same through `statusTintCssOf`.
- The waiter's phone: `phone/views/TablePlan.vue` is the home screen (route `home`, screen
  `tablePlan`). `shared/core/landing.ts` decides what it shows (`waiterLandingFor`, read through
  `phone/composables/useWaiterLanding.ts`): the locked order first, then no festival active (the
  catalog names none) or no tables, the plan of the only area, the plan of the area the waiter
  opened, or the area list. Until the plan has loaded, and while an empty plan waits for the catalog,
  it claims nothing. A plan of an area without tables says so above the drawing. It shows
  `phone/components/tablePlan/AreaList.vue` (one large button per area, in the laptop's order),
  `AreaPlan.vue` (the read-only Konva drawing of one area), `TableActionSheet.vue` (the table's name,
  "Bestellung aufnehmen", "Abrechnen", "Abbrechen") and `UnsentOrderDialog.vue` (the question when
  another table's unsent items are on the phone). `phone/components/header/AppHeader.vue` carries
  the back control ("Bereiche" from a plan when there are several areas, "Tischplan" from the
  ordering screen and the settle view) and the area or table name.
- `phone/core/tablePlanCamera.ts` is the camera of the phone's drawing: `cameraFittingTheWidth`,
  `clampedPosition`, `pinchOf`, `pinchedCamera` (zoom about the point between the fingers, from the
  width of the screen up to `DEEPEST_ZOOM`, four times that) and `waiterDrawingLength` (the lowest
  shape plus `ROOM_BELOW_THE_LOWEST_SHAPE`).
- `phone/stores/tablePlan.ts` (`useTablePlanStore`: `areas`, `hasLoaded`, `loadFailed`,
  `openedAreaId`, `load`, `listen`, `openArea`, `closeArea`) reads `GET /api/table-plan` through the
  latest request gate and reloads on `ConfigurationChanged` (the plan) and `OrdersChanged` (the
  colours); `App.vue` starts its listeners and its first load with the catalog's, and the connection's
  refetch on `GroupsJoined` reloads it.
- Texts: `phone.tablePlan.*`.
- Texts: `admin.tablePlan.*` (the fields for a new area and a new table have labels of their own,
  `newAreaName` and `newTableName`), `errors.admin.festivals.areaNameMissing` and
  `tableNameMissing`.

## Invariants

- **Geometry values are Konva's own and are stored exactly as received**, as doubles: no rounding,
  no normalising, no range check. `X`, `Y` is the top-left corner before rotation for a table and a
  station alike; `Rotation` is degrees clockwise about that corner and may be negative.
- **SQLite itself refuses a festival mix-up.** A table's and a station shape's `(AreaId, FestivalId)`
  point at the area's alternate key `(Id, FestivalId)`, a station shape's
  `(FestivalStationId, FestivalId)` at the festival station's, and an order's `(TableId, FestivalId)`
  at the table's. Every relation is `Restrict`.
- **An order shows its table's current name**, read through `Order.Table` on the station cards and
  the one-table view, so renaming a table renames it on every order.
- **Names are unique per festival without regard to case, hidden rows included**, for areas and for
  tables (collation `NOCASE`, unique index on `(FestivalId, Name)`). The service checks first and
  refuses with `areaNameTaken` or `tableNameTaken`.
- **Nothing is deleted while the festival runs**, neither an area nor a table. "Running" comes from
  `RunningFestivalLookup`.
- **A table with an order is never deleted**, running festival or not, and an area is deleted only
  when none of its tables has an order (`tableWithOrdersStays`, `areaWithOrdersStays`). Both can
  only be hidden.
- **Hiding waits for the table to be finished while the festival runs.** A table with an item not
  yet handed out or not yet settled is not hidden, nor is an area one of whose tables has such an
  item (`tableWithItemsToHandOutOrSettleStaysShown`, `areaWithItemsToHandOutOrSettleStaysShown`). After the festival, and for
  a row that is hidden already, hiding always succeeds. Showing and every other change stay allowed.
- **A hidden table still takes orders.** The phone only offers the plan's tables, so placing an
  order checks only that the table belongs to the running festival.
- **Deleting an area deletes its tables and station shapes through the tracker**, and taking a
  station off a festival deletes its shape the same way, so the restricting keys never fire.
- **An id that does not belong to the festival is a broken program.** An unknown area, table,
  station or station shape in the route answers a bare 404; an area of another festival named in a
  body answers 400 with `RefusalMessageKeys.AdminActionFailed`, and so do a blank name and a missing
  geometry.
- **Copying a festival copies the areas that are not hidden**, with their tables that are not hidden
  (new ids, same names and geometry), and the shape of every station that sits in a copied area.
  Hidden areas and hidden tables stay behind with the old festival.
- **The table plan read** lists every area, hidden ones included, by `SortOrder` then name, each with
  all its tables by name and its station shapes by station sort order.
- **The phone's plan read** answers the running festival only, with the same order but without
  hidden areas and hidden tables. When no festival runs it answers 200 with an empty area list, the
  way the catalog answers an empty value, and a running festival without shown areas answers the
  same. Its answer changes with `ConfigurationChanged` (the plan) and `OrdersChanged` (orders,
  hand-outs, settlements), and both reach the phones.
- **The editor sends Konva's values unchanged.** Each shape is a Konva group carrying `x`, `y`,
  `width`, `height` and `rotation` of the stored geometry, with its rectangle and name inside, and a
  drop or a resize sends the group's values as they are, without rounding. Snapping and keeping the
  shape inside the drawing (inside its width, below its top, at least 20 units on each side) move the
  node itself during the drag (`dragmove`) or the resize (`anchorDragBoundFunc`, `boundBoxFunc`), so
  what is drawn is what is saved. The transformer's scale is multiplied into `width` and `height` on
  `transform` and reset to 1, so the name never stretches. There is no rotate control, but a stored
  rotation is drawn. A refused save puts the node back to the stored geometry, because vue-konva only
  reapplies a value that changed. A shape's listeners go in its group config (`onDragmove` and so
  on), which vue-konva registers on the node: listeners written as `@dragmove` on a vue-konva
  `Group`, whose root is a fragment, make Vue warn on every shape.
- **The drawing is 400 units wide and as long as its lowest shape.** The editor adds 200 units below
  the lowest shape, or more to fill the screen, and scrolls; a new table or station goes below the
  lowest shape. Nothing about the length is stored.
- **The editor never fills in a name and never sends a blank one.** The add buttons wait for a typed
  name, and a renamed area or table left blank shows `areaNameMissing` or `tableNameMissing` instead
  of being sent. Everything else saves the moment it is dropped or a field is left; deleting an area or
  a table asks first. A refusal shows where the admin acted: under the area, under the add field, in
  the shape's block beside the drawing, above the drawing, or under the station. A refusal and a
  refused name belong to their area or shape, never to what is selected: a rename refused after the
  admin has already tapped another table keeps its block, its reason and the typed name on screen.
- **The two colour states are computed on every read and never stored.** Handed out (the fill):
  leave out every order of the table whose items are all handed out, at every one of its stations.
  Of the orders left, none with an item handed out is `None`, at least one handed-out item is
  `Some`, and no order left is `All`. Settled (the border) is the same rule over `SettledAtUtc`;
  an item given away for zero is settled like any other. A table that never had an order is `All`
  for both. Example: order 1 fully handed out, order 2 untouched, order 3 partly handed out is
  `Some`; once order 3 is fully handed out it is `None`; once order 2 is too it is `All`; a new
  order makes it `None` again.
- **The phone draws the plan read-only, as large as the screen.** The stage is the size of the
  frame under the header (`App.vue` gives the home screen the full height), and `stage.scale` and
  `stage.position` are the camera, starting at the width of the screen over
  `TABLE_PLAN_DRAWING_WIDTH`. The waiter pans by dragging the stage, held inside the drawing by
  `clampedPosition` as its `dragBoundFunc`, and zooms by pinching, following Konva's Multi-touch Scale
  Stage demo: `Konva.hitOnDragEnabled` is set when the plan mounts, and the stage's drag stops while
  two fingers touch and starts again when one is left. Konva blocks native scrolling on the canvas,
  so the plan is panned, never page-scrolled.
- **A tap opens a table, a pan never does.** Each table is a group at its stored geometry, rotation
  included, with `id` `table-<tableId>`, name `plan-table` and its name inside; a station is a group
  named `plan-station` that does not listen, so a tap on it opens nothing and a press on it still
  pans. The stage starts a pan only after 8 pixels, and Konva cancels the click once a pan has
  started, so a waiter who lets go over the table they started on opens nothing. Konva cancels a tap
  per finger, so a gesture in which two fingers touched is marked a pinch and opens no table, and the
  next gesture's first finger clears that mark and the stopped pan.
- **The fill shows `handOutProgress` and the border shows `settlementProgress`**: none red, some
  amber, all green, with no legend. The words are one tap away, on the settle view.
- **Orders from before the plan keep their table.** `SwitchOrdersToTables` gives every festival
  with orders one hidden area "Bisherige Tische" holding one hidden table per table name its orders
  used, at one default position and size. Names that differ only in capitals (SQLite `NOCASE`)
  share a table under the spelling of the earliest order. Every order then points at its table and
  the old name column is dropped; festivals without orders get nothing. The migration creates a
  unique index on the old `Tables` before EF rebuilds both tables, because SQLite checks the
  rebuilt `Orders` foreign key against the old table and refuses it otherwise.

## Tests

- `GastronomyApp.Core.Tests/Services`: `AreaAdministrationServiceTest`,
  `TableAdministrationServiceTest` (including the order-dependent hide and delete rules),
  `FestivalStationShapeServiceTest`, `AreaShapeServiceTest`, the station shape cases of
  `FestivalStationServiceTest`, and the table cases of `OrderAcceptanceServiceTest`;
  `TableServiceTest` for the two colour states (the example above, the settled side, a table
  without orders), the `ShareOfFlaggedItems` cases of `OrderServiceTest`, and `TablePlanServiceTest`
  for the running festival.
- `GastronomyApp.Infrastructure.Tests`: `Configurations/AreaConfigurationTest`,
  `TableConfigurationTest`, `FestivalStationShapeConfigurationTest` and `OrderConfigurationTest`
  (the unique names, the database refusing a festival mix-up and the deletion of a table with an
  order, doubles read back unchanged); `Repositories/TablePlanRepositoryTest` (including the
  phone's read: hidden areas and tables left out, another festival's areas left out, the flags of
  each order's items, an order at two stations beside two station shapes in one area answered once
  each, and the whole read in one statement, counted by `TestSupport/QueryCounter`); the plan
  copy in
  `FestivalRepositoryTest`; `Persistence/ReleasedDatabaseUpgradeTest` (festival station rows survive
  the rebuild of their table, and old orders at two festivals end up at hidden tables of their own
  festival). `TestSupport/DomainSeeder.SeedTableAsync` gives a test a table to order at.
- `GastronomyApp.Api.Tests`: `Endpoints/AdminFestivalTablePlanEndpointsTest` for every route and
  the order-dependent refusals; `Endpoints/TablePlanEndpointsTest` for the phone's route (the shown
  plan, the two states on the wire including an order given away for zero, no running festival, a
  station tablet and an unknown device refused); the plan copy and the station removal in
  `AdminFestivalEndpointsTest`; one hub test per entity type in
  `Announcers/ConfigurationChangedAnnouncementTest`. The seeded world (`ApiSeeder`) holds the table
  "Tisch 12" in a hidden area; `OrderTestContext.SeedTableAsync` adds another by name.
- `frontend/tests`: `admin/core/tablePlanSnapping.spec.ts`, `tablePlanPlacement.spec.ts`,
  `areaOrder.spec.ts`, `tablePlanSelection.spec.ts`; `shared/core/tablePlanDrawing.spec.ts`,
  `tablePlanColours.spec.ts` and `themeColours.spec.ts`; the store in
  `admin/stores/tablePlan.loading.spec.ts` (the gate, `ConfigurationChanged`, the refetch),
  `tablePlan.areas.spec.ts` and `tablePlan.tables.spec.ts` on `tablePlanFixture.ts`; the card in
  `admin/components/festivals/FestivalPage.tablePlan.spec.ts`; the editor in
  `admin/components/tablePlan/TablePlanEditor.drawing.spec.ts` (Konva on `canvas`: drag, snap, guide
  lines, a drawing shown at twice the size, scrolling a selected shape into view, refusal, theme
  switch), `.resizing` (the 20-unit and width limits, a refused resize, a station shape), `.areas`,
  `.tables` (a refused rename the admin walked away from), `.stations` and `.page.spec.ts` on
  `tablePlanEditorFixture.ts`; `shared/composables/useThemedValue.spec.ts`; the address in `shared/router/adminRoute.spec.ts` and
  `admin/views/AdminShell.spec.ts`. The how-to page `test-a-konva-drawing.md` describes the drawing
  helpers.
- The phone: `shared/core/landing.spec.ts` (`waiterLandingFor`: one area, several, an opened area,
  none, no festival, the locked order), `shared/core/statusTints.spec.ts`, `phone/core/tablePlanCamera.spec.ts` (fit, clamped pan, pinch limits),
  `phone/stores/tablePlan.spec.ts` (the read, a failed read keeping the plan, the gate, the reloads
  on `GroupsJoined`, `OrdersChanged` and `ConfigurationChanged`), and on `tablePlanFixture.ts` in
  `phone/views`: `TablePlan.landing.spec.ts` (the landings, no festival, an area without tables,
  the failed load, the locked order leading to the summary), `TablePlan.touch.spec.ts` (a tap, a
  one-finger pan, a pinch that zooms and opens nothing, the tap after a pinch), `TablePlan.drawing.spec.ts` (geometry, names, stations not tappable, the
  fit, fill and border per state in both themes, a tap, a pan that opens nothing, the clamped pan)
  and `TablePlan.sheet.spec.ts` (each button, the question about unsent items in both choices);
  `phone/components/header/AppHeader.spec.ts` for the back control and the names.
