# Handing out at a station

The laptop splits every order by station (the kitchen, a bar). Each station has one tablet, set up
like a phone, and its page shows two columns: every order that still has open items, and the orders
the waiter wanted handed out item by item as each is ready. The staff select the items they hand
out, one or several at a time, and mark them done after a confirmation that repeats the table name.
A fully done order leaves both columns and appears in the done view, where a mistaken tap can be put
back; an open column, the overview board and the done view without an order each show the shared empty
list text (`BaseEmptyListText`, `data-test="nothing-done"` in the done view). The waiter's phone shows a waiting time per item and per station order, worked out from the
station's queue.

## Where the code lives

Backend, under `backend/`:

- `GastronomyApp.Core/Entities/StationOrder.cs`: the part of an order that belongs to one station,
  with `StationOrderNumber` (the number that station shows for it), `DeliveryMode` and
  `IsHiddenFromAsItComesQueue`. An article's `CatalogItem.ProductionMinutes` and
  `IsQueueIndependent` feed the estimates.
- `Core/Entities/OrderItemStatus.cs` with `Contracts/Enums/OrderItemState.cs` (`Open = 0`,
  `HandedOut = 20`, `Cancelled = 30`, `CancellationAcknowledged = 40`, numbers pinned, never
  compared with `<` or `>`): every status an item has had,
  each row pointing at the one before it (`PreviousStatusId`, unique) and naming who set it, a waiter
  or a station (exactly one, a check constraint). `OrderItem.CurrentStatusId` points at the newest
  row. Rows are never changed or deleted; they go only with their item when the database cascade
  removes it.
- `Core/Queries/OrderItemQueries.cs`: the one home of "what state is this item in". The
  `[Projectable]` checks `IsOpen`, `IsHandedOut`, `IsCancelled` (both cancelled states), `IsStillOrdered`
  (not cancelled), `IsSettled`,
  `AwaitsCancellationAcknowledgement` (cancelled while it was open, not yet acknowledged),
  `IsOnTheQueueCard` (open, handed out or waiting for its acknowledgement), `State` and
  `StateSetAtUtc` work in SQL and on loaded items alike, and back the filters `WhereOpen`,
  `WhereStillOrdered` and `WhereToHandOutOrToSettle`. `Core/Queries/StationOrderQueries.cs` picks
  the cards of the two reads (`WhereAnItemIsOpenOrAwaitsCancellationAcknowledgement` for the queue,
  `WhereListedAsDone` for the done view). The checks on the bare enum are in `OrderItemStateQueries`.
  `OrderItem.CurrentStatus` is auto-included, so every loaded item carries its current status; the
  status before it is included by hand where a cancellation's acknowledgement is decided in memory
  (see the `query-building-blocks` page of `architecture`). Every reader goes through these checks,
  never through `CurrentStatus.Status` of its own.
- `Core/Services/OrderItemLifecycle.cs`: the only writer of status rows and the only home of the
  allowed changes. `JudgeChange` answers every pair of current and asked state from one table with
  an `OrderItemChangeVerdict` (`Results/`): `Allowed` (none to `Open` by the order's waiter, `Open`
  to `HandedOut` and back by the station, `Open` or `HandedOut` to `Cancelled` by a waiter,
  `Cancelled` to `CancellationAcknowledged` by the station only when the item was open before it was
  cancelled), or why not: `AlreadyInThatState`, `ItemIsCancelled`, `ItemIsNotHandedOut` (an open
  item put back), `ItemDoesNotAwaitCancellationAcknowledgement`, `ItemIsNotPlacedYet`.
  `AppendStatus` throws on anything but `Allowed` and appends a row after the current one. The
  station and the cancel action each turn a verdict into their own refusal with a flat `switch`, so
  a refusal can never disagree with what `AppendStatus` accepts. Reading an acknowledgement without the status before the
  current one throws an `InvalidOperationException` naming `OrderItemStatus.PreviousStatus`. Because an item and its status point at each
  other, a new item's first `Open` row is saved first and made current with `PointAtFirstStatus` in a
  second save of the same request transaction; `OrderPlacementService` does this. Handing out and
  putting back need one save.
- `Core/Services/StationQueueService.cs`: the one service of the station tablet's queue, on
  `IGastronomyDatabase`. `ReadQueueAsync` and `ReadFulfilledAsync` answer the two reads,
  `FulfillAsync`, `UnfulfillAsync`, `AcknowledgeCancellationAsync` and
  `HideFromAsItComesQueueAsync` the four actions (the first three share one loop that finds the
  selected items, refuses unknown ones, asks `OrderItemLifecycle.JudgeChange` about each and turns
  the verdict into the station's refusal (`ItemWasCancelled`, `ItemNotFulfilled` for a put-back,
  `ItemNotAwaitingCancellationAcknowledgement`), skips an item `AlreadyInThatState`, so a repeated hand-out or acknowledgement changes nothing,
  and appends the status to the rest), and each
  action answers with the fresh `StationQueueView`. Every method first finds the station at the
  running festival (unknown station, no running festival, station not at the festival, in that
  order, in three statements), so the queue read runs four statements. A request names items by id,
  and an id that is not one of this station's items refuses the whole request and stores nothing. The
  item order inside a card is sorted in memory with `StringComparer.OrdinalIgnoreCase`, because
  `OrderItem.ItemName` and `Note` carry no `NOCASE` collation. The estimates are `EstimateService` (`ListAsync`, `QuoteAsync`) on `IGastronomyDatabase`, with the pure `StationWaitingTimes` beside it, which sums the open work of one station (`Results/OpenWork`) for an article (`Results/ArticleTiming`). `Core/Mapping/EstimateMapping.cs` projects the timed assignments, the articles and the open work. Refusals:
  `Core/Refusals/Refusal.StationQueue.cs`. The wire records are mapped by
  `Core/Mapping/StationQueueMapping.cs` (cards, lines and the station summary). The numbers come from
  `Core/Services/SequenceNumberAllocator.cs`.
