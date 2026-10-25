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
- `Core/Services/FestivalService.cs` is the service of the Feste tab and reads and writes through
  `IGastronomyDatabase`: `ListAsync`, `CreateAsync`, `UpdateAsync`, `CopyAsync` and `HideAsync`, each
  taking the `SaveFestivalRequest` where it has one. The list projects with
  `ProjectToType<AdminFestivalView>()` (the station and menu counts are query expressions in
  `Core/Mapping/FestivalMapping.cs`), counts the orders per festival in one grouped query and sets
  `IsRunning` through `RunningFestivalLookup.IsRunning`, ordered by start, latest first.
  `PeriodRefusalAsync` is the one period check, and `CopyContentsAsync` (private) copies the
  configuration. Showing a festival again has no rule of its own, so it is
  `ActivationService.ShowAsync<Festival, SavedFestivalView>`, the generic action over `IHideable`.
- `FestivalMenuService` (put an article on the menu with its stations, take it off, set its
  availability) and `FestivalStationService` (add a station, remove it, place or move its shape on the
  plan, remove the shape) are the area services of the festival page's menu and stations. Both read and
  write through `IGastronomyDatabase`, answer `SavedItemView` or `SavedStationView` through Mapster
  (`Core/Mapping/FestivalMapping.cs`) or `Success`, and keep their refusals in order: festival, article
  or station, the stations of the festival, an active station that prepares the item. The stock rows
  are `FestivalStockService` (see the `festival-stock` page).
- `Infrastructure/Repositories/FestivalRepository.cs` stays for the services of the unconverted
  sub-areas, the desktop and `RunningFestivalLookup`: `FindRunningAsync` gives the shown festivals to
  `FestivalSchedule.FindRunningAt` and `FindAllAsync` loads all for the desktop. `ItemOrderability` asks "not hidden and not yet over" of `IGastronomyDatabase` itself (it includes festivals still to come, so it is not "running").
