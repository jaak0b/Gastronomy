# Table plan

A festival has areas ("Tischplan" / "table plan" on screen, in every sentence on the laptop and the
phone: a hall, a tent, the garden). Each area holds its
tables and the shapes of the festival's stations placed in it, each drawn as a rectangle the admin
can move and resize. Every order belongs to one table of the plan. A waiter's phone reads the
running festival's plan, where each table shows by its fill how far its orders are handed out and by
its border how far they are settled. The admin keeps the areas on the "Tischplan" card of the
festival page and draws each area on a page of its own. Areas and tables are never deleted, only
hidden and shown again; a station's shape is the one thing that is deleted. The phone opens on the plan: the list of areas, or the
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
  station is on the plan, and the key keeps a station in at most one area) derive from it. All three entity types raise `ConfigurationChanged`.
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
- Services: `AreaAdministrationService` (`ListTablePlanAsync`, create, update, move, hide, show;
  a new area is numbered after the festival's last area, hidden ones included, through
  `Core/Services/SortOrdering.cs` (`NextSortOrder`), the way categories are numbered, and editing
  an area never changes its place; a move goes through `SortOrdering.Move` over
  `FindAreasInOrderAsync` and swaps the area with the next one in that direction that
  `MoveAreaRequest.ShowsHiddenAreas` says the admin can see),
  `TableAdministrationService` (create, update including a move to another area, hide, show, and
  copy a selection of tables into one area and move a selection in one transaction) and
  `FestivalStationShapeService` (place or move,
  remove). `Core/Services/TableCopyNaming.cs` is the one home of a copy's name: it counts up the
  ASCII digit run that ends the source name, keeping the text before it, the source's case and its
  digit count (`A1` to `A2`, `A09` to `A10`, `A99` to `A100`), appends ` 1`, ` 2`, ... to a name
  without a trailing digit, and takes the first free name per festival, hidden tables included,
  ignoring case. It processes one paste in ascending source name (`OrdinalIgnoreCase`, then id),
  uses `BigInteger` so a long digit run cannot overflow, and never truncates or normalises the
  source name. Each takes its request record. Port:
  `ITablePlanRepository` (`TablePlanRepository`) owns every read and write of areas, tables and
  station shapes, and answers the order questions the hide rules ask
  (`HasItemsToHandOutOrSettleAtTableAsync`, `HasItemsToHandOutOrSettleInAreaAsync`). Its read for the
  copy and the move is `FindTablesAsync` (the requested tables of one festival, tracked because the
  move updates them), and the copy also reads `FindTableNamesAsync`
  (every name of the festival, hidden rows included). The admin's
  read, `FindTablePlanAsync` (with `countsItemsToHandOutOrSettle`), answers `ListedArea` and
  `ListedTable` (`Core/Results`): the entity and `HasItemsToHandOutOrSettleAtTheRunningFestival`,
  false unless the read was told to count, computed in the same single statement with
  `WhereNotHandedOutOrNotSettled()` from `Infrastructure/Persistence/OrderItemQueriesExtensions.cs`,
  the filter this read and the hide rules share for an item not yet handed out or not yet settled, so the editor can disable hiding before the click.
  Whether the items count is decided only in `Core/Services/ItemsToHandOutLookup.cs`, the one home of
  "only while the festival runs" that the stations use too (see the `festivals` page):
  `ListTablePlanAsync` reads the plan through its `FindTablePlanAsync(festival)`, and the two hide
  rules ask `HasItemsToHandOutOrSettleAtTableIfRunningAsync` and
  `HasItemsToHandOutOrSettleInAreaIfRunningAsync`, so the flag on the wire and the refusal always agree. `OrderAcceptanceService` looks the order's table up through
  `FindTableAsync`; the open items screen reads one table with its orders through
  `IOpenItemRepository.FindTableWithOrdersAsync` (see the `settling-a-table` page).
