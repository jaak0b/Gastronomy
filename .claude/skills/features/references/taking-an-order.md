# Taking an order

A waiter taps a table on the plan and "Bestellung aufnehmen", and builds that table's order: the
categories screen shows one button per category with the number of portions already taken from it,
the table's name in the header with the way back to the plan, and a bar with the item count and the
running total. Tapping a category lists its articles, one row each, where a tap adds one portion, the
Note button adds one with a note, and the rows under the article count the portions by note and
station and take them off again.

## Where the code lives

Backend, the catalog the screen reads: `GET /api/catalog` in `Endpoints/CatalogEndpoints.cs` calls
`CatalogService.ReadRunningFestivalAsync` (`Core/Services`), which answers `CatalogView.Empty` when no
festival runs. It asks `ItemOrderability` (also in `Core/Services`, reading through
`IGastronomyDatabase`) for the articles that are on the menu, switched on and prepared at an active
station of the festival, then runs two projections: the festival's active stations
(`FestivalStationQueries.ActiveAt`) and the menu rows with their category (`Core/Mapping/CatalogMapping.cs`,
into `Results/CatalogMenuEntry`). The articles come in the admin's order, the categories are the
ones those articles belong to, each once. The whole read is six statements including the running
festival lookup, pinned by `Infrastructure.Tests/Services/CatalogServiceTest`, because every phone
repeats it on every hub event. The estimates the screen shows are on the
`handing-out-at-a-station` page.

Frontend, under `frontend/src/phone/`:

- `views/Catalog.vue` (route `/catalog`) is the whole ordering screen: the category buttons in two
  columns (`labelFor` adds the portion count from `core/categoryPortions.ts`), the basket bar
  (`components/catalog/BasketBar.vue`, which goes on to `/review`) and, once a category is open, its
  name as the shared `shared/components/CategoryLabel.vue` (the admin's category label at the
  phone's sizes), its articles in `components/catalog/ItemGrid.vue` and the button back to the
  categories. The open category is the router's step `category`, so back closes it before it
  leaves the screen. It decides
  where a new portion goes: `addItem` places it straight away or opens the station question, and
  `chooseStation` answers it. It leaves for the summary while the order is locked and for the plan
  while the draft has no table. The header (`components/header/AppHeader.vue`) shows the table's
  name and "Tischplan".
- `views/TablePlan.vue` opens the ordering screen from the table's action sheet
  (`openingTheOrderAt` in `core/draftCart.ts`; see the `table-plan` page).
- `components/catalog/ItemRow.vue` is one article: tapping its name or its price adds one portion,
  the Note button adds one with a note, and each group of portions has its count, its station, its
  note, minus and plus.
  `ItemNoteDialog.vue` asks for a note or corrects one; `LineStationSheet.vue` asks which station is
  to make the article, with a note field when a portion is being added and without one when portions
  move to another station.
- `core/itemPositions.ts`: `positionsForItem` lists the draft lines of one article,
  `groupPositions` groups them by note and station, and `buildItemPositionsView` adds the count and
  the article total. `core/routingPreview.ts` (`needsStationChoice`, `candidateStations`),
  `core/estimates.ts` (`estimateRangeForItem`, the quickest and the slowest station's minutes),
  `core/estimateWording.ts`, `core/catalogItemState.ts` and `core/totals.ts`.
- `core/draftCart.ts` holds the draft order (`DraftOrder`, `DraftLine`, and `DraftTable`, the
  `tableId` and `name` of the table it is taken at) and saves it to `localStorage` under `draftOrder`
  with every change; `openingTheOrderAt` decides what "Bestellung aufnehmen" does with the draft.
  `core/basket.ts` turns it into the lines the basket shows, and `core/submission.ts` sends the
  table's id. `shownNameOf` names the draft's table, read on every screen through
  `composables/useDraftTableName.ts`.
- Stores: `stores/order.ts` (`useOrderStore`: `addItem`, `dropLine`, `noteLine`, `chooseStation`,
  `chooseTable`, `discardTheDraftAndChooseTable`, `itemCount`, `totalCents`, `changesAreRefused`),
  `stores/catalog.ts`, and
  `stores/estimates.ts`, whose `load` the screen calls when it opens. `composables/useStationChoiceQuotes.ts`
  gives the station question its waiting times.
- `views/Review.vue` is the summary: the lines through `components/review/LineList.vue`, the delivery
  mode per station (`order.chooseDeliveryMode`, with `core/stationOrders.ts` and
  `core/stationDeliveries.ts`), the order total and the two send buttons.
- Once the laptop accepted the order, `composables/useSendAndLeave.ts` lands on the plan of the
  area the order was taken in, with that area still open, for "Senden und später kassieren", or
  opens the table's settle view with the new items ticked for "Senden und kassieren". It does so in
  the continuation of the send the waiter started, whichever screen shows when the answer comes, so
  a back press while the order is on its way changes nothing. Both moves are redirects that take the
  order screen and the summary out of the history, so neither back nor forward reopens the sent
  order (see the `back-button` page).
- Texts: `phone.catalog.*`, `phone.tablePlan.*` (the action sheet and the question about unsent
  items), `common.labels.soldOut` and `common.estimates.*` in `shared/i18n/de.json` and `en.json`.

Backend, under `backend/`:

- `POST /api/orders` (`GastronomyApp.Api/Endpoints/OrderEndpoints.cs`, calling `OrderPlacementService`
  directly) takes `PlaceOrderRequest` (`GastronomyApp.Contracts/Orders`): the client order id, the
  `TableId` of a table of the plan, the items and the delivery modes, and answers `PlacedOrderView`.
- `GastronomyApp.Core/Services/OrderPlacementService.cs` is the area service for placing an order
  (`PlaceAsync`). It looks the client order id up first (a retry returns the stored order), then the
  running festival (`RunningFestivalLookup`) and the table, then routes every line in the order sent
  with four batched reads (articles, menu rows, assignments, active stations) and
  `OrderRoutingResolver`, and collects every refused line. Only then does it number the order
  (`SequenceNumberAllocator`: the global order number from the festival, the station number from the
  festival's station link, each saved at once inside the request's transaction), give every item
  its first `Open` status through `OrderItemLifecycle`, add it, save, make each first status the
  item's current one and save again (an item and its status point at each other, so EF cannot store
  both in one save; see the `handing-out-at-a-station` page), run `StockSoldOutMarker` and read the
  order back for the answer. The answer is mapped by `Core/Mapping/OrderMapping.cs`, which takes the status from
  `OrderQueries.Status` (fulfilled when nothing is left to hand out, partially fulfilled when
  something is handed out, open otherwise; cancelled items count as nothing left) and sums the
  prices of the items that are not cancelled. Refusals are
  in `Core/Refusals/Refusal.Order.cs`.

## Invariants

- **A note is never merged into another line (root rule 19).** Every portion is a draft line of its
  own (`addLine` appends one per tap), and `groupPositions` keys a group on the note and the station
  together, so `3 x Hotdog` with "ohne Ketchup" never joins the plain ones. Only the article's own
  count and total sum every group.
- **A station is chosen only for an article with several candidate stations.** `needsStationChoice`
  is true when the article lists more than one station. Such an article opens the station question
  before the portion is added (the Note button opens it with the note field focused), its group rows
  name the station as a button that moves those portions, and plus on a group adds at that group's
  station without asking. An article with one station is placed there at once and its group row
  names the station as plain text.
- **The draft is one order, never a list.** `draftCart.ts` saves the order on every change, a reload
  puts it back, and a stored draft that cannot be read is thrown away and reported once
  (`draftWasLost`), a draft an older build stored under a typed table name included. Nothing sends
  it on its own; see the frontend `CLAUDE.md` rule on the retry queue.
- **The draft shows its table's current name.** The header, the summary and the question about
  unsent items show the name the phone's plan holds for the table now, and the name the draft was
  opened under only while the table is not on the plan.
- **The draft belongs to the table it was opened at.** "Bestellung aufnehmen" on a table takes the
  table when the draft has no items, carries on with the draft on its own table, and asks first when
  another table's items are on it. The question (`components/tablePlan/UnsentOrderDialog.vue`) hides
  the plan behind an opaque surface (scrim `background` at opacity 1) and says in one sentence which
  table holds how many unsent items for what total (`phone.tablePlan.messages.unsentOrder`, counted
  with `order.itemCount` and priced with `formatPrice` like the basket bar). Its three buttons: "Zu
  Tisch …" (the filled one) goes to that table's order, "Verwerfen" discards the items and takes the
  order for the tapped table without asking again, and "Zurück" closes the question and leaves the
  draft and the plan as they were. "Position kassieren" and "Position stornieren" ask nothing and leave the
  draft alone. A draft that
  was never sent locks nothing: the waiter may go back to the plan, and the items wait for their
  table.
- **A frozen order takes no change and blocks the way to the plan.** While a send attempt is
  unresolved (`changesAreRefusedFor`), the order store ignores every change, and the ordering screen
  and the home screen move the waiter to the summary as they open (`waiterLandingFor` puts the locked
  order first). A reload always starts at home (`routeToStartAt` in `shared/router/route.ts`), so it
  leads there too. A draft from another festival is dropped silently unless it is frozen (see the
  `festivals` page).
- **The total is a calculation aid.** Once a portion is on the order, the article shows
  "{count} × {price}" and its total, and the basket bar shows the item count and the order total on the
  categories screen only; the screen of one category leaves the order total out so the items own the
  screen, and the summary shows it too. `orderTotalCents` adds up the unit prices of the basket lines, and a line whose
  article left the menu adds nothing.
- **An item keeps the price the phone showed.** The item name comes from the catalog; the price is
  the one the phone displayed to the guest, and the laptop stores it untouched
  (`OrderItem.UnitPriceCents`), also when the item is given away later.
- **The delivery mode is chosen per station on the summary, before sending.** Each station's part of
  the order is handed out together (the default, `DELIVERY_MODE_BEFORE_THE_SERVER_CHOOSES` in
  `core/stationOrders.ts`) or item by item as each is ready. It is fixed once the order is sent, and
  no screen offers to change it (see the `handing-out-at-a-station` page).
- **An order is always sent open.** "Senden und später kassieren" is for a table that runs a tab;
  "Senden und kassieren" is for a guest who pays on the spot, and only opens the settle view with the
  new items ticked.
- **Sold out stops adding, not removing.** Tapping the name, the price, the Note button or plus adds
  nothing, and the waiting time gives way to "Ausverkauft"; minus still takes portions off.
- **The keyboard reaches adding once.** The name and the price add the same portion, and keyboard
  focus reaches that action once per article.
- **The summary sends only with a table.** Both send buttons stay disabled while the draft has no
  table, and `buildSubmitRequest` throws for one, because the plan is the only way to an order.
- **An order names a table of the running festival.** The laptop looks the `TableId` up at the
  running festival, hidden tables included. A table it does not find there is a broken program: it
  logs the table id, answers 422 with `errors.order.cannotBeProcessed` and stores nothing, so the
  phone's handling of a refusal with our envelope applies. A retry with a known client order id
  returns the original order before the table is looked at.
- **What happens when a step of placing fails.** No running festival, an unknown table, an item
  that is not on the menu or not routable: refused before anything is numbered or written, so no
  order exists. The insert, the numbers, both saves of the items' first status and the stock step
  share the request's transaction; a failure in any of them rolls all of it back and the phone
  keeps the order on screen with its retry.
- **An order and its items are never deleted.** Nothing in the app removes one. Deleting an item
  with its statuses loaded fails on the pointer between the item and its current status, so a
  feature that ever deletes orders clears that pointer in an earlier save first.

## Tests

- `frontend/tests/phone/components/catalog`: `ItemRow.row.spec.ts` (adding by name and by price, the
  keyboard order, the parts beside the price, sold out, the default station),
  `ItemRow.figures.spec.ts` (price, waiting time, counts and totals), `ItemRow.notes.spec.ts` (the
  note dialog, the note and station groups, the station question for a note) and
  `ItemRow.keyboardInset.spec.ts`, on `itemRowFixture.ts`.
- `frontend/tests/phone/views`: `Catalog.building.spec.ts` (adding, the way to the summary, the
  frozen order, no table), `Catalog.categories.spec.ts`, `Catalog.stationChoice.spec.ts` (the station
  question, also when the price is tapped) and `Catalog.waitingTime.spec.ts`, on `catalogFixture.ts`;
  `Review.sending.spec.ts` (both landings after sending, no table); `TablePlan.sheet.spec.ts` (the
  action sheet, and the question about unsent items: its sentence with count and total in both
  languages, each of its three buttons and what follows) and `TablePlan.landing.spec.ts` (the locked
  order leading to the summary).
- `frontend/tests/phone/core`: `itemPositions.spec.ts`, `categoryPortions.spec.ts`,
  `routingPreview.spec.ts`, `stationOrders.spec.ts`, `stationDeliveries.spec.ts`,
  `draftCart.editing.spec.ts` (including `openingTheOrderAt`),
  `draftCart.storage.spec.ts`, `draftCart.unreadable.spec.ts` (including a draft with a typed table
  name), `submission.spec.ts`, `totals.spec.ts`, `estimates.spec.ts`, `estimateWording.spec.ts` and
  `catalogItemState.spec.ts`; `frontend/tests/AppBackPressAfterSending.spec.ts` (the landing after
  sending, back during and after a send); `frontend/tests/shared/router/bootPath.spec.ts` (a reload starts at
  home).
- `frontend/tests/phone/stores`: `order.reload.spec.ts` (the unreadable draft, an order frozen by a
  reload), `order.sending.spec.ts` (the table's id on the wire, a frozen order keeping its table) and
  `orderFestivalScope.spec.ts`; `frontend/tests/phone/composables/useStationChoiceQuotes.spec.ts` and
  `useSendAndLeave.spec.ts`. `tests/support/waiterPhone.ts` (`theWaiterTakesTheOrderAt`) gives a
  store spec an order at a table.
- Backend: `GastronomyApp.Infrastructure.Tests/Services/OrderPlacementServiceTest.cs` (on the real
  DbContext: storing, numbering, routing, delivery modes, a retried client order id, every refusal and
  the sold-out marker), `SequenceNumberAllocatorTest`, `StockSoldOutMarkerTest`,
  `Mapping/OrderMappingTest`, `GastronomyApp.Core.Tests/Services/OrderRoutingResolverTest`,
  `GastronomyApp.Api.Tests/Endpoints/ConcurrentOrderTest`,
  `GastronomyApp.Api.Tests/Endpoints/OrderEndpointsTest.cs` (a table of the running festival, a
  hidden one, one of another festival), `OrderIdempotencyTest.cs` and `OrderRefusalLoggingTest.cs`.
- jsdom has no layout and does not apply scoped styles, so no spec proves where the row's parts
  land on a phone. A change to the row's layout is measured in a real browser at 360 px with the
  root font at 100, 130 and 160 percent.