- Routes in `GastronomyApp.Api/Endpoints/StationEndpoints.cs`. For the station tablet:
  `GET /api/station/orders`, `GET /api/station/orders/fulfilled`, `POST /api/station/items/fulfill`,
  `POST /api/station/items/unfulfill`, `POST /api/station/items/acknowledge-cancellation` and
  `POST /api/station/orders/{stationOrderId}/hide`
  (`StationQueueService` directly). For the phone: `GET /api/estimates` and
  `POST /api/estimates/quote` (`EstimateService` directly). Wire records in `GastronomyApp.Contracts/Stations`,
  the estimate records in `Contracts/Orders`, `DeliveryMode` and `OrderStatus` in `Contracts/Enums`.

Frontend, under `frontend/src/`:

- `station/views/StationPage.vue` with `station/components/` (`StationOpenBoard.vue`,
  `StationOrderCard.vue`, `StationFulfilledCard.vue`, `StationDoneDialog.vue`),
  `station/composables/` and `station/stores/station.ts`. `shared/core/stationBoard.ts` builds the
  lines, the counts and the selection: `openItemsIn` (what can be selected and marked done),
  `itemsNotHandedOutIn` (what a card lists: the open items and the cancelled ones waiting for their
  acknowledgement), `itemsAwaitingAcknowledgementIn` (the cancelled items of a queue card, which
  are exactly the ones waiting, because the queue sends no other cancelled item) with
  `selectedItemIdsAwaitingAcknowledgement`, `itemsNotCancelledIn` (what the done card's summary
  counts), `cancellationText` (the "Storniert um 18:42" or "Stornierung bestätigt um 18:45" line of
  a cancelled item, from its `stateSetAtUtc`), and `itemLines`,
  which groups by article and note and never merges a cancelled item into a line of open or
  handed-out ones (`ItemLine.isCancelled`). `retainStillSelectableItemIds` keeps a selection across
  a new queue only for items still selectable in the same state, so a selected open item that a
  waiter cancels drops out of the selection instead of turning into an acknowledgement.
  `stationFailureMessage` turns a refused action into the
  key and the parameters the page shows, so a refusal naming an item names it.
- The store's four actions (`fulfill`, `unfulfill`, `acknowledgeCancellation`, `hide`) each call
  their route and share `runAction`, which takes a number from the board's request gate, so an
  action's answer always wins over a load already in flight, and turns a failure into the page's
  refusal line.
- `shared/core/orderItemStates.ts` (`isOpen`, `isHandedOut`, `isCancelled`) is the frontend's one
  reading of the wire's `OrderItemState`, shared with the waiter's settle view. A cancelled item is
  drawn struck through by the global class `cancelled-item` in `shared/styles/cancelledItems.css`,
  the same class the settle view uses, set on the item's name and note so its state line stays
  readable. On the card it is a line with the time it was cancelled; selecting it replaces
  "Zubereiten" with "Stornierung bestätigen", which acknowledges the selected cancelled items of
  that card at once, without a confirmation. In the grouped view it is a line of its own, and in the
  done view a row with its state line and neither tick nor put back.
- The phone: `phone/core/estimates.ts` and `phone/stores/estimates.ts` for the waiting times; the
  delivery mode is chosen on the summary (see the `taking-an-order` page).

## Invariants

- **An item is open, done or cancelled.** Done is the current status `HandedOut`, appended when the
  station hands the item out; putting a mistaken tap back appends a new `Open` after it. A waiter
  cancels an open or a handed-out item (see the `settling-a-table` page), and a cancellation is
  final. The history keeps every step, and there is no in-between production state.
- **A cancelled item is never handed out and leaves every count.** Handing out or putting back a
  cancelled item is refused naming it (`errors.station.itemWasCancelled`). A card keeps an item
  cancelled while it was open until the station acknowledges it; acknowledging an item already
  acknowledged changes nothing, and one that does not wait for an acknowledgement is refused with
  `errors.station.changeNotSaved`. An acknowledgement that does not reach the laptop leaves the item
  selected and struck through, with the page's message to tap again. An item cancelled after
  hand-out needs no acknowledgement and leaves the card. A card whose items were all cancelled
  appears in the done view, so its station number leaves no gap. A card stays in the queue while one of its items is open or waits for its
  acknowledgement. The card's item count and done count, the waiting times and the order's status
  leave cancelled items out.
