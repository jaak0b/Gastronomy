# Table plan

A festival has areas ("Bereich" / "Area" on screen: a hall, a tent, the garden). Each area holds its
tables and the shapes of the festival's stations placed in it, each drawn as a rectangle the admin
can move, resize and rotate. So far only the laptop has this: the admin routes and the data behind
them. The admin editor (Konva) and the waiter's plan on the phone come later, and orders do not use
tables yet. In code the whole thing is the table plan, never a layout.

## Where the code lives

Backend, under `backend/`:

- `GastronomyApp.Core/Entities/Area.cs`: `FestivalId`, `Name`, `SortOrder` (the order of the area
  buttons on the phone), `IsHidden`, with `Tables` and `StationShapes`.
- `AreaShape.cs` is the abstract base of whatever is drawn in an area: `FestivalId`, `AreaId`, `X`,
  `Y`, `Width`, `Height`, `Rotation` and the `Festival` and `Area` navigations. It is not an EF entity
  type: no inheritance mapping, no discriminator, no table of its own. Each derived class has its own
  table carrying these columns.
- `Table.cs` (`Id`, `Name`, `IsHidden`) and `FestivalStationShape.cs` (`FestivalStationId`, which is
  both its key and its link to `FestivalStation`; a row means the station is on the plan) derive from
  it. All three entity types raise `ConfigurationChanged`.
- `Core/Services/AreaShapeService.cs` is the only code that writes the five geometry values
  (`Reshape`) and reads them back for a copy (`GeometryOf`). The table service, the station shape
  service and the festival copy all go through it.
- Configurations in `Infrastructure/Configurations`: `AreaConfiguration`, `TableConfiguration`,
  `FestivalStationShapeConfiguration`, and the alternate key `(Id, FestivalId)` in
  `FestivalStationConfiguration`. Migration `AddTablePlan` (tables `Areas`, `Tables`,
  `FestivalStationShapes`).
- Services: `AreaAdministrationService` (`ListTablePlanAsync`, create, update, hide, show, delete),
  `TableAdministrationService` (create, update including a move to another area, hide, show, delete)
  and `FestivalStationShapeService` (place or move, remove). Each takes its request record. Port:
  `ITablePlanRepository` (`TablePlanRepository`) owns every read and write of areas, tables and
  station shapes.
- `FestivalStationService.RemoveAsync` deletes the station's shape together with its link through
  `ITablePlanRepository`, and `FestivalRepository.CopyContentsAsync` copies the plan (see the
  invariants).
- Refusals: `GastronomyApp.Core/Refusals/Refusal.TablePlan.cs`.
- Routes in `GastronomyApp.Api/Endpoints/AdminFestivalEndpoints.cs`, under
  `/api/admin/festivals/{festivalId}`: `GET /table-plan`, `POST /areas`, `PUT /areas/{areaId}`,
  `POST /areas/{areaId}/hide` and `/show`, `DELETE /areas/{areaId}` (`AdminAreaHandler`);
  `POST /tables`, `PUT /tables/{tableId}`, `POST /tables/{tableId}/hide` and `/show`,
  `DELETE /tables/{tableId}` (`AdminTableHandler`); `PUT` and `DELETE /stations/{stationId}/shape`
  (`AdminFestivalStationShapeHandler`, keyed by the station id, not the link id).
- Wire records in `GastronomyApp.Contracts/Admin/Festivals`: `AreaShapeGeometry` (`X`, `Y`, `Width`,
  `Height`, `Rotation`, nested as `Geometry` in every request and view that carries a shape, so the
  frontend hands it straight to Konva), `SaveAreaRequest`, `SaveTableRequest` (names the area, so the
  same record creates and moves a table), `SaveStationShapeRequest`, `SavedAreaView`,
  `SavedTableView`, `AdminTablePlanView`, `AdminAreaView`, `AdminTableView`, `AdminStationShapeView`
  (`StationName`); a station shape answers with `SavedStationView`. Mapping:
  `GastronomyApp.Api/Mapping/AdminTablePlanMapping.cs`.
- Texts: `errors.admin.festivals.areaNameTaken`, `tableNameTaken`, `areaStaysWhileTheFestivalRuns`
  and `tableStaysWhileTheFestivalRuns` in `frontend/src/shared/i18n/de.json` and `en.json`.

## Invariants

- **Geometry values are Konva's own and are stored exactly as received**, as doubles: no rounding,
  no normalising, no range check. `X`, `Y` is the top-left corner before rotation for a table and a
  station alike; `Rotation` is degrees clockwise about that corner and may be negative.
- **SQLite itself refuses a festival mix-up.** A table's and a station shape's `(AreaId, FestivalId)`
  point at the area's alternate key `(Id, FestivalId)`, and a station shape's
  `(FestivalStationId, FestivalId)` at the festival station's. Every relation is `Restrict`.
- **Names are unique per festival without regard to case, hidden rows included**, for areas and for
  tables (collation `NOCASE`, unique index on `(FestivalId, Name)`). The service checks first and
  refuses with `areaNameTaken` or `tableNameTaken`.
- **Nothing is deleted while the festival runs**, neither an area nor a table; hiding, showing and
  every other change stay allowed. "Running" comes from `RunningFestivalLookup`.
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

## Tests

- `GastronomyApp.Core.Tests/Services`: `AreaAdministrationServiceTest`,
  `TableAdministrationServiceTest`, `FestivalStationShapeServiceTest`, `AreaShapeServiceTest`, and
  the station shape cases of `FestivalStationServiceTest`.
- `GastronomyApp.Infrastructure.Tests`: `Configurations/AreaConfigurationTest`,
  `TableConfigurationTest` and `FestivalStationShapeConfigurationTest` (the unique names, the
  database refusing a festival mix-up, doubles read back unchanged);
  `Repositories/TablePlanRepositoryTest`; the plan copy in `FestivalRepositoryTest`;
  `Persistence/ReleasedDatabaseUpgradeTest` (festival station rows survive the rebuild of their table).
- `GastronomyApp.Api.Tests`: `Endpoints/AdminFestivalTablePlanEndpointsTest` for every route; the
  plan copy and the station removal in `AdminFestivalEndpointsTest`; one hub test per entity type in
  `Announcers/ConfigurationChangedAnnouncementTest`.
