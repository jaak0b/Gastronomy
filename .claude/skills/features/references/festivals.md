# Festivals

The admin keeps the festivals on the Feste tab: a name, a start and an end, and on the festival's own
page its stations, its articles with their prices and its ingredient stock. At most one festival is
running at a time, and the phones, the station tablets and the settling screen all work on that one.
Hiding a running festival is refused, and so is taking an article off its menu or a station with open
items off it.

## Where the code lives

Backend, under `backend/`:

- `GastronomyApp.Core/Entities/Festival.cs`: `Name`, `StartsAtUtc`, `EndsAtUtc`, `NextOrderNumber` (the
  global order number counter, a concurrency token) and `IsHidden`. It raises `ConfigurationChanged`
  and `OrdersChanged`. What belongs to it: `FestivalStation` (with `NextStationOrderNumber`),
  `FestivalCatalogItem` (price and `IsAvailable`), `ItemStationAssignment`, `FestivalIngredient`
  (see the `festival-stock` page) and `Area` with its tables and station shapes (see the
  `table-plan` page). `Order` and `StationOrder` each carry a `FestivalId`.
- `Core/Services/FestivalSchedule.cs` is the only place that answers time questions about a festival:
  `IsRunning`, `FindRunningAt`, `FindOverlapping` and `HasStartWithin` (the desktop's
  `UpdateInstallGate` uses it to hold an update back). `RunningFestivalLookup` is what every other
  service asks (`FindAsync`, `IsRunning`): it reads the clock through `TimeProvider` and hands the
  question to `FestivalSchedule`. `CatalogService`, `OrderAcceptanceService`,
  `OrderItemSettlementService`, `OpenItemsService`, `EstimateService`, `StationAtFestivalLookup`,
  `FestivalMenuService`, `FestivalStationService` and the administration services of articles and
  stations all hold one.
- `FestivalAdministrationService` lists, creates, updates, copies, hides and shows festivals.
  `PeriodRefusalAsync` is the one period check. `FestivalService` counts the stations and menu items
  of a festival, and `CountOrdersByFestivalAsync` counts its orders, for the list view.
- `FestivalMenuService` and `FestivalStationService` put articles and stations on one festival and
  take them off again.
- `Infrastructure/Repositories/FestivalRepository.cs`: `FindRunningAsync` gives the shown festivals to
  `FestivalSchedule.FindRunningAt`, `CopyContentsAsync` copies a festival's configuration, and
  `FindIdsNotEndedAsync` answers "not hidden and not yet over" for `ItemOrderability` in the query
  itself (it includes festivals still to come, so it is not "running").
- Refusals: `GastronomyApp.Core/Refusals/Refusal.Festival.cs` (`PeriodInvalid`,
  `PeriodOverlapsAnotherFestival`, `FestivalIsRunning`), `Refusal.FestivalMenu.cs` and
  `Refusal.FestivalStation.cs`.
- Routes: `GastronomyApp.Api/Endpoints/AdminFestivalEndpoints.cs` under `/api/admin/festivals`: list,
  create, `PUT /{festivalId}`, `POST /{festivalId}/copy`, `/hide` and `/show` with
  `Handlers/AdminFestivalHandler.cs`; `PUT` and `DELETE /{festivalId}/items/{itemId}` and
  `POST .../availability` with `AdminFestivalMenuHandler.cs`; `PUT` and `DELETE
  /{festivalId}/stations/{stationId}` with `AdminFestivalStationHandler.cs`, which also deletes the
  station's shape on the plan. The plan's own routes are on the `table-plan` page.
- Wire records: `GastronomyApp.Contracts/Admin/Festivals` (`SaveFestivalRequest`, `SavedFestivalView`,
  `AdminFestivalView` with `IsHidden`, `IsRunning` and the three counts, `AdminFestivalListView`,
  `SaveFestivalItemRequest`). Mapping: `GastronomyApp.Api/Mapping/AdminFestivalMapping.cs`; the handler
  sets `IsRunning` from `FestivalAdministrationService.IsRunning`.

Frontend, under `frontend/src/`:

- `admin/components/festivals/FestivalsList.vue` is the Feste tab: one row per festival with the
  "Aktiv" and "Ausgeblendet" chips, open, copy, hide and show, a checkbox that reveals the hidden ones,
  and the new festival button. `FestivalDialog.vue` is the form for a new festival and for a copy.
  `shared/router/route.ts` reads `/admin/festivals/{festivalId}` and `admin/views/AdminShell.vue`
  chooses between the list and the page.
- `FestivalPage.vue` is the festival page: name, start and end save when a field is left or Enter is
  pressed, with no save button. Its sections are `FestivalStations.vue`, `FestivalItems.vue` (with
  `FestivalPlacementDialog.vue`, `StationSelect.vue` and `admin/composables/useFestivalItemRows.ts`)
  and `FestivalStock.vue` (see the `festival-stock` page).
