# Showing a table's orders

A waiter taps a table on the plan and "Bestellungen anzeigen", and the phone lists every order of
that table with its items, read only. The orders are drawn by the shared order list that Kassieren
and Stornieren use too: each item has two columns, "Bezahlt" and "Ausgegeben", with a green check
when it is done and a red cross when it is not, and a cancelled item is struck through. Two dropdowns
at the top ("Bestellungen", "Positionen") and the box "Stornierte Positionen anzeigen" narrow the
list; Kassieren and Stornieren show the same filter. The info button at the end of every item opens
its details: the price, then its whole history with date and time on every step. No row shows a
price.

## Where the code lives

Frontend, under `frontend/src/phone/`:

- `views/OrderOverview.vue` (route `/order-overview`, route and screen name `orderOverview`): the
  title "Bestellungen" (`components/orderList/OrderListTitle.vue`, the one-line bold title of all
  three screens), the filter, the sentence when the table could not be loaded, one
  `OrderListCard` per order without a purpose (no tick boxes; a tap on a row does nothing), and
  `phone.orderOverview.messages.noOrderMatches` when the laptop sends no order. It goes back to the
  plan when no table is open, as after a reload, and closes the table when the waiter leaves. The
  bar at the top (`components/header/AppHeader.vue`, see the `table-plan` page) shows the table's
  current name, "Tischplan" and the reload, an icon whose tooltip says "Neu laden".
- `components/orderList/OrderListFilter.vue` is the one filter of all three screens: two
  `v-select`s side by side, "Bestellungen" ("Alle", "Offen", "Teilweise ausgegeben", "Ausgegeben")
  and "Positionen" ("Alle", "Bezahlt", "Nicht bezahlt"), and below them the box "Stornierte
  Positionen anzeigen". It takes a `TableSearchFilter` (`core/openItems.ts`, the three options of
  the search with none left out) and emits `change` with the new one. "Alle" is the option value
  `every` inside the component and goes out as `null`.
- `components/orderList/OrderListCard.vue` is the shared order list, one card per order, used by
  this screen and by `components/openItems/TableOrders.vue` (Kassieren and Stornieren). Props:
  `order`, `language`, and for the two acting screens `purpose` (a `SelectionPurpose`) and
  `selectedItemIds`; it emits `toggle-item` and `set-whole-order`. One CSS grid holds the whole
  card, so every row shares its columns: the header line (with a purpose, the whole-order tick box
  in the first column, then the order number and "{time} · {name}"), the column heading line (the
  done counter "x / y zubereitet" on the left from the wire's `handedOutItemCount` and
  `notCancelledItemCount`, then "Bezahlt" and "Ausgegeben"), and one line per item: the name with
  its note, the two marks, and the details button (`data-test="item-details"`, the
  `mdi-information-outline` icon on `shared/components/BaseIconAction.vue`, named
  `phone.itemHistory.actions.showDetails` in its tooltip). When at least one item can be ticked
  (`canBeSelected`), the first column is a narrow tick box column on every line, left empty where
  `canBeSelected` says no, so the names line up; a card with nothing to tick has no such column and
  starts at its left edge like the overview card. The done counter is small grey secondary text. The two mark columns are as wide as their headings and the gaps are small, so the name and
  its note get the rest of a phone's width. The "Bezahlt" mark is `settledAtUtc !== null`; the
  "Ausgegeben" mark is `isHandedOut(state)`, or `wasHandedOutWhenCancelled` for a cancelled item. A
  cancelled item's name carries the shared `cancelled-item` class. With a purpose, a tap anywhere on
  a line whose item can be ticked ticks or unticks it (`toggle-item`), and a tap on the tick box
  itself ticks once; without a purpose, or on an item that cannot be ticked, a tap on the line does
  nothing. The details button opens `ItemHistoryDialog` and never ticks.
- `components/orderList/ItemHistoryDialog.vue`: the item's details on `BaseFullPageDialog`, titled
  with the item name, its note below, then its price from the list's item
  (`phone.itemHistory.labels.price`, "Preis: 3,50 €"), then the history. It loads once when it
  opens, through the latest request
  gate, and never on hub events: a spinner while waiting, the laptop's refusal
  (`errors.itemHistory.unknownItem`), or `phone.openItems.errors.loadFailed` with "Neu laden" when no
  answer came; "Schließen" or the back button closes it. The timeline shows per step the date and time in the format
  of the order's header (`formatFestivalMoment`, "27.10.2026, 23:05"), the event and who made it; the payment adds the amount with "bar" or "mit Karte" (the amount alone for `none` or
  a method the laptop never stored) and "Grund: …" when a reason was typed.
- `core/itemHistory.ts` (`historyEntriesOf`): turns the `OrderItemHistoryView` into the timeline.
  The first status change is the order ("Bestellt"), a later `open` is a put back ("Zurückgelegt"),
  and the payment is placed among the status changes by its time.
- `api/itemHistory.ts` (`readTheItemHistory`) is the one call of
  `GET /api/open-items/items/{orderItemId}/history`: `found`, `refused` with the message key, or
  `unanswered`.
- `stores/orderOverview.ts` (`useOrderOverviewStore`: `openTable`, `closeTable`, `load`,
  `changeFilter`, `listen`, `tableId`, `filter`, `report`, `loadFailureKey`; `EVERY_ORDER`).
  `views/TablePlan.vue` opens a table with `openTable`.
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

- **Read only.** Nothing on this screen can be ticked, settled or cancelled, and a tap on a row does
  nothing.
- **No price in a row.** On all three screens the price of an item is shown only in its details.
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
  out and after payment, the counter from the wire with and without a filter, no tick box, no order
  matching, no table after a reload, the filter's first search, each order status, each payment
  status, back to "Alle", the cancelled box, two picks together, the dropdown names and their
  options in English, the details button opening the price and the history, a tap on the row
  opening nothing).
- `frontend/tests/phone/components/orderList/OrderListCard.spec.ts` (each mode's columns, the
  heading line, the empty tick box cells, the whole-order box in the header line, the details
  button on every screen opening the details without ticking, closing them, a row tap ticking on
  both acting screens, ticking nothing on an item that cannot be ticked and opening nothing on the
  overview, a tap on the tick box ticking once) and `ItemHistoryDialog.spec.ts` (the request, the
  spinner, the title and note, the price in German and English, the timeline with date and time
  and authors, the payment in German and
  English with its reason, a payment without a method, the refusal in both languages, no answer and
  loading again, closing); `frontend/tests/phone/core/itemHistory.spec.ts`.
- `frontend/tests/phone/stores/orderOverview.spec.ts` (the first search, a filter change, the gate, a
  404, no answer, closing, the reload after `GroupsJoined` and with the picked filter on
  `OrdersChanged`).
- `TablePlan.sheet.spec.ts` for "Bestellungen anzeigen", `AppHeader.spec.ts` for the bar and its reload,
  `tests/shared/core/landing.spec.ts`, `appTitle.spec.ts` and `tests/shared/router/bootPath.spec.ts`
  for the route.
