# Handing out at a station

The laptop splits every order by station (the kitchen, a bar). Each station has one tablet, set up
like a phone, and its page shows two columns: every order that still has open items, and the orders
the waiter wanted handed out item by item as each is ready. The staff select the items they hand
out, one or several at a time, and mark them done after a confirmation that repeats the table name.
A fully done order leaves both columns and appears in the done view, where a mistaken tap can be put
back. The waiter's phone shows a waiting time per item and per station order, worked out from the
station's queue.

## Where the code lives

Backend, under `backend/`:

- `GastronomyApp.Core/Entities/StationOrder.cs`: the part of an order that belongs to one station,
  with `StationOrderNumber` (the number that station shows for it), `DeliveryMode` and
  `IsHiddenFromAsItComesQueue`. `OrderItem.FulfilledAtUtc` records the hand-out. An article's
  `CatalogItem.ProductionMinutes` and `IsQueueIndependent` feed the estimates.
- `Core/Services`: `StationQueueService`, `StationQueueChangeService`, `StationQueueWriter`,
  `OrderItemFulfillmentService` and `StationOrderService`; the estimates are `EstimateService` (`ListAsync`, `QuoteAsync`) on `IGastronomyDatabase`, with the pure `StationWaitingTimes` beside it, which sums the open work of one station (`Results/OpenWork`) for an article (`Results/ArticleTiming`). `Core/Mapping/EstimateMapping.cs` projects the timed assignments, the articles and the open work. Refusals:
  `Core/Refusals/Refusal.StationQueue.cs`. The numbers come from
  `Infrastructure/Repositories/SequenceNumberAllocator.cs`.
- Routes in `GastronomyApp.Api/Endpoints/StationEndpoints.cs`. For the station tablet:
  `GET /api/station/orders`, `GET /api/station/orders/fulfilled`, `POST /api/station/items/fulfill`,
  `POST /api/station/items/unfulfill` and `POST /api/station/orders/{stationOrderId}/hide`
  (`StationQueueHandler`, `StationFulfillmentHandler`). For the phone: `GET /api/estimates` and
  `POST /api/estimates/quote` (`EstimateService` directly). Wire records in `GastronomyApp.Contracts/Stations`,
  the estimate records in `Contracts/Orders`, `DeliveryMode` and `OrderStatus` in `Contracts/Enums`.

Frontend, under `frontend/src/`:

- `station/views/StationPage.vue` with `station/components/` (`StationOpenBoard.vue`,
  `StationOrderCard.vue`, `StationFulfilledCard.vue`, `StationDoneDialog.vue`),
  `station/composables/` and `station/stores/station.ts`. `shared/core/stationBoard.ts` builds the
  lines, the counts and the selection.
- The phone: `phone/core/estimates.ts` and `phone/stores/estimates.ts` for the waiting times; the
  delivery mode is chosen on the summary (see the `taking-an-order` page).

## Invariants

- **An item is open or done.** Done is `OrderItem.FulfilledAtUtc`, set when the station hands the
  item out and cleared again when a mistaken tap is put back. There is no in-between production state
  and no status log.
- **The two columns.** The first holds every order with open items, the second the as-it-comes
  orders the employee has not hidden from it. A fully done order leaves both and appears in the done
  view. `IsHiddenFromAsItComesQueue` is the employee's own decision; it only removes the order from
  the second column and means something only for an `AsItComes` station order.
- **The delivery mode is fixed once the order is sent.** `Together` hands the whole station order
  over at once, `AsItComes` hands each item out as soon as it is ready. A station order set to
  together is done when its last item is done.
- **An order's status is derived from its items and never stored**: `Open` when none is done,
  `PartiallyFulfilled` when some are, `Fulfilled` when all are. An order has no stored total either.
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

- `GastronomyApp.Core.Tests/Services`: `StationQueueServiceTest`, `StationQueueChangeServiceTest`,
  `StationQueueWriterTest`, `OrderItemFulfillmentServiceTest`, `StationOrderServiceTest` and
  `StationWaitingTimesTest` (the pure sums).
- `GastronomyApp.Infrastructure.Tests`: `Services/EstimateServiceTest` (the list and the quote on the real DbContext: open work of the same festival and station only, a handed-out item left out, independent articles, refusals), `Configurations/StationOrderConfigurationTest`,
  `Configurations/OrderNumberIndexTest`, `Repositories/StationOrderRepositoryTest` and
  `Repositories/SequenceNumberAllocatorTest`.
- `GastronomyApp.Api.Tests`: `Endpoints/StationQueueEndpointsTest`, `Endpoints/EstimateEndpointsTest`,
  `Mapping/StationQueueMappingTest`.
- `frontend/tests/station`: the views `StationPage.screen`, `.markingDone`, `.doneView` and
  `.connection` on `stationPageFixture.ts`, the components, the composables and the store specs
  (`station.queue`, `station.done`, `station.staleAnswers`, `stationChangeListening`,
  `stationSnapshotOrdering`); `tests/support/station.ts`; `frontend/tests/shared/core/stationBoard.spec.ts`;
  `frontend/tests/phone/core/estimates.spec.ts` and `phone/stores/estimates.spec.ts`.
