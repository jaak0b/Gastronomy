# Settling a table

A waiter taps a table on the plan and "Position kassieren", and the phone shows that table's items
that are neither paid nor cancelled, order by order, in the shared order list of the
`showing-a-tables-orders` page: the "Bezahlt" and "Ausgegeben" marks, the done counter and the
details button that opens an item's price and history, plus a tick box column. Above the orders
sits the overview's filter, set to "Positionen: Nicht bezahlt" with "Stornierte Positionen
anzeigen" off every time the screen opens; the waiter can change it to see paid or cancelled items,
and a paid one and a cancelled one (struck through) have an empty tick box cell. The waiter ticks
what the guest pays for, by its tick box or by tapping its row, taps Kassieren under the selected
total and, on the amount page, confirms what the table actually handed over, in cash or by card.

The same view cancels items: "Position stornieren" on the table's action sheet opens it in cancel
mode, which opens its filter on "Positionen: Alle" with "Stornierte Positionen anzeigen" off, so it
shows the items that are not cancelled, and lets every item that is not cancelled be ticked, paid
ones included. The footer counts the
ticked items and its "Stornieren" opens a page that lists them, says how much cash the guest gets
back for the paid ones, and cancels them on confirming. A cancellation is final.

## Where the code lives

Frontend, under `frontend/src/phone/`:

- `views/OpenItems.vue` (route `/open-items`) is the settle view of one table, titled "Kassieren":
  the title (`components/orderList/OrderListTitle.vue`), the shared filter
  (`components/orderList/OrderListFilter.vue`, see the `showing-a-tables-orders` page), the sentence when the table could not be loaded, the sentence when nothing is left
  (`phone.openItems.messages.nothingToSettle` or `nothingToCancel`, replaced by `phone.orderOverview.messages.noOrderMatches` once the waiter changed the filter), the notice after settling, the table's orders and
  the bottom bar with the selected total and the one Kassieren button that opens the amount page. It
  goes back to the plan when no table is open, as after a reload, and closes the table when the
  waiter leaves. In cancel mode it is titled "Stornieren" (`phone.openItems.cancellingTitle`), the footer counts the selection
  (`phone.openItems.labels.selectedCount`) and its "Stornieren" opens `CancelItemsDialog.vue`. The bar at the bottom (`components/header/AppHeader.vue`, see the
  `table-plan` page) shows the table's current name, "Tischplan" and the reload "Neu laden"
  that the refusal notices name.
- `components/openItems/`: `TableOrders.vue` (the box for the whole table, then one shared
  `components/orderList/OrderListCard.vue` per order with the view's purpose and ticks, see the
  `showing-a-tables-orders` page; the whole-order box sits in the order's header line, an item is
  ticked through its own tick box or a tap anywhere on its row, and its details button opens its
  price and history without ticking),
  `SettleNotice.vue`, and
  `AmountPaidDialog.vue`, the amount page: the selected total, the fields, the reason of a refusal,
  then Bar kassieren and Mit Karte kassieren, or Kassieren alone when nothing is to be paid, "Neu
  laden" while a reason shows (reloads and goes back to the table), and Abbrechen.
  `CancelItemsDialog.vue` is the cancel page: the selected items with price and "Bezahlt", the cash
  to give back when it is above zero (`phone.openItems.messages.cashToReturn`), the reason of a
  refusal, then Stornieren, "Neu laden" while a reason shows (reloads and goes back to the table),
  and Zurück. Both pages are built on `components/BaseFullPageDialog.vue`, the phone's full-screen
  page: a `title` prop, the content in the default slot, the buttons in the `actions` slot, each
  button the full width, and the page kept above the keyboard (`useKeyboardInset`). Both pages'
  "Neu laden" call `closeThePageAndReload` in `OpenItems.vue`, which closes the open page and loads
  the table through the latest request gate. Neu laden, Zurück and Abbrechen all go through
  `closeThePage`, which clears the store's notice (`dismissNotice`), so a refusal's reason never
  stays on the table or comes back when the page opens again.
- `components/AmountPaidFields.vue`: the amount field, filled in with the selected total, and the
  reason field that appears once the amount falls short.