- Refusals: `GastronomyApp.Core/Refusals/Refusal.Festival.cs` (`PeriodInvalid`,
  `PeriodOverlapsAnotherFestival`, `FestivalIsRunning` with
  `errors.admin.festivals.runningFestivalStaysShown`, which is also the tooltip of the festival
  list's disabled eye), `Refusal.FestivalMenu.cs` and `Refusal.FestivalStation.cs`.
- Routes: `GastronomyApp.Api/Endpoints/AdminFestivalEndpoints.cs` under `/api/admin/festivals`: list,
  create, `PUT /{festivalId}`, `POST /{festivalId}/copy`, `/hide` (all calling `FestivalService`) and
  `/show` (calling `ActivationService`); `PUT` and `DELETE /{festivalId}/items/{itemId}` and
  `POST .../availability` calling `FestivalMenuService`; `PUT` and `DELETE
  /{festivalId}/stations/{stationId}` calling `FestivalStationService`, whose removal also deletes the
  station's shape on the plan. The plan's own routes are on the `table-plan` page.
- Wire records: `GastronomyApp.Contracts/Admin/Festivals` (`SaveFestivalRequest`, `SavedFestivalView`,
  `AdminFestivalView` with `IsHidden`, `IsRunning` and the three counts, `AdminFestivalListView`,
  `SaveFestivalItemRequest`). Mapping: `GastronomyApp.Core/Mapping/FestivalMapping.cs`.
- The station list read (`GET /api/admin/stations`, with `?festivalId=` on the festival page)
  answers what would refuse taking each station out, so a screen disables the removal or the switch
  off before the click. `AdminStationView.HasItemsToHandOutAtTheRunningFestival` comes from
  `Core/Services/ItemsToHandOutLookup.cs`, the one home of that check: `FindStationIdsIfRunningAsync`
  (a festival: its stations with an item not handed out, and none unless it runs) and
  `FindStationIdsAtTheRunningFestivalAsync` (the same for the running festival, none when nothing
  runs), over `StationRepository.FindStationIdsWithItemsToHandOutAsync`. The list,
  `FestivalStationService.RemoveAsync` (`StationHasOrdersAtTheFestival`) and
  `StationService.DeactivateAsync` (`StationHasUnfulfilledItems`) all ask it, and so
  do the tables and areas of the table plan: its read and the hiding of a table or an area ask
  `RunningFestivalLookup.IsRunning` before they count the items not handed out or not settled (see the
  `table-plan` page). So "only while it runs" is decided there and nowhere else, and no screen checks it again. The articles left without a station
  (`ItemsWouldHaveNoStation`) come in two fields, each filled only by the read whose screen offers
  that action and null in the other: `StrandedItemCountWhenRemovedFromTheFestival` on the festival
  read (`ItemOrderability.FindItemsStrandedByRemovingEachStationAsync`, which `RemoveAsync` asks for
  one station) and `StrandedItemCountWhenSwitchedOff` on the read without a festival
  (`FindItemsStrandedBySwitchingOffEachStationAsync`, which `DeactivateAsync` asks for one station).
  A list reads the festivals not yet over once, and per festival its active stations and menu once,
  then one query for the menu as it is and one per active station of the festival; a station that
  is not an active station of the festival strands nothing there, without a query. `StationService.ListAsync` puts the values into the `AdminStationView` it returns.

Frontend, under `frontend/src/`:

- `admin/components/festivals/FestivalsList.vue` is the Feste tab: one row per festival with the
  "Aktiv" and "Ausgeblendet" chips, copy, the pencil "Bearbeiten" (`BaseEditButton.vue`) that opens
  the festival page, hide (the crossed-out eye of the category rows with
  the tooltip "Ausblenden", `BaseIconAction.vue`, asking through `BaseHideConfirmDialog.vue`, and on
  the running festival disabled with `runningFestivalStaysShown` as its tooltip through
  `BaseActionWithRefusalReason.vue`) and
  show (the open eye with the tooltip "Einblenden", `BaseShowButton.vue`), in that order, the
  checkbox that reveals
  the hidden ones in the heading line (`BaseHeadingWithCheckbox.vue`, as on every global tab), and
  the new festival button; the table plan's areas and tables hide and show through the same pieces
  (see the `hideable-rows` how-to page). `FestivalDialog.vue` is the form for a new festival and for a copy.
  `shared/router/route.ts` reads `/admin/festivals/{festivalId}` and
  `/admin/festivals/{festivalId}/areas/{areaId}`, and `admin/views/AdminShell.vue` chooses between the
  list, the page and the editor of one area of the table plan.
- `FestivalPage.vue` is the festival page; an unknown festival id returns to the overview through
  `admin/composables/useOpenedFestival.ts`, which the editor of an area shares. It decides only once
  the festival list has arrived (`hasLoaded` of `admin/stores/adminList.ts`), so a read overtaken by
  the connection's refetch neither sends the admin away nor skips loading the page. The article
  rows' trash icon is `BaseTrashButton.vue` inside `BaseActionWithRefusalReason.vue` (disabled with
  its reason while the festival runs), and so is the station rows' trash icon, disabled with
  `stationHasOrdersAtTheFestival` while the station holds items to hand out at the running
  festival, and otherwise with the full `itemsWouldHaveNoStation` refusal while removing it would
  leave articles without a station, the order in which the laptop refuses. The full text, which
  asks the admin to give the articles another station first, stays here because the page's article
  rows offer their stations. Its German and English texts link the fact sentence
  `admin.stations.messages.itemsWouldHaveNoStation` (`@:`), which the Ausgabestellen tab shows
  alone, so that sentence is written once per language.
  Both reasons are chosen in `admin/composables/useStationRefusalReasons.ts`, shared with the
  Ausgabestellen tab. The
  station rows are `stations/RemovableStationRows.vue`, which the table plan editor's station list
  uses too; a refusal that comes back anyway shows on the row. The article and station removals ask
  through `BaseRemoveConfirmDialog.vue`. The station card's and the article card's search and add
  button are `BaseSearchAddLine.vue`, with "Neu…" in its slot; with nothing to add the station search
  says either that every station switched on is already here (`noStationsToAdd`) or that none is
  switched on (`noStationSwitchedOn`). Every row of the page's lists is a `BaseListRow.vue`, the one
  home of the tint on every second row. Name, start and end save when a field is left or Enter is
  pressed, with no save button. Its sections are `FestivalStations.vue`, `FestivalTablePlan.vue`
  (see the `table-plan` page), `FestivalItems.vue` (with `FestivalPlacementDialog.vue`,
  `StationSelect.vue` and `admin/composables/useFestivalItemRows.ts`) and `FestivalStock.vue` (see
  the `festival-stock` page).
- Stores: `admin/stores/festivals.ts` (`useAdminFestivalsStore`: `save`, `copy`, `hide`, `show`,
  `runningFestival`, reloading on `ConfigurationChanged` and `OrdersChanged`). It never computes
  "running": it takes `isRunning` from the laptop. The stations and items stores read one festival
  through `admin/stores/festivalScopedList.ts` (`loadAtTheFestival`); the stations store reloads on
  `ConfigurationChanged` and `OrdersChanged`, because `hasItemsToHandOutAtTheRunningFestival`
  changes with every order and every hand-out. `festivalStock.ts` holds the stock and `tablePlan.ts` the plan.