- The phone's plan: `TablePlanService.ReadRunningFestivalTablePlanAsync` asks `RunningFestivalLookup`
  for the running festival and reads its plan through
  `ITablePlanRepository.FindShownAreasWithItemFlagsAsync`. That is one SQL statement, so one
  snapshot, which projects into the records in `Core/Results`: `ShownArea` (the `Area` with its
  `Tables` and `StationShapes`), `ShownTable` (the `Table` and `ItemFlagsPerOrder`, one list of
  `OrderItemFlags` (`IsHandedOut`, `IsSettled`) per order, over all its stations) and
  `NamedStationShape` (the `FestivalStationShape` with its `StationId` and `StationName`, which the
  admin's `ListedArea` carries too). No order or item entity
  is loaded, because every phone reads this again on every `OrdersChanged`.
  `Core/Services/TableService.cs` derives a table's two colour states, `HandOutProgressOf` and
  `SettlementProgressOf`, each through `OrderService.ShareOfFlaggedItems`, the one function that
  turns item flags into `None`, `Some` or `All` and that `OrderService.StatusOf` uses as well.
- `FestivalStationService.RemoveAsync` deletes the station's shape together with its link through
  `ITablePlanRepository`, and `FestivalService.CopyAsync` copies the plan (see the
  invariants).
- Refusals: `GastronomyApp.Core/Refusals/Refusal.TablePlan.cs`.
- Routes in `GastronomyApp.Api/Endpoints/AdminFestivalEndpoints.cs`, under
  `/api/admin/festivals/{festivalId}`: `GET /table-plan`, `POST /areas`, `PUT /areas/{areaId}`,
  `POST /areas/{areaId}/move` (body `MoveAreaRequest`: `direction` `up` or `down`, and
  `showsHiddenAreas`),
  `POST /areas/{areaId}/hide` and `/show` (`AdminAreaHandler`);
  `POST /tables`, `PUT /tables/{tableId}`, `POST /tables/{tableId}/hide` and `/show`
  (`AdminTableHandler`), `POST /tables/copy`, which creates every copy of one paste in the one
  request transaction and answers `CopiedTablesView` with the new table ids, and
  `POST /tables/move`, which moves one or more tables in one transaction and answers no content; `PUT` and
  `DELETE /stations/{stationId}/shape`
  (`AdminFestivalStationShapeHandler`, keyed by the station id, not the link id). The phone's
  `GET /api/table-plan` lives in `GastronomyApp.Api/Endpoints/TablePlanEndpoints.cs`
  (`TablePlanHandler`), for staff phones only, like the catalog and open items routes.
- Wire records: `AreaShapeGeometry` in `GastronomyApp.Contracts/TablePlan` (`X`, `Y`, `Width`,
  `Height`, `Rotation`, nested as `Geometry` in every admin and phone request and view that carries
  a shape, so the frontend hands it straight to Konva). In `GastronomyApp.Contracts/Admin/Festivals`:
  `SaveAreaRequest` (the name only), `MoveAreaRequest` (a `MoveDirection` from `Contracts/Enums`,
  shared with `MoveCategoryRequest`, and `ShowsHiddenAreas`), `SaveTableRequest` (names the area, so the
  same record creates a table and changes a table's name or area in the dialog; the drawing moves
  geometry through `MoveTablesRequest` instead), `SaveStationShapeRequest`, `SavedAreaView`,
  `SavedTableView`, `MoveTablesRequest` (one `MoveTableRequest` per table, each a table id and its
  `Geometry`, with no name and no area), `CopyTablesRequest` (the target area and one `CopyTableRequest` per table, each
  a source table id and its `Geometry`), `CopiedTablesView` (the new `TableIds`), `AdminTablePlanView`, `AdminAreaView` (no sort order: the list comes in the
  laptop's order) and `AdminTableView` (each with `HasItemsToHandOutOrSettleAtTheRunningFestival`), `AdminStationShapeView` (`StationName`); a station shape answers
  with `SavedStationView`. Mapping: `GastronomyApp.Api/Mapping/AdminTablePlanMapping.cs`, from the
  `Listed*` records and `NamedStationShape`. The phone's records are in
  `GastronomyApp.Contracts/TablePlan`: `TablePlanView` (`Areas`), `TablePlanAreaView` (`AreaId`,
  `Name`, `Tables`, `StationShapes`), `TablePlanTableView` (`TableId`, `Name`, `Geometry`,
  `HandOutProgress`, `SettlementProgress`) and `TablePlanStationShapeView` (`StationName`,
  `Geometry`); both states are a `ShareState` (`Contracts/Enums`: `None`, `Some`, `All`, on the
  wire `none`, `some`, `all`, the same word the phone's open items code uses). Mapping:
  `GastronomyApp.Api/Mapping/TablePlanMapping.cs`, from the `Shown*` records and `NamedStationShape`, which calls
  `TableService` for the two states.
- Texts: `errors.admin.festivals.areaNameTaken`, `tableNameTaken`,
  `areaWithItemsToHandOutOrSettleStaysShown`, `tableWithItemsToHandOutOrSettleStaysShown` and
  `tableCopyNameTooLong` in
  `frontend/src/shared/i18n/de.json` and `en.json`.

Frontend, under `frontend/src/`:

- `admin/components/festivals/FestivalTablePlan.vue` is the "Tischplan" card of the festival page.
  Its area rows follow the festival page's station card and are drawn by the shared
  `admin/components/HideableEntryList.vue` (see the `hideable-rows` how-to page). The heading line
  carries "Tischplan" and, at its right, "Ausgeblendete anzeigen", placed like "Deaktivierte
  anzeigen" on the Artikel tab. A row has the name as text, the number of shown tables (one
  computed text per area), then "Tischplan öffnen", the up and down arrows of the category rows
  (`BaseMoveButtons.vue`), which move the area the moment they are pressed, past the next area on
  screen: while "Ausgeblendete anzeigen" is unticked a hidden area is passed over and keeps its
  place, and while it is ticked every area counts. Then the pencil (`BaseEditButton.vue`) opening
  `tablePlan/TablePlanNameDialog.vue` (the name only, at most 40 characters, a `BaseFormDialog`
  like `StationDialog.vue`, shared with the tables), then the trash icon that hides, or the open eye
  "Einblenden" (`BaseShowButton.vue`) on a hidden row. "Neuer Tischplan" sits at the right below
  the rows and opens the same dialog, empty; the dialog sends the name only, and the laptop puts
  the new area after the last one, hidden ones included. The trash icon is disabled with the reason as a tooltip while the
  festival runs and a table of the area has items to hand out or settle. A taken name shows in the
  dialog. The warnings appear under the heading only once the plan of this festival has arrived,
  each a `BaseWarningText.vue` like the stock card's, and count only what waiters see: no area at
  all (`noArea`, naming "Neuer Tischplan"), only hidden areas (`allAreasHiddenAndNotListed`, naming
  the checkbox, while it is unticked; `allAreasHidden` once it is ticked, which `HideableEntryList`
  hands its `notices` slot as `showsHidden`), or shown areas without a shown table (`noTable`,
  naming "Tischplan öffnen").
- "Tischplan öffnen" opens one area's editor on its own page at `/admin/festivals/{festivalId}/areas/{areaId}`:
  `shared/router/route.ts` reads it as the admin route with `areaId`, and `admin/views/AdminShell.vue`
  chooses `admin/components/tablePlan/TablePlanEditor.vue` (`festivalId`, `areaId`) and lets it fill
  the screen height. The page has a back link to the festival page and the area's name as its
  heading. An unknown festival returns to the overview through
  `admin/composables/useOpenedFestival.ts`, the guard the festival page uses too, which decides only
  once the festival list has arrived (`hasLoaded` of `admin/stores/adminList.ts`); an unknown area
  returns to the festival page, decided only once the store holds this festival's plan
  (`loadedFestivalId`), so a read overtaken by a newer one never sends the admin away. The editor
  owns the selection (`TablePlanSelection` in `admin/core/tablePlanSelection.ts`): any number of
  tables, or one station, never both. Ctrl-click and Shift-click toggle a table into or out of a
  table selection, a plain click selects just the table under the pointer, and a station is always
  selected alone. A press on empty space becomes a marquee once it passes the editor's 3-pixel drag
  threshold: the dashed `marquee` rectangle with the translucent `marquee-fill` selects every table
  its box touches, a plain marquee replaces the selection and a Ctrl or Shift marquee adds to it. A
  press shorter than the threshold is the plain click that clears the selection. The page is one
  block centred in the window, at most 1100 pixels wide like the festival page:
  `TablePlanDrawing.vue` (the Konva drawing) with the lists to its right, at most a form column
  wide, or below it in a window too narrow for both. `TablePlanTableList.vue` draws the table rows
  with the same `HideableEntryList.vue` as the card: the heading line "Tische" with "Ausgeblendete
  anzeigen", the name as text, the pencil opening `TablePlanNameDialog.vue`, the hiding trash icon
  (disabled with the reason while the festival runs and the table has items to hand out or settle)
  or the open eye "Einblenden", and "Neuer Tisch" at the right (the table goes below the lowest
  table of the area, hidden ones included, and below the station shapes, and is selected).
  `TablePlanStationList.vue` draws its rows with `admin/components/stations/RemovableStationRows.vue`,
  the rows of the festival page's station card: one row per station on this plan with the trash
  icon, which asks first (`BaseRemoveConfirmDialog`) and deletes its shape, and below them the shared
  `admin/components/BaseSearchAddLine.vue` offering only the festival's stations on no plan. With
  nothing to offer it says either that every station of the festival is in an area
  (`noStationsToPlace`) or that the festival has no station yet (`festivalHasNoStations`).
  Tapping a shape in the drawing highlights its row and scrolls the list to it, a table row and a
  station row alike (`BaseListRow.vue`). The resize handles appear only for exactly one selected
  shape; a table selection of several shows its tables with the primary border and no handles.
- Copy and paste live in the drawing. Ctrl+C copies the selected tables (a station is never
  copied), Ctrl+V pastes them, and the context menu opened by a right click offers "Kopieren" on a
  table and "Einfügen" on an empty spot. A right click keeps the current table selection when the
  clicked table is already in it and selects just that table otherwise; it never copies by itself.
  "Kopieren" copies whatever is selected then, so a selected table copies the whole selection and an
  unselected one copies just itself. A station shows no menu. The browsers' own menu is suppressed only over the drawing. The keyboard shortcuts
  are ignored from an input, textarea, select or contenteditable, and while a dialog is open;
  the browser default is prevented only when the app handles the key. The listener is added while
  the editor is mounted and removed when it closes. A paste reloads the plan, selects every new
  table and scrolls the first copy into view; a refusal shows above the drawing, where a move or a
  resize refusal appears. A group drag moves every selected table by the same delta and snaps the
  dragged table against the shapes outside the selection. A single drag, a resize and a group drag
  all send one `POST /tables/move` with every changed geometry, so one commit stores the whole move
  and one reload reads the answer; a refusal puts every node back to its pre-drag geometry and shows
  the refusal, and a half-moved group can never exist.
- `admin/core/tablePlanPlacement.ts` holds `drawnTablesOf`, the one definition of the tables the
  editor draws of an area: its shown tables, never a hidden one. `isDrawnIn` in
  `tablePlanSelection.ts` uses it, so a selected table that gets hidden loses its selection.
  `tableIdsInMarquee` selects the tables whose corners meet the marquee. `geometryForANewShapeIn`
  is the one placement of a new table and a new station alike: below every
  table of the area, hidden ones included, and every station shape. `pastedCopiesBeside` and
  `pastedCopiesAt` place a copied group: in the same area one `SNAP_GRID` step (10 units) right and
  down, in another area at the coordinates the originals have, and from the menu with the group's
  top-left bounding-box corner at the clicked drawing point. All three shift the whole group left
  when it would cross the 400-unit drawing width and never shift its left edge below zero.
- `admin/stores/tablePlan.ts` (`useAdminTablePlanStore`) holds the festival's `AdminAreaView` list
  and `loadedFestivalId` (the festival whose plan that list is, set when a read is applied), and has
  one action per admin route; every action reloads the plan through the latest request gate, and the
  store reloads on `ConfigurationChanged` (the plan) and `OrdersChanged` (the flags that disable
  hiding). `moveArea` sends a `MoveAreaRequest` carrying the state of "Ausgeblendete anzeigen",
  which `HideableEntryList.vue` hands to `moveThrough`, one move after the other through
  `admin/core/oneAtATime.ts`, as the category store does. The clipboard lives only in this store's
  memory: the source festival id, the source area id, and per table its source id and a geometry
  snapshot. It survives switching areas inside one festival, clears when a different festival loads,
  and never touches local storage. `copyTables` and `pasteInto` own it;
  `pasteInto` sends `POST /tables/copy`, reloads the plan through the existing helper and returns
  the new table ids or the refusal. `moveTables` sends one `POST /tables/move` for a single drag, a
  resize or a whole group and reloads the plan once through the same helper.
- `shared/core/tablePlanDrawing.ts` holds `TABLE_PLAN_DRAWING_WIDTH` (400 units, for the phone as
  well), `cornersOf` and `lowestEdgeOf`. `admin/core/tablePlanSnapping.ts` holds the snapping
  geometry adapted from Konva's Objects Snapping demo (`snapStopsBeside`, `snapDraggedBox`,
  `snapResizeCorner`, a grid of 10 units and a snap distance of 6), and
  `admin/core/tablePlanPlacement.ts` where a new shape goes, where a copied group is pasted, the
  smallest size, the room below the lowest shape and `fitsTheDrawing`.
- Colours: `shared/core/tablePlanColours.ts` builds the light and dark palette from tokens of
  `shared/theme.ts` through `shared/core/themeColours.ts`, and the drawing picks the one of the
  current theme with `shared/composables/useThemedValue.ts`, as the charts do. In the editor a table
  is `surface-light` with an `on-surface` border and a station is inverted (`primary` with
  `on-primary` lettering).
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
  `AreaPlan.vue` (the read-only Konva drawing of one area with its scroll bars), `TableActionSheet.vue`
  (the table's name, "Bestellung aufnehmen", "Abrechnen", "Zurück") and `UnsentOrderDialog.vue` (the
  question when another table's unsent items are on the phone; see the `taking-an-order` page). `phone/components/header/AppHeader.vue` carries
  the back control ("Tischpläne" from a plan when there are several areas, "Tischplan" from the
  ordering screen and the settle view) and the area or table name.
- `phone/core/tablePlanCamera.ts` is the camera of the phone's drawing: `cameraFittingTheWidth`,
  `clampedPosition`, `pinchOf`, `pinchedCamera` (zoom about the point between the fingers, from the
  width of the screen up to `DEEPEST_ZOOM`, four times that) and `waiterDrawingLength` (the lowest
  shape plus `ROOM_BELOW_THE_LOWEST_SHAPE`). Beside it, `phone/core/tablePlanScrollBars.ts`
  (`scrollBarsOf`: each bar's offset and length from the camera, the screen and the drawing's length)
  and `shared/core/shapeNameFit.ts` (`nameFontSizeKeepingWordsWhole`, the font size of the name
  inside a shape: a table's in the phone's drawing and the editor's, a station's in the phone's).
- `phone/stores/tablePlan.ts` (`useTablePlanStore`: `areas`, `hasLoaded`, `loadFailed`,
  `openedAreaId`, `load`, `listen`, `openArea`, `closeArea`) reads `GET /api/table-plan` through the
  latest request gate and reloads on `ConfigurationChanged` (the plan) and `OrdersChanged` (the
  colours); `App.vue` starts its listeners and its first load with the catalog's, and the connection's
  refetch on `GroupsJoined` reloads it.
- Texts: `phone.tablePlan.*`.
- Texts: `admin.tablePlan.*` (the dialog's `areaName`, `tableName`, `newArea`, `editArea`,
  `newTable`, `editTable`, `openPlan`, the menu's `copy`, `paste`, the hide questions `hideAreaTitle`, `hideTableTitle` with
  `hideAreaBody`, `hideTableBody`, the removal body `removeStationBody`, the warnings `noArea`,
  `allAreasHidden`, `allAreasHiddenAndNotListed`, `noTable`, `noStationsToPlace` and
  `festivalHasNoStations`), and the shared "Einblenden", "Ausgeblendet", "Ausgeblendete anzeigen"
  (`admin.common.actions.showHidden`), "Bearbeiten", "Ausgabestelle entfernen" and "Ausgabestelle
  hinzufügen" from `admin.festivals.*`, `admin.common.*`. The two hide refusals double as the
  tooltips of the disabled trash icons. On screen an area is always "Tischplan" (der Tischplan, des
  Tischplans, die Tischpläne) and "table plan" in English; code, keys and the database keep "area".

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
- **Areas and tables are never deleted, only hidden and shown again**, like festivals. A hidden one
  keeps its name, so the name stays taken. A station's shape is the one thing that is deleted: taking
  the station off the plan (`DELETE /stations/{stationId}/shape`) or off the festival.
- **Hiding waits for the table to be finished while the festival runs.** A table with an item not
  yet handed out or not yet settled is not hidden, nor is an area one of whose tables has such an
  item (`tableWithItemsToHandOutOrSettleStaysShown`, `areaWithItemsToHandOutOrSettleStaysShown`).
  `ItemsToHandOutLookup` decides it, and the table plan read's flag says the same, so the screens
  read the flag alone and never the festival's own running state. After the festival, before it,
  and for a row that is hidden already, hiding always succeeds. Showing and every other change stay
  allowed.
- **A hidden table still takes orders.** The phone only offers the plan's tables, so placing an
  order checks only that the table belongs to the running festival.
- **Taking a station off a festival deletes its shape through the tracker** together with its link,
  so the restricting key never fires.
- **An id that does not belong to the festival is a broken program.** An unknown area, table,
  station or station shape in the route answers a bare 404; an area of another festival named in a
  body answers 400 with `RefusalMessageKeys.AdminActionFailed`, and so do a blank name and a missing
  geometry.
- **A paste is one request and one transaction, and the laptop names every copy.** `POST
  /tables/copy` carries the target area and, per copy, the source table id and the geometry it takes.
  The laptop creates every copy in the target area of the same festival as new shown tables, keeps
  size and rotation, and never copies a station. All of them are added and saved once, so a paste
  lands whole or not at all, and the answer carries the new table ids. The frontend never computes
  or proposes a copy name. The copies are new `Table` rows, so the one commit sends
  `ConfigurationChanged` once and no service sends it. An unknown or foreign source table answers the existing `TableNotFound`;
  an empty or duplicate source list is a broken-program guard that answers
  `errors.admin.actionFailed` and names the exact reason in the developer message the screen never
  sends.
- **A copy's name counts up the source name's ending.** `TableCopyNaming` keeps the text before a
  trailing ASCII digit run and that run's digit count, and counts the number up (`A1` to `A2`, `A09`
  to `A10`, `A99` to `A100`); a source without a trailing digit gets ` 1`, then ` 2`, `...`
  appended. It takes the first free name per festival, hidden tables included, ignoring case (at
  least as strict as SQLite's `NOCASE`), never truncates or normalises the source name, and
  processes one paste in ascending source name (`OrdinalIgnoreCase`, then id). A generated name
  longer than the 40 characters the editor's name field allows refuses the whole paste as
  `tableCopyNameTooLong` and creates nothing; the 40-character editor cap itself is unchanged and no
  general backend or database name-length rule exists.
- **The clipboard is in memory only.** `useAdminTablePlanStore` keeps the source festival id, the
  source area id, and per table the source id and a geometry snapshot. It survives switching areas
  inside one festival, clears when a different festival loads, and never touches local storage. A
  copied station is nothing.
- **A table move is one request and one transaction.** `POST /tables/move` carries only each table
  id and the geometry it takes, never a name or an area. The laptop loads the tables tracked, applies
  every geometry through `AreaShapeService` and saves once, so every table moves or none does; an
  unknown or foreign table answers `TableNotFound`, and an empty or duplicate list is the
  broken-program guard that names the exact reason. The table dialog keeps `PUT /tables/{tableId}`
  for a name or area change. The dragged table
  snaps against every shape outside the selection and the other selected tables follow by the same
  delta. A single drag, a resize and a group drag each send one move request and cause one plan
  reload. On a refusal every node goes back to its pre-drag geometry and the refusal shows, so a
  half-moved group is never left looking saved.
- **Copying a festival copies the areas that are not hidden**, with their tables that are not hidden
  (new ids, same names and geometry), and the shape of every station that sits in a copied area.
  Hidden areas and hidden tables stay behind with the old festival.
- **The table plan read** lists every area, hidden ones included, by `SortOrder` then name, each with
  all its tables by name and its station shapes by station sort order, and says per area and per
  table whether it has items to hand out or settle. It is one SQL statement.
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
- **The drawing is 400 units wide and as long as its lowest shape.** The editor shows it at one
  fixed scale of 1.5 (`DRAWING_SCALE` in `TablePlanDrawing.vue`) with a margin of 4 units around it
  (`MARGIN_AROUND_THE_DRAWING`, the layer's offset), 612 pixels wide on every window, so it never
  shrinks when the window narrows and a border at the edge is drawn whole; the transformer's
  bounds take the margin off before they compare. Its visible height follows the window and it
  scrolls inside for long areas. It adds 200 units below the lowest drawn shape, or more to fill its visible
  height, and draws no hidden table. A new table and a new station both go below the lowest table
  of the area, hidden ones included, and below the station shapes, so neither takes the place of a
  hidden table, and showing a table again moves nothing. Nothing about the length is stored.
- **Names of areas and tables are typed in a dialog and never guessed.** One dialog,
  `TablePlanNameDialog.vue`, asks
  for the name of an area or a table, at most 40 characters (the laptop sets no limit; 40 follows
  the station dialog), and cannot be saved while it is blank; a refusal such as a taken name keeps the
  dialog open with the reason. The one name the laptop writes itself is a copy's, through
  `TableCopyNaming`; the 40-character field cap stays as it is. The order of the areas changes only through the arrows, each press
  saved at once, and each press passes over the hidden areas the admin cannot see; a new area goes
  last, numbered by the laptop. A move, a resize and a group move save when they are dropped.
  Hiding and taking a station off the plan ask first, showing asks nothing, and hiding is disabled
  with the reason while the laptop would refuse it. A refusal that comes back anyway shows on its row,
  under the station field, or above the drawing for a move or a resize.
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
- **A tap cancels the click the browser would send behind it.** A phone follows a quick tap with
  mouse events and a click at the same spot, after the sheet has opened, so the click would land on
  the sheet's scrim and close it at once; a long press sends no click. The table's `tap` handler
  calls `preventDefault()` on the native `touchend`, which stops that click. The laptop's mouse opens the table on Konva's `click`, and its native click lands
  on the canvas, never on the scrim.
- **The scroll bars only show where the waiter is.** A thin bar along the right edge and one along
  the bottom, as long as the share of the drawing on screen and placed where that share lies, each
  shown only while more than `PIXELS_OUT_OF_VIEW_BEFORE_A_BAR_SHOWS` (one pixel) of the drawing is out
  of view in its direction, so at the starting zoom only a long area has one. They follow the camera
  during a pan (`dragmove`) and a pinch, are plain elements over the canvas with
  `pointer-events: none`, so a tap near the edge reaches the table, and take `on-surface` at medium
  emphasis from the theme.
- **A table's name, and on the phone a station's name, never breaks inside a word.** The name wraps
  only between words, and when one word is wider than the shape, `nameFontSizeKeepingWordsWhole`
  shrinks the font below its largest size (14 units) until that word fits, rounded down to a tenth.
  The words are measured with a Konva text node in the drawing's own font, made by
  `shared/composables/useShapeNameWordWidth.ts` for both drawings and let go when the drawing
  closes. The editor draws a station's name at the largest size.
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

- `GastronomyApp.Core.Tests/Services`: `AreaAdministrationServiceTest` (the number of a new area,
  an edit that keeps the place, the moves, past a hidden area or onto it), `SortOrderingTest` (a
  move past an entry that does not count),
  `TableAdministrationServiceTest` (including the order-dependent hide rules, the copy of a
  table selection: names, geometry, hidden-table name collisions, a missing or foreign source, a
  target area outside the festival, the 40-character refusal and nothing created when it refuses,
  and the batch move: several tables reshaped and saved once, a missing table, an empty list and a
  duplicate list),
  `TableCopyNamingTest` (every naming example, leading zeros, a name without a trailing digit,
  collisions with shown and hidden names, ASCII case collisions, a paste of several tables in
  deterministic order, a very long digit run),
  `ItemsToHandOutLookupTest` (the plan read counting the items left only while the festival runs,
  and the table and area checks running, over and not yet started),
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
  each, and the whole read in one statement, counted by `TestSupport/QueryCounter`; the admin's read
  with its flags per table and area, no flag when it is told not to count, also one statement; the
  copy reads `FindTableNamesAsync` with hidden names and no other festival's, and `FindTablesAsync`
  with a shown and a hidden table of the festival and another festival's id left out, and as a
  tracked read whose changed geometry is saved); the plan
  copy in
  `FestivalServiceTest`; `Persistence/ReleasedDatabaseUpgradeTest` (festival station rows survive
  the rebuild of their table, and old orders at two festivals end up at hidden tables of their own
  festival). `TestSupport/DomainSeeder.SeedTableAsync` gives a test a table to order at.
- `GastronomyApp.Api.Tests`: `Endpoints/AdminFestivalTablePlanEndpointsTest` for every route
  (a new area numbered after a hidden last one, an edit that keeps the place, an area moved up, past
  a hidden area while hidden areas are not shown and onto it while they are, an area of another
  festival not found) and the order-dependent refusals, with the plan read first and its flag checked
  against the hide answer for a table and an area while the festival runs, before it and after it;
  the copy route (every copy and its ids, a second source missing creates none, the 40-character
  refusal answers 409 and creates none, an area of another festival is refused generically, and the
  empty and duplicate lists answer the generic key); the move route (several tables moved in one
  request, a missing second table moves nothing, and the empty and duplicate lists answer the
  generic key);
  `Endpoints/TablePlanEndpointsTest` for the phone's route (the shown
  plan, the two states on the wire including an order given away for zero, no running festival, a
  station tablet and an unknown device refused); the plan copy and the station removal in
  `AdminFestivalEndpointsTest`; one hub test per entity type in
  `Announcers/ConfigurationChangedAnnouncementTest`, including one paste of two tables and one move
  of two tables heard once.
  The seeded world (`ApiSeeder`) holds the table
  "Tisch 12" in a hidden area; `OrderTestContext.SeedTableAsync` adds another by name.
- `frontend/tests`: `admin/core/tablePlanSnapping.spec.ts`, `tablePlanPlacement.spec.ts` (including
  the paste placements: one grid step in the same area, unchanged coordinates in another, the menu
  anchor, the fit-left rule and never a left edge below zero), `tablePlanSelection.spec.ts` (the
  multi-table selection, the station, the toggle and the marquee intersection), `oneAtATime.spec.ts`;
  `shared/core/tablePlanDrawing.spec.ts`,
  `tablePlanColours.spec.ts` and `themeColours.spec.ts`; the store in
  `admin/stores/tablePlan.loading.spec.ts` (the gate, `ConfigurationChanged`, `OrdersChanged`,
  the refetch), `tablePlan.areas.spec.ts` and `tablePlan.tables.spec.ts` on `tablePlanFixture.ts` (`loadedFestivalId`
  in the loading spec, and one move request plus one reload in the tables spec), and `tablePlan.copying.spec.ts` (the in-memory clipboard and its scope, the
  paste request, the menu placement, the refusal); the card in `admin/components/festivals/FestivalPage.tablePlan.spec.ts` (the
  rows, the count of shown tables, the box for hidden areas in the heading line, the order of a
  row's actions, the pencil and the open eye with their names, "Tischplan öffnen", the trash icon
  disabled with its reason, the warnings with the stock card's look, the tick sentence only while
  the box is unticked, and that they wait for the plan) and `FestivalPage.tablePlanAreas.spec.ts`
  (the name-only dialog for a new area and for editing one, its limit of 40 characters, the name
  alone sent for a new or edited area, the arrows with the state of the box
  for hidden areas, a taken name in the dialog, hiding that asks first, showing) on `festivalTablePlanFixture.ts`; the editor in
  `admin/components/tablePlan/TablePlanEditor.drawing.spec.ts` (Konva on `canvas`: a hidden table
  never drawn, the size kept when the window narrows, a border at the edge drawn whole, a name never
  broken inside a word, the measuring text let go when the editor closes, drag, snap, guide lines,
  selecting, scrolling a selected shape into view, refusal, theme switch, the drawing growing to
  fill a tall window), `.resizing` (the 20-unit and width limits, a refused resize, a station
  shape), `.tables` (the rows, the pencil before the trash icon, the box for hidden tables, the row highlighted and scrolled to, the
  dialog for a new table below the lowest table, a hidden one included, and for editing one, hiding
  that asks first, is disabled with its reason and takes the table out of the drawing, showing that
  moves nothing), `.stations` (the rows on this plan, a row highlighted and scrolled to, a new
  station placed below a hidden table, the add line
  with the stations on no plan and its two sentences when it has none, the trash icon that asks
  first, a station offered again once its shape is gone), `.selection.spec.ts` (Ctrl and Shift
  clicks, the plain click, the marquee with its dashed box and translucent fill, a plain and a Ctrl
  marquee, the short press that clears, the transformer only for one shape, a group drag sending one
  snapped move request and causing one reload with the laptop's answer, and a refused group move
  restoring both tables) and `.copying.spec.ts` (Ctrl+C and Ctrl+V, the context menu
  on a table, an empty spot and a station, a selected table keeping the whole selection and copying
  all of it, the browser menu suppressed only over the drawing, the
  after-paste selection and scroll, the refusal) and `.page.spec.ts` (the heading, the way back, an unknown
  area or festival, a newer read overtaking the first) on `tablePlanEditorFixture.ts`; `shared/composables/useThemedValue.spec.ts`; the address in `shared/router/adminRoute.spec.ts` and
  `admin/views/AdminShell.spec.ts`. The how-to page `test-a-konva-drawing.md` describes the drawing
  helpers.
- The phone: `shared/core/landing.spec.ts` (`waiterLandingFor`: one area, several, an opened area,
  none, no festival, the locked order), `shared/core/statusTints.spec.ts`, `phone/core/tablePlanCamera.spec.ts` (fit, clamped pan, pinch limits),
  `phone/core/tablePlanScrollBars.spec.ts` (fitted with nothing hidden, a long area, zoomed in,
  panned to each end), `shared/core/shapeNameFit.spec.ts`,
  `phone/stores/tablePlan.spec.ts` (the read, a failed read keeping the plan, the gate, the reloads
  on `GroupsJoined`, `OrdersChanged` and `ConfigurationChanged`), and on `tablePlanFixture.ts` in
  `phone/views`: `TablePlan.landing.spec.ts` (the landings, no festival, an area without tables,
  the failed load, the locked order leading to the summary), `TablePlan.touch.spec.ts` (a tap, a
  one-finger pan, a pinch that zooms and opens nothing, the tap after a pinch, the sheet staying open
  after the click a phone sends behind a quick tap and after the laptop's mouse click),
  `TablePlan.scrollBars.spec.ts` (the bars showing and hiding with the camera),
  `TablePlan.drawing.spec.ts` (geometry, names, a name kept whole in a narrow table and in a narrow
  station, stations not tappable, the fit, fill and border per state in both themes, a tap, a pan that opens nothing, the
  clamped pan) and `TablePlan.sheet.spec.ts` (each button, the question about unsent items with its
  three choices);
  `phone/components/header/AppHeader.spec.ts` for the back control and the names.
