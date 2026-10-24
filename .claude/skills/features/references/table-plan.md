# Table plan

A festival has areas ("Bereich" / "Area" on screen: a hall, a tent, the garden). Each area holds its
tables and the shapes of the festival's stations placed in it, each drawn as a rectangle the admin
can move, resize and rotate. Every order belongs to one table of the plan. A waiter's phone reads
the running festival's plan, where each table shows by its fill how far its orders are handed out
and by its border how far they are settled. So far only the laptop has this: the admin routes, the
phone's read route, the data behind them and orders placed by table. The admin editor (Konva) and
the waiter's plan screen come later. In code the whole thing is the table plan, never a layout.

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

## Invariants

- **Geometry values are Konva's own and are stored exactly as received**, as doubles: no rounding,
  no normalising, no range check. `X`, `Y` is the top-left corner before rotation for a table and a
  station alike; `Rotation` is degrees clockwise about that corner and may be negative.
- **SQLite itself refuses a festival mix-up.** A table's and a station shape's `(AreaId, FestivalId)`
  point at the area's alternate key `(Id, FestivalId)`, a station shape's
  `(FestivalStationId, FestivalId)` at the festival station's, and an order's `(TableId, FestivalId)`
  at the table's. Every relation is `Restrict`.
- **An order shows its table's current name**, read through `Order.Table` on the station cards, the
  open items list and the one-table view, so renaming a table renames it on every order.
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
- **The two colour states are computed on every read and never stored.** Handed out (the fill):
  leave out every order of the table whose items are all handed out, at every one of its stations.
  Of the orders left, none with an item handed out is `None`, at least one handed-out item is
  `Some`, and no order left is `All`. Settled (the border) is the same rule over `SettledAtUtc`;
  an item given away for zero is settled like any other. A table that never had an order is `All`
  for both. Example: order 1 fully handed out, order 2 untouched, order 3 partly handed out is
  `Some`; once order 3 is fully handed out it is `None`; once order 2 is too it is `All`; a new
  order makes it `None` again.
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