- Phone: `phone/stores/catalog.ts` (the catalog names the running festival), `phone/stores/tablePlan.ts`
  (the running festival's plan, empty when none runs), `phone/stores/order.ts`
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
  period with every stored festival, hidden ones included (the query filters nothing and
  `FindOverlapping` never reads `IsHidden`) and refuses with `errors.admin.festivals.overlaps`,
  naming the festival in the way. A period that only touches another one's end is allowed, and an
  edited festival ignores its own period. `ShowAsync` checks no period at all, so showing a hidden
  festival can never fail on overlap.
- **The phone drops its draft order without a word** when the catalog it loads names another festival
  or none: the draft's `festivalId` no longer equals `catalog.festival?.festivalId`, so a new empty
  order takes its place and `draftWasLost` stays false. A frozen order is never dropped: while
  `changesAreRefused` is true (a send is under way or the phone does not know how it ended) the draft
  stays on screen and in `localStorage`. A catalog that fails to load changes nothing. The draft takes
  the running festival's id with its first change, a table chosen before any line included.
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
- **What belongs to a festival is there once.** A station takes part once (`FestivalStation`, unique
  on `(FestivalId, StationId)`, carrying the number that station shows at that festival), an article
  is on the menu once (`FestivalCatalogItem`, unique on `(FestivalId, CatalogItemId)`, with its price
  there and whether it has sold out tonight), and an article is assigned to a station once
  (`ItemStationAssignment`, unique on `(FestivalId, CatalogItemId, StationId)`). `CatalogItem`
  carries neither a price nor an availability, because both are facts about an article at one
  festival.
- **One page manages a festival end to end.** `/admin/festivals/{festivalId}` holds the name and
  dates, then the stations, the table plan, the articles and the ingredient stock, each in an
  outlined card. It works the same for past, running, future and hidden festivals; nothing on it is
  read-only.
- **The festival page has no save button of its own.** Its fields (name, dates, an article's price
  and stations, an ingredient's available amount) save the moment the admin leaves them, and a field
  the laptop refuses says so on its row. The forms it opens, to create or edit a station, an article
  or a category, to create or rename a table plan (`TablePlanNameDialog.vue`) and to place an
  article with its price and stations, have their own save button.
- **Adding to a festival is inline:** a type-to-search field, an add button, and a "Neu…" button
  beside them that opens the create form. A new station is then chosen for adding; a new article
  goes straight to the form for its price and stations.
- **Words:** the festival page says "Artikel", never "Karte", and a running festival is "Aktiv" /
  "Active".

## Tests

- `GastronomyApp.Core.Tests/Services`: `FestivalScheduleTest` (the start counts, the end does not, a
  hidden festival never runs, overlap with a hidden festival), `RunningFestivalLookupTest`,
  and `StationAtFestivalLookupTest`; the no running festival cases of
  `Infrastructure.Tests/Services/CatalogServiceTest`, `OrderAcceptanceServiceTest`, `OrderItemSettlementServiceTest` and
  `StationQueueServiceTest`.
- `GastronomyApp.Infrastructure.Tests`: `Repositories/FestivalRepositoryTest` (running),
  `Repositories/FestivalStationRepositoryTest`, `Services/FestivalMenuServiceTest` and
  `Services/FestivalStationServiceTest` (every action on the real DbContext, shapes included),
  `Configurations/OrderNumberIndexTest` (the station number index) and
  `Services/FestivalServiceTest` (every festival action on the real DbContext: the list with its
  counts and `IsRunning`, the period checks, the stock rows of a new festival and of a copy, and
  what a copy takes across, plan included) and `Services/ActivationServiceTest` (showing).
- `GastronomyApp.Api.Tests/Endpoints`: `AdminFestivalEndpointsTest` (the wire shape, overlap with a
  hidden festival, show without an overlap check, hide while running refused with its reason, copy) and
  `FestivalScopeEndpointsTest`.
- What would refuse taking a station out: `GastronomyApp.Core.Tests/Services/ItemsToHandOutLookupTest`
  (running, over, not yet started, nothing running), `StationRepositoryTest` in
  `GastronomyApp.Infrastructure.Tests/Repositories` (another festival's open items name no
  station), `StationServiceTest` in `GastronomyApp.Infrastructure.Tests/Services` (which festival the flag is read at, a festival that
  is over, which stranded count each read fills, the statement count not growing with the stations), `FestivalStationServiceTest`
  and `Services/ItemOrderabilityTest` (on the real DbContext: each station on its own, a station that is not an active station of
  the festival, the active stations and the menu read once and one query per active station). In
  `GastronomyApp.Api.Tests/Endpoints`, `AdminFestivalEndpointsTest` and `StationDeactivationTest`
  each read the list and then try the action for a station with an open item while the festival
  runs, has not started and is over, and check that the flag matches the answer; they also cover
  each read's stranded count, and that each read leaves the other read's count null. The filters themselves:
  `GastronomyApp.Infrastructure.Tests/Queries/OrderItemQueriesTest`.
- `frontend/tests`: `admin/stores/festivals.spec.ts` and `festivalScope.spec.ts`; the station
  list's reload on `OrdersChanged` in `admin/stores/screenListening.spec.ts`;
  `admin/components/festivals/FestivalsList.spec.ts` (the eye icons and the pencil with their
  names, the order of a row's actions, the eye disabled on the running festival with its reason) and
  `FestivalPage.*.spec.ts` (`heading`, `refusals`, `stations` with the disabled trash icon, its
  two reasons and the two sentences of the empty search, `itemChanges`, `itemPlacement`,
  `tablePlan`, `tablePlanAreas`) on
  `festivalPageFixture.ts`, the guard against an overtaken festival read in `.heading`, and the
  loaded state in `admin/stores/adminList.spec.ts`;
  `phone/stores/orderFestivalScope.spec.ts` (the silent drop and the frozen order). Fixtures come from
  `aFestival` in `tests/support/wireViews.ts`.
