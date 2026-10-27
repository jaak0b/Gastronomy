# Showing a table's orders

A waiter taps a table on the plan and "Bestellungen anzeigen", and the phone lists every order of
that table with its items, read only. Each item has two columns, "Bezahlt" and "Ausgegeben", with a
green check when it is done and a red cross when it is not; a cancelled item is struck through.
Each order shows its done counter ("x / y zubereitet"), which appears nowhere else on the phone. A
filter at the top narrows the list by order status, by payment status and by whether cancelled items
show.

## Where the code lives

Frontend, under `frontend/src/phone/`:

- `views/OrderOverview.vue` (route `/order-overview`, route and screen name `orderOverview`): the
  title "Bestellungen", "Neu laden", the filter, the sentence when the table could not be loaded, one
  card per order, and `phone.orderOverview.messages.noOrderMatches` when the laptop sends no order. It
  goes back to the plan when no table is open, as after a reload, and closes the table when the
  waiter leaves. The header shows the table's current name and "Tischplan".
- `components/orderOverview/OrderOverviewFilter.vue`: chips for the order status ("Alle", "Offen",
  "Teilweise ausgegeben", "Ausgegeben"), chips for the payment status ("Alle", "Bezahlt", "Nicht
  bezahlt") and the box "Stornierte Positionen anzeigen". `OverviewOrderCard.vue`: one order with its
  number, who took it and when, the done counter (`producedCountIn`, `notCancelledItemCountIn` from
  `core/openItems.ts`), the two column headings and per item the name, the note, and the two marks
  (`isHandedOut` and `isCancelled` from `shared/core/orderItemStates.ts`, the shared
  `cancelled-item` class).
- `stores/orderOverview.ts` (`useOrderOverviewStore`: `openTable`, `closeTable`, `load`,
  `changeFilter`, `listen`, `tableId`, `filter`, `report`, `loadFailureKey`; `EVERY_ORDER` and the
  type `OrderOverviewFilter`). `views/TablePlan.vue` opens a table with `openTable`.
- `api/tableSearch.ts` (`searchTheTable`) is the one call of `POST /api/open-items/tables/{tableId}/search`,
  shared with the settle and cancel view: it answers `found`, `refused` with the message key (a 404
  becomes `phone.openItems.errors.tableCouldNotBeLoaded`) or `unanswered`.
- Texts: `phone.orderOverview.*`, plus `phone.openItems.labels.paid`, `phone.openItems.labels.order`,
  `phone.openItems.actions.reload`, `phone.openItems.errors.*` and `common.labels.doneCounter`.

The backend is the table search of the `settling-a-table` page.

## Invariants

- **Read only.** Nothing on this screen can be ticked, settled or cancelled.
- **The filter is sent as picked.** The screen opens with every order (`orderStatus` and
  `paymentStatus` null, `includeCancelled` true) and sends all three options on every search; the
  laptop filters, the phone shows what comes back. The order status keeps or drops whole orders, the
  other two keep or drop items. Closing the table resets the filter.
- **A delayed answer never overwrites a newer one.** Every load, a filter change included, takes a
  number from the latest request gate, and the screen loads again with its own filter on
  `OrdersChanged`, `ConfigurationChanged` and `GroupsJoined`.
- **No colour beyond the two columns.** Green check and red cross are the only colours; the chips are
  neutral.

## Tests

- `frontend/tests/phone/views/OrderOverview.spec.ts` (the column headings in German and English,
  the checks and crosses, a cancelled item struck through, the done counter, no order matching, no
  table after a reload, the filter's first search, each order status, each payment status, the
  cancelled box, two picks together, the chips in English).
- `frontend/tests/phone/stores/orderOverview.spec.ts` (the first search, a filter change, the gate, a
  404, no answer, closing, the reload after `GroupsJoined` and with the picked filter on
  `OrdersChanged`).
- `TablePlan.sheet.spec.ts` for "Bestellungen anzeigen", `AppHeader.spec.ts` for the header,
  `tests/shared/core/landing.spec.ts`, `appTitle.spec.ts` and `tests/shared/router/bootPath.spec.ts`
  for the route, `tests/phone/core/openItems.settling.spec.ts` for the done counter.
