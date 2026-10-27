# Showing a table's orders

A waiter taps a table on the plan and "Bestellungen anzeigen", and the phone lists every order of
that table with its items, read only. The orders are drawn by the shared order list that Kassieren
and Stornieren use too: each item has two columns, "Bezahlt" and "Ausgegeben", with a green check
when it is done and a red cross when it is not, and a cancelled item is struck through. Two dropdowns
at the top ("Bestellungen", "Positionen") and the box "Stornierte Positionen anzeigen" narrow the
list. Tapping an item opens its whole history.

## Where the code lives

Frontend, under `frontend/src/phone/`:

- `views/OrderOverview.vue` (route `/order-overview`, route and screen name `orderOverview`): the
  title "Bestellungen", "Neu laden", the filter, the sentence when the table could not be loaded, one
  `OrderListCard` per order without a purpose (no tick boxes, no prices), and
  `phone.orderOverview.messages.noOrderMatches` when the laptop sends no order. It goes back to the
  plan when no table is open, as after a reload, and closes the table when the waiter leaves. The
  header shows the table's current name and "Tischplan".
- `components/orderOverview/OrderOverviewFilter.vue`: two `v-select`s side by side, "Bestellungen"
  ("Alle", "Offen", "Teilweise ausgegeben", "Ausgegeben") and "Positionen" ("Alle", "Bezahlt",
  "Nicht bezahlt"), and below them the box "Stornierte Positionen anzeigen". "Alle" is the option
  value `every` inside the component and goes out as `null`.
- `components/orderList/OrderListCard.vue` is the shared order list, one card per order, used by
  this screen and by `components/openItems/TableOrders.vue` (Kassieren and Stornieren). Props:
  `order`, `language`, and for the two acting screens `purpose` (a `SelectionPurpose`) and
  `selectedItemIds`; it emits `toggle-item` and `set-whole-order`. One CSS grid holds the whole
  card, so every row shares its columns: the header line (with a purpose, the whole-order tick box
  in the first column, then the order number and "{time} · {name}"), the column heading line (the
  done counter "x / y zubereitet" on the left from the wire's `handedOutItemCount` and
  `notCancelledItemCount`, then "Bezahlt" and "Ausgegeben"), and one line per item. With a purpose
  the first column is the tick box column on every line, left empty where `canBeSelected` says no,
  so the names line up, and a price column stands before the marks. The "Bezahlt" mark is
  `settledAtUtc !== null`; the "Ausgegeben" mark is `isHandedOut(state)`, or
  `wasHandedOutWhenCancelled` for a cancelled item. A cancelled item's name and price carry the
  shared `cancelled-item` class. Tapping a line opens `ItemHistoryDialog`; tapping its tick box only
  ticks.
- `components/orderList/ItemHistoryDialog.vue`: the item's history on `BaseFullPageDialog`, titled
  with the item name and its note below. It loads once when it opens, through the latest request
  gate, and never on hub events: a spinner while waiting, the laptop's refusal
  (`errors.itemHistory.unknownItem`), or `phone.openItems.errors.loadFailed` with "Neu laden" when no
  answer came; "Schließen" closes it. The timeline shows per step the time of day, the event and
  who made it; the payment adds the amount with "bar" or "mit Karte" (the amount alone for `none` or
  a method the laptop never stored) and "Grund: …" when a reason was typed.
- `core/itemHistory.ts` (`historyEntriesOf`): turns the `OrderItemHistoryView` into the timeline.
  The first status change is the order ("Bestellt"), a later `open` is a put back ("Zurückgelegt"),
  and the payment is placed among the status changes by its time.
- `api/itemHistory.ts` (`readTheItemHistory`) is the one call of
  `GET /api/open-items/items/{orderItemId}/history`: `found`, `refused` with the message key, or
  `unanswered`.
- `stores/orderOverview.ts` (`useOrderOverviewStore`: `openTable`, `closeTable`, `load`,
  `changeFilter`, `listen`, `tableId`, `filter`, `report`, `loadFailureKey`; `EVERY_ORDER` and the
  type `OrderOverviewFilter`). `views/TablePlan.vue` opens a table with `openTable`.
- `api/tableSearch.ts` (`searchTheTable`) is the one call of `POST /api/open-items/tables/{tableId}/search`,
  shared with the settle and cancel view: it answers `found`, `refused` with the message key (a 404
  becomes `phone.openItems.errors.tableCouldNotBeLoaded`) or `unanswered`.
- Texts: `phone.orderOverview.*`, `phone.itemHistory.*`, `errors.itemHistory.unknownItem`, plus
  `phone.openItems.labels.paid`, `phone.openItems.labels.order`, `phone.openItems.actions.reload`,
  `phone.openItems.errors.*`, `common.labels.doneCounter` and `common.actions.close`.

The backend is the table search of the `settling-a-table` page. Its answer carries each order's
done counter as two numbers the laptop counts over every item of the order that is not cancelled,
whatever the filter (`NotCancelledItemCount`, `HandedOutItemCount`), and per item
`WasHandedOutWhenCancelled`, whether a cancelled item had been handed out at the moment it was
cancelled. One item's history (who took the order, every status change with its author, the
payment with its reason) is `GET /api/open-items/items/{orderItemId}/history` on the same page.

## Invariants

- **Read only.** Nothing on this screen can be ticked, settled or cancelled, and it shows no prices.
- **The counter is the laptop's.** The phone shows the two counts of the wire and never counts the
  items it received, because a filter can leave items out.
- **The filter is sent as picked.** The screen opens with every order (`orderStatus` and
  `paymentStatus` null, `includeCancelled` true) and sends all three options on every search; the
  laptop filters, the phone shows what comes back. The order status keeps or drops whole orders, the
  other two keep or drop items. Closing the table resets the filter.
- **A delayed answer never overwrites a newer one.** Every load, a filter change included, takes a
  number from the latest request gate, and the screen loads again with its own filter on
  `OrdersChanged`, `ConfigurationChanged` and `GroupsJoined`.
- **No colour beyond the two columns.** Green check and red cross are the only colours; no row is
  tinted, and the timeline of the history is neutral.

## Tests

- `frontend/tests/phone/views/OrderOverview.spec.ts` (the column headings in German and English,
  the checks and crosses, a cancelled item struck through, the marks of an item cancelled after hand
  out and after payment, the counter from the wire with and without a filter, no tick box and no
  price, no order matching, no table after a reload, the filter's first search, each order status,
  each payment status, back to "Alle", the cancelled box, two picks together, the dropdown names and
  their options in English, a tap on an item opening its history).
- `frontend/tests/phone/components/orderList/OrderListCard.spec.ts` (each mode's columns, the
  heading line, the empty tick box cells, the whole-order box in the header line, the history on a
  row tap and not on a tick box tap, closing it) and `ItemHistoryDialog.spec.ts` (the request, the
  spinner, the title and note, the timeline with times and authors, the payment in German and
  English with its reason, a payment without a method, the refusal in both languages, no answer and
  loading again, closing); `frontend/tests/phone/core/itemHistory.spec.ts`.
- `frontend/tests/phone/stores/orderOverview.spec.ts` (the first search, a filter change, the gate, a
  404, no answer, closing, the reload after `GroupsJoined` and with the picked filter on
  `OrdersChanged`).
- `TablePlan.sheet.spec.ts` for "Bestellungen anzeigen", `AppHeader.spec.ts` for the header,
  `tests/shared/core/landing.spec.ts`, `appTitle.spec.ts` and `tests/shared/router/bootPath.spec.ts`
  for the route.