- `core/openItems.ts`: the selection over one table's `TableOrderReportView` for a
  `SelectionPurpose`, `settling` or `cancelling` (`selectableItemsIn`, `selectableItemIdsIn`,
  `selectableItemIdsInOrder`, `selectedItems`, `withItemToggled`, `withWholeTable`, `withWholeOrder`,
  `withoutItemsThatAreGone`), `defaultFilterFor(purpose)` (the filter each screen opens with),
  `selectedAmountCents`, `cashToReturnCents`, `isPaymentNoticeNeeded`,
  `isAPaymentMethodNeeded`, `canTheAmountBeSettled` and `noticeAfterSettling`; the hand-out and
  selection states are the generated `ShareState`. `canBeSelected(item, purpose)` is the one test
  of what can be ticked (settling: not settled and not cancelled; cancelling: not cancelled); a row
  shows its tick box, and a tap on it ticks, exactly when it says yes; otherwise the cell stays
  empty and a tap does nothing. A cancelled row's name carries the shared class `cancelled-item` (`shared/styles/cancelledItems.css`,
  the same struck-through look as on the station tablet). No row is tinted.
  `core/settlementSplit.ts` spreads the amount paid over the ticked lines.
- `stores/openItems.ts` (`useOpenItemsStore`: `openTable`, `openTableAndSelectItemsOnceLoaded`,
  `openTableToCancelItems`, `closeTable`, `load`, `changeFilter`, `filter`, `listen`, `toggleItem`, `setWholeTable`,
  `setWholeOrder`, `settle`, `cancelSelectedItems`, `purpose`, `report`, `loadFailureKey`, `notice`,
  `isSettling`, `isCancelling`, `itemsToCancel`, `cashToReturnForTheItemsToCancel`) and `composables/useSettleDialog.ts`
  (`amountPaidIsOpen`, `settleTheAmountPaid`). `views/TablePlan.vue` opens a table with `openTable` or
  `openTableToCancelItems`;
  `composables/useSendAndLeave.ts` opens it with the items of an order just sent ticked.
- Texts: `phone.openItems.*` in `shared/i18n/de.json` and `en.json`.

Backend, under `backend/`:

- `GastronomyApp.Api/Endpoints/OpenItemEndpoints.cs` under `/api/open-items`: `POST /tables/{tableId}/search`,
  `GET /items/{orderItemId}/history`, `POST /settle` and `POST /cancel`, all calling `OpenItemsService` directly. Wire records in `GastronomyApp.Contracts/OpenItems/`
  (`TableOrderSearchRequest` with the three search options, `TableOrderReportView` with the table's `TableId` and current `TableName`,
  `TableOrderRecordView` with the order's `NotCancelledItemCount` and `HandedOutItemCount`,
  `TableOrderRecordItemView` with each item's `State`, `StateSetAtUtc`, `WasHandedOutWhenCancelled` and
  `ChargedPriceCents`; `OrderItemHistoryView` and `OrderItemStatusChangeView`; `SettleItemsRequest`, `SettleLineRequest`, `SettlementView`;
  `CancelItemsRequest`, `CancellationView`).