- Stores: `admin/stores/festivals.ts` (`useAdminFestivalsStore`: `save`, `copy`, `hide`, `show`,
  `runningFestival`, reloading on `ConfigurationChanged` and `OrdersChanged`). It never computes
  "running": it takes `isRunning` from the laptop. The stations and items stores read one festival
  through `admin/stores/festivalScopedList.ts` (`loadAtTheFestival`), and `festivalStock.ts` holds the
  stock.
- Phone: `phone/stores/catalog.ts` (the catalog names the running festival), `phone/stores/order.ts`
  (`dropTheDraftIfTheFestivalChanged`), `phone/core/draftCart.ts` (`festivalId`,
  `draftIsForAnotherFestival`, `stampFestival`) and `phone/core/sendProgress.ts`
  (`changesAreRefusedFor`).
- Texts: `admin.festivals.*` and `errors.admin.festivals.*` in `shared/i18n/de.json` and `en.json`.

## Invariants

Every point below is intended behaviour. A change to any of them needs the owner's decision first.

- **No grace window at the end.** `FestivalSchedule.IsRunning` is `nowUtc < EndsAtUtc`, so a festival
  is over at the end moment itself and nothing adds time after it. From the first request after that
  the catalog answers `CatalogView.Empty` (no festival), and an order, a settlement and a station
  queue are refused (each area has its own `Refusal.<Area>.NoRunningFestival`). The admin pads the
  period instead: a festival open from 17:00 until 03:00 the next morning is entered as starting at
  12:00 and ending at 15:00 the next day.
- **Festivals never overlap in time, hidden or not.** `PeriodRefusalAsync` compares a new or edited
  period with every stored festival, hidden ones included (`FindAllAsync` filters nothing and
  `FindOverlapping` never reads `IsHidden`) and refuses with `errors.admin.festivals.overlaps`,
  naming the festival in the way. A period that only touches another one's end is allowed, and an
  edited festival ignores its own period. `ShowAsync` checks no period at all, so showing a hidden
  festival can never fail on overlap.
- **The phone drops its draft order without a word** when the catalog it loads names another festival
  or none: the draft's `festivalId` no longer equals `catalog.festival?.festivalId`, so a new empty
  order takes its place and `draftWasLost` stays false. A frozen order is never dropped: while
  `changesAreRefused` is true (a send is under way or the phone does not know how it ended) the draft
  stays on screen and in `localStorage`. A catalog that fails to load changes nothing. The draft takes
  the running festival's id with its first change, a table name typed before any line included.
- **`StationOrder` carries its own `FestivalId`** although its `Order` has one too. The unique index
  on `(FestivalId, StationId, StationOrderNumber)` in `StationOrderConfiguration` can only cover the
  columns of its own table, and it is what keeps a station's numbers unique within a festival.
- **Copying a festival exists only on the Feste list** (`FestivalsList.vue`; the festival page has no
  copy control) and copies configuration only: the station links, the menu rows with their prices,
  the station assignments and the plan's areas, tables and station shapes that are not hidden
  (see the `table-plan` page), never an order. The copy takes a new name and a new period that must pass
  the overlap check, starts both order counters at 1, puts every article back on sale and gets
  unlimited stock rows instead of the source's amounts.
- **Running means not hidden and `StartsAtUtc <= now < EndsAtUtc`, and only `FestivalSchedule`
  decides it.** The hide flag is `IsHidden`; stations, articles, categories and ingredients use
  `IsActive`, festivals never do.

## Tests

- `GastronomyApp.Core.Tests/Services`: `FestivalScheduleTest` (the start counts, the end does not, a
  hidden festival never runs, overlap with a hidden festival), `RunningFestivalLookupTest`,
  `FestivalAdministrationServiceTest`, `FestivalServiceTest`, `FestivalMenuServiceTest`,
  `FestivalStationServiceTest` and `StationAtFestivalLookupTest`; the no running festival cases of
  `CatalogServiceTest`, `OrderAcceptanceServiceTest`, `OrderItemSettlementServiceTest` and
  `StationQueueServiceTest`.
- `GastronomyApp.Infrastructure.Tests`: `Repositories/FestivalRepositoryTest` (running, not ended,
  copying), `FestivalMenuRepositoryTest`, `FestivalStationRepositoryTest`,
  `Configurations/OrderNumberIndexTest` (the station number index) and
  `Services/FestivalAdministrationServiceTest` (the stock rows of a new festival and of a copy).
- `GastronomyApp.Api.Tests/Endpoints`: `AdminFestivalEndpointsTest` (the wire shape, overlap with a
  hidden festival, show without an overlap check, hide while running, copy) and
  `FestivalScopeEndpointsTest`.
- `frontend/tests`: `admin/stores/festivals.spec.ts` and `festivalScope.spec.ts`;
  `admin/components/festivals/FestivalsList.spec.ts` and `FestivalPage.*.spec.ts` (`heading`,
  `refusals`, `stations`, `itemChanges`, `itemPlacement`) on `festivalPageFixture.ts`;
  `phone/stores/orderFestivalScope.spec.ts` (the silent drop and the frozen order). Fixtures come from
  `aFestival` in `tests/support/wireViews.ts`.