- **Every saved item has a current status.** Placing stores each item with its first `Open` row,
  set by the order's waiter at the order's time, and both saves of placing commit or roll back
  together.
- **A second tap never hands an item out twice.** Handing out an item that is already done succeeds
  and keeps the first time. Putting back an item that is still open is refused naming the item, and a
  selection that holds an id outside this station changes nothing. Both actions re-read the whole
  queue after the save and answer with it. Two simultaneous taps on the same item keep the first
  tap's time, because every write request waits for the one before it (`BEGIN IMMEDIATE` in
  `RequestTransactionFilter`) and the second then finds the item already done.
- **The two columns.** The first holds every order with open items, the second the as-it-comes
  orders the employee has not hidden from it. A fully done order leaves both and appears in the done
  view. `IsHiddenFromAsItComesQueue` is the employee's own decision; it only removes the order from
  the second column and means something only for an `AsItComes` station order.
- **The delivery mode is fixed once the order is sent.** `Together` hands the whole station order
  over at once, `AsItComes` hands each item out as soon as it is ready. A station order set to
  together is done when its last item is done.
- **An order's status is derived from its items and never stored**: `Open` when none is done,
  `PartiallyFulfilled` when some are, `Fulfilled` when all are, counting only the items that are not
  cancelled. An order has no stored total either.
- **Every station order carries two numbers**, the festival's global order number and the station's
  own number, so a gap shows in the station's list. `Festival.NextOrderNumber` and
  `FestivalStation.NextStationOrderNumber` count per festival, every festival starts at 1 by itself,
  and nothing ever sets a counter back. Removing a station from a festival deletes the row that holds
  its counter, so adding the station back continues from the highest `StationOrderNumber` already
  used at that festival instead of repeating. A station never receives two station orders of one
  order (unique on `(OrderId, StationId)`).
- **Estimates are computed, never stored.** The laptop reports per station the minutes still
  queued, counted from the open items' production minutes (a missing value counts as zero) and
  leaving out articles prepared independently of the queue. The phone adds the item's own minutes,
  and for an independent article it shows that time alone.
- **Nobody is notified when an item is done.** The card and the confirmation carry the table name,
  and whichever waiter passes the station takes the tray.

## Tests

- `GastronomyApp.Core.Tests/Services`: `StationWaitingTimesTest` (the pure sums),
  `OrderItemLifecycleTest` (the verdict for every current and asked state, who may set it, the first status
  made current, the acknowledgement read without the status before it).
- `GastronomyApp.Infrastructure.Tests`: `Services/EstimateServiceTest` (the list and the quote on the real DbContext: open work of the same festival and station only, a handed-out item left out, independent articles, refusals), `Configurations/StationOrderConfigurationTest`,
  `Configurations/OrderItemStatusConfigurationTest` (the check constraint, no fork in a history, the
  cascade from a deleted order, no item without a current status),
  `Persistence/ReleasedDatabaseUpgradeTest` (an open and a handed-out item of a released database get
  their histories, and 20000 items get theirs within a time bound),
  `Queries/OrderItemQueriesTest` (the filters and the checks, in memory and translated by EF Core
  in a `Where`, nested in an `Any` and in a projection),
  `Configurations/OrderNumberIndexTest`, `Services/SequenceNumberAllocatorTest`,
  `Services/StationQueueServiceTest` (every read and action on the real DbContext: both columns, the
  order of the cards and of the lines, the refusals and their order, nothing stored on a refusal, the
  four statements of the queue read, the status rows a hand-out and a put-back append, cancelled
  items on and off the card, the acknowledgement),
  `Services/ItemsToHandOutLookupTest` and
  `Mapping/StationQueueMappingTest`.
- `GastronomyApp.Api.Tests`: `Endpoints/StationQueueEndpointsTest` (with two simultaneous taps on
  one item), `Endpoints/EstimateEndpointsTest`, `Endpoints/OrderEndpointsTest` (a failing second save
  of placing stores nothing). Test seeds give items their history through
  `Infrastructure.Tests/TestSupport/OrderItemStatusSeeder.cs` (`Open`, `OpenAndHandOutOnSaveWhenAHandOutTimeIsGiven`
  and `SaveAsync` save the two-step way; `OpenAndMakeCurrent` is for items that are never saved) and
  `Api.Tests/TestSupport/StoredItemHandOut.cs`.
- `frontend/tests/station`: the views `StationPage.screen`, `.markingDone`, `.doneView`,
  `.cancellation` (the struck-through item, the acknowledgement, a lost answer and a refusal) and
  `.connection` on `stationPageFixture.ts`, the components, the composables and the store specs
  (`station.queue`, `station.done`, `station.staleAnswers`, `stationChangeListening`,
  `stationSnapshotOrdering`); `tests/support/station.ts` (`item`, `cancelledItem`);
  `frontend/tests/shared/core/stationBoard.spec.ts` and `orderItemStates.spec.ts`;
  `frontend/tests/phone/core/estimates.spec.ts` and `phone/stores/estimates.spec.ts`.