- `GastronomyApp.Core/Services/OpenItemsService.cs` is the one service of the area on
  `IGastronomyDatabase`: `SearchTableAsync` reads one table with the orders its options keep and
  every item of those orders, and answers the `TableOrderReportView` (running festival lookup, then
  one table statement, so two statements; the order status filter and the test that an order keeps
  at least one item run in SQL inside the filtered include, through `OrderQueries.Status` and
  `OrderItemQueries.MatchesTableSearch`; the same `MatchesTableSearch` then picks the items to send
  in memory, into a `Core/Results/SearchedOrder` per order, because each order's two counts are
  taken over all of its items that are not cancelled, whatever the options; the item load includes
  `CurrentStatus.PreviousStatus`, which `OrderItemQueries.WasHandedOutWhenCancelled`
  reads; the lines of an order are sorted
  in memory with `StringComparer.OrdinalIgnoreCase`, because the item name and note carry no
  `NOCASE` collation). `ReadItemHistoryAsync` projects one item with `ProjectToType` into the
  `OrderItemHistoryView`: who took the order and when, every status in `CreatedAtUtc` order with
  the waiter's or the station's name, and the payment as five nullable fields (`SettledAtUtc`,
  `ChargedPriceCents`, `PaymentMethod`, `SettledByStaffMemberName` and `PaymentNotice`, the reason
  typed when less than the price was collected; all null when unpaid, the reason also null when
  nothing was typed;
  `PaymentMethod` is also null on items paid before 2026-10-02, when the column was added). It
  looks the item up by id alone, without the running festival, and refuses an id the laptop does
  not hold with `errors.itemHistory.unknownItem` (422, `Refusal.ItemHistory.UnknownItem`). `SettleAsync` checks and applies a settlement and
  answers the `SettlementView`. The check order is: running festival, the waiter named (a program
  error, thrown), duplicate lines, the payment method against the money paid, unknown lines,
  cancelled items (`errors.settlement.itemWasCancelled`, naming each), missing notices, one table.
  `CancelAsync` cancels items of one table through `OrderItemLifecycle`, whose `JudgeChange` decides
  which items are already cancelled, and answers the
  `CancellationView` with the sum of the cancelled items already paid; its check order is: running
  festival, the waiter named (thrown), an empty selection, duplicates, unknown items, items already
  cancelled (`errors.cancellation.alreadyCancelled`, naming each, so two waiters never both hand the
  money back), one table. The two actions share `DuplicatesIn` and `TableIdsOf`, private methods
  of the service that answer ids; each action turns them into its own refusals. Nothing is stored on a refusal. Refusals are in
  `Core/Refusals/Refusal.Settlement.cs`, `Refusal.Cancellation.cs` and `Refusal.ItemHistory.cs`.
  The wire records are mapped by `Core/Mapping/OpenItemMapping.cs` (lines, a `SearchedOrder` with
  its counts, an item's history with its status changes, the settlement); the service builds the
  table report from the table's id, its current name and the mapped orders.

## Invariants

- **Cancel mode reuses the settle view.** The purpose decides only the filter the screen opens with,
  what can be ticked, the empty sentence, the footer, the title and which page the footer opens;
  the orders, the rows, the ticks, the filter and the reload are the same. Opening a table with
  `openTable` always returns to settling with its own filter, and `openTableToCancelItems` to
  cancelling with its own, so a pick never outlives the screen.
- **The cancel page stays open on a refusal and on silence.** A refusal shows the laptop's reason
  (`errors.cancellation.*`, naming the item) and the "Neu laden" those texts name; a lost answer
  says it is unclear whether the cancellation arrived (`phone.openItems.errors.cancelAnswerNeverCame`).
  Stornieren stays available; sending again after a lost answer is refused only for the items
  already cancelled. Nothing is sent again without the waiter. An accepted cancellation clears the
  selection, reloads the table through the latest request gate, so a load already on its way can
  never put a cancelled item back, and closes the page.
- **The cash shown is what was charged.** The cancel page adds up `chargedPriceCents` of the paid
  items in the selection, always as cash, and says nothing when that is zero.

- **Every settlement names the amount paid.** The Kassieren button opens the amount page for every
  selection, a free one included, and the request carries a paid price for every line
  (`splitSettlement`). Giving something away is that same act with the amount set to zero, never a
  second kind of settlement.
- **What a settlement stores.** `OrderItem.SettledAtUtc` is the paid flag (null means still open, so
  a flag and a timestamp can never disagree; every reader asks `OrderItemQueries.IsSettled`), `ChargedPriceCents` is what was actually collected, and
  `PaymentNotice` is the reason typed when less than the displayed price was collected. The phone
  sends one price per line and the laptop stores exactly that price; it never spreads an amount
  across items. What a table still owes is derived from these on every read, never stored.
- **The full amount is filled in.** The amount field starts at the selected total, so the ordinary
  case is Kassieren and then Bar or Karte.
- **A short amount needs a typed reason.** Below the selected total the reason field appears and the
  confirm buttons stay disabled until something is written in it (`canTheAmountBeSettled`). The laptop
  refuses a line paid below its price without a reason (`PaymentNoticeMissing`). Paying more than the
  total needs no reason.
- **Cash or card unless nothing is to be paid.** An amount above zero offers Bar and Karte, and zero
  offers Kassieren alone and sends `none`. The laptop refuses money paid without cash or card, and a
  payment method named when nothing was paid.
- **A cancelled item is never settled.** The settle view lists it only with "Stornierte Positionen
  anzeigen" ticked, struck through; it cannot be ticked, alone or with its order or the whole table, and it counts toward neither the
  selected total. Settling an item a waiter cancelled in the meantime is
  refused naming it (`errors.settlement.itemWasCancelled`).
- **The amount page stays open on a refusal and closes otherwise.** A refusal keeps what was typed
  and shows the reason on the page, with the item it names, and the "Neu laden" those texts name. An accepted settlement closes it and clears the selection. When
  no answer came it closes, keeps the selection, and the screen tells the waiter to reload
  (`useSettleDialog`).
- **One table per settlement.** The settle view shows one table, and the laptop refuses a selection
  whose orders name more than one `TableId`.
- **A notice belongs to the table it was given at.** Opening or closing a table clears the notice
  after settling (`openTable`, `closeTable`), so "Geben Sie dem Gast … zurück" never shows at the next
  table, nor on its amount page.
- **The table search keeps or drops whole orders by status and single items by payment and
  cancellation.** Each option of `TableOrderSearchRequest` is optional and filters nothing when
  left out: `orderStatus` (`open`, `partiallyFulfilled`, `fulfilled`, the order's status as
  `OrderQueries.Status` computes it, cancelled items left out, so an order whose items are all
  cancelled counts as `fulfilled`) keeps or drops whole orders with all their items;
  `paymentStatus` (`paid`, `notPaid`) and `includeCancelled` (`false` leaves cancelled and
  acknowledged items out) keep or drop items. An order left with no item is not sent. Every screen
  sends all three options as the waiter picked them; Kassieren opens on `paymentStatus: notPaid`
  and `includeCancelled: false`, Stornieren on `paymentStatus: null` and `includeCancelled: false`,
  both with `orderStatus: null`.
- **The settle view is loaded by table id, and a stale table is never trusted.** It searches
  `/api/open-items/tables/{tableId}/search` through `api/tableSearch.ts` and the latest request gate,
  with the picked filter, and again on `OrdersChanged`, `ConfigurationChanged` and `GroupsJoined`;
  every change of the filter loads again through the gate, and the ticks then drop every item the
  answer no longer shows (`withoutItemsThatAreGone`). When the laptop cannot be reached the orders leave the screen and the
  ticks wait for the next answer. A 404 is a broken program, because the table came from the plan:
  the view says in one sentence that the table could not be loaded and to open it again from the
  plan (`phone.openItems.errors.tableCouldNotBeLoaded`), and clears the ticks
  (`showWhyTheLaptopRefusedTheTable`, which shows any other refusal of the read the same way). The laptop answers a
  table of the running festival, hidden or not, with every order newest first; when no festival
  runs it answers `Refusal.Settlement.NoRunningFestival`, as settling does, and the view shows its
  sentence.
- **Whoever settled first keeps it.** An item another waiter already settled stays as it was, and the
  notice names the amount to hand back to the guest (`noticeAfterSettling`). The same waiter settling
  an item again overwrites their own price and reason, keeping the time and the collector, so sending
  again after a lost answer settles nothing twice.

## Tests

- `frontend/tests/phone/views`, on `openItemsFixture.ts`: `OpenItems.table.spec.ts` (loading by id,
  the title, a paid position, nothing left to take payment for, the laptop out of reach, the 404 in German and English, no table after a
  reload, closing on leaving, the ticks of an order just sent, a cancelled item struck through with
  an empty tick box cell, the details button on both screens opening the price and the history
  without ticking, a row tap ticking on both screens and unticking again, a row tap on a paid item
  ticking nothing, a tap on the tick box ticking without the history), `OpenItems.filter.spec.ts`
  (each screen's filter when it opens and what it sends, the picks sent as made, the filter back
  on its own choices when the screen opens again, ticks dropped for items a pick hides, the paid
  check and no tick box on Kassieren, a paid item tickable on Stornieren, the struck-through
  cancelled row on both, nothing left to cancel), `OpenItems.ticking.spec.ts` (ticking
  through the tick box, whole table, whole order, settling from the footer, the notice when somebody else settled
  first), `OpenItems.cancelling.spec.ts` (cancel mode: the title, a paid item tickable, the count in
  German and English; the cancel page: the list, the title and buttons, the cash in both languages, sending and returning
  to the table, a refusal with its reload, the reload, the reason gone after the reload and after
  back, no answer, back), `OpenItems.settleButton.spec.ts` (the button in German and English, the amount filled in,
  a free selection, Bar, Karte and Abbrechen shut on the amount page while settling),
  `OpenItems.settling.spec.ts` (a shorter amount with its reason, rounding up, cancelling, a refusal,
  a refusal naming an item cancelled in the meantime with its reload, the reload, the reason gone
  after the reload and after Abbrechen, no answer) and `OpenItems.paymentMethod.spec.ts` (card, and nothing paid for free items);
  `TablePlan.sheet.spec.ts` for "Position kassieren" and "Position stornieren" on the plan.
- `frontend/tests/phone/components/header/AppHeader.spec.ts` for the reload in the bar, and
  `frontend/tests/AppLayout.spec.ts` for that reload filling the settle view again after the
  laptop was out of reach.
- `frontend/tests/phone/components/openItems`: `AmountPaidDialog.spec.ts` (including the title and
  the reload beside a refusal), `AmountPaidDialogShrinkingSelection.spec.ts` and
  `frontend/tests/phone/components/orderList/OrderListCard.spec.ts` (the shared list, see the
  `showing-a-tables-orders` page); `frontend/tests/phone/components/BaseFullPageDialog.spec.ts`.
- `frontend/tests/phone/core`: `openItems.selection.spec.ts` (both purposes, the cash to give back),
  `openItems.search.spec.ts` (the filter each purpose opens with), `openItems.settling.spec.ts` and
  `settlementSplit.spec.ts`; `frontend/tests/shared/core/statusTints.spec.ts`.
- `frontend/tests/phone/stores`: `openItems.table.spec.ts` (the read by id, the gate, a lost
  connection, the 404, a refusal, closing, the reloads, the picked filter on `OrdersChanged`, the
  gate on a filter change), `openItems.settling.spec.ts` (including the
  notice staying behind at its table), `openItems.cancelling.spec.ts` (a paid item tickable, the
  cash, the request, the reload after success and the gate, a refusal, no answer, back to settling),
  `openItemsSettleRetry.spec.ts`, `openItems.justSent.spec.ts`,
  `openItemsAfterAnotherPhonePlacesAnOrder.spec.ts` and `order.settleRightAway.spec.ts`;
  `frontend/tests/phone/composables/useSettleDialog.spec.ts` and `useSendAndLeave.spec.ts`.
- Backend: `GastronomyApp.Infrastructure.Tests/Services/OpenItemsServiceTest.cs` (every read and
  settle rule on the real DbContext, each search option alone and the options of Kassieren together,
  an emptied order not sent, cancelled and acknowledged items, no option filtering nothing, nothing
  stored on a refusal, the two statements of the table search, the counts under the paid filter and
  beside cancelled items, handed out when cancelled for each way an item can be cancelled, an item's
  whole history with put back, cancel, acknowledgement and payment with its reason, no payment, a
  payment without a method or a reason, an unknown item) and `Mapping/OpenItemMappingTest.cs`,
  `GastronomyApp.Api.Tests/Endpoints/OpenItemEndpointsTest.cs` (the table search by table id, a
  renamed table, the options of Kassieren and an order status on the wire, an option outside its
  values, settling read back through the search, a selection spanning two tables, the counts and
  the hand-out mark on the wire, an item's history with the reason of a short payment, an unknown item, no token),
  `FestivalScopeEndpointsTest.cs`, `Auth/Filters/DeviceKindTest.cs` (a station tablet refused on
  the search and the history).
