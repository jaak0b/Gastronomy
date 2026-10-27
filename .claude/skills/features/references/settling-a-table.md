# Settling a table

A waiter taps a table on the plan and "Position kassieren", and the phone shows that table's orders one by
one, with how far each is handed out and what is already paid. The waiter ticks what the guest pays
for, taps Kassieren under the selected total and, on the amount page, confirms what the table
actually handed over, in cash or by card.

The same view cancels items: "Position stornieren" on the table's action sheet opens it in cancel
mode, where every item that is not cancelled can be ticked, paid ones included. The footer counts the
ticked items and its "Stornieren" opens a page that lists them, says how much cash the guest gets
back for the paid ones, and cancels them on confirming. A cancellation is final.

## Where the code lives

Frontend, under `frontend/src/phone/`:

- `views/OpenItems.vue` (route `/open-items`) is the settle view of one table, titled "Kassieren":
  the reload button ("Neu laden"),
  the sentence when the table could not be loaded, the notice after settling, the table's orders and
  the bottom bar with the selected total and the one Kassieren button that opens the amount page. It
  goes back to the plan when no table is open, as after a reload, and closes the table when the
  waiter leaves. In cancel mode it is titled "Position stornieren", the footer counts the selection
  (`phone.openItems.labels.selectedCount`) and its "Stornieren" opens `CancelItemsDialog.vue`. The header (`components/header/AppHeader.vue`) shows the table's current name and
  "Tischplan".
- `components/openItems/`: `TableOrders.vue` (the orders of the table, each card tinted and bordered
  by how far it is handed out, with a tick per order and for the whole table), `OpenPositionRow.vue`
  (one item, tinted by whether it is handed out), `SettleNotice.vue`, and
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
  `withoutItemsThatAreGone`), `selectedAmountCents`, `cashToReturnCents`, `isPaymentNoticeNeeded`,
  `isAPaymentMethodNeeded`, `canTheAmountBeSettled` and `noticeAfterSettling`; the hand-out and
  selection states are the generated `ShareState`. `canBeSelected(item, purpose)` is the one test
  of what can be ticked (settling: not settled and not cancelled; cancelling: not cancelled); a row
  shows its tick box exactly when the view passes it `isSelectable`, and
  `positionStateOf`, `producedCountIn`, `notCancelledItemCountIn` and `productionStateOf` read the item's
  `state` through `shared/core/orderItemStates.ts`; an order whose items are all cancelled counts as
  `all`, as on the table plan. A cancelled row carries the shared class `cancelled-item`
  (`shared/styles/cancelledItems.css`, the same struck-through look as on the station tablet),
  with no tint, no state icon and no tick box. The
  colours of a card and a row come from `shared/core/statusTints.ts` (`statusTokenOf`,
  `statusTintCssOf`), the same mapping and strength the table plan's drawing uses.
  `core/settlementSplit.ts` spreads the amount paid over the ticked lines.
- `stores/openItems.ts` (`useOpenItemsStore`: `openTable`, `openTableAndSelectItemsOnceLoaded`,
  `openTableToCancelItems`, `closeTable`, `load`, `listen`, `toggleItem`, `setWholeTable`,
  `setWholeOrder`, `settle`, `cancelSelectedItems`, `purpose`, `report`, `loadFailureKey`, `notice`,
  `isSettling`, `isCancelling`, `itemsToCancel`, `cashToReturnForTheItemsToCancel`) and `composables/useSettleDialog.ts`
  (`amountPaidIsOpen`, `settleTheAmountPaid`). `views/TablePlan.vue` opens a table with `openTable` or
  `openTableToCancelItems`;
  `composables/useSendAndLeave.ts` opens it with the items of an order just sent ticked.
- Texts: `phone.openItems.*` in `shared/i18n/de.json` and `en.json`.

Backend, under `backend/`:

- `GastronomyApp.Api/Endpoints/OpenItemEndpoints.cs` under `/api/open-items`: `/tables/{tableId}`,
  `POST /settle` and `POST /cancel`, all calling `OpenItemsService` directly. Wire records in `GastronomyApp.Contracts/OpenItems/`
  (`TableOrderReportView` with the table's `TableId` and current `TableName`,
  `TableOrderRecordView`, `TableOrderRecordItemView` with each item's `State`, `StateSetAtUtc` and
  `ChargedPriceCents`; `SettleItemsRequest`, `SettleLineRequest`, `SettlementView`;
  `CancelItemsRequest`, `CancellationView`).
- `GastronomyApp.Core/Services/OpenItemsService.cs` is the one service of the area on
  `IGastronomyDatabase`: `ReadTableAsync` reads one table with its orders and answers the
  `TableOrderReportView` (running festival lookup, then one table statement, so two statements; the
  lines of an order are sorted in memory with `StringComparer.OrdinalIgnoreCase`, because the item
  name and note carry no `NOCASE` collation), and `SettleAsync` checks and applies a settlement and
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
  `Core/Refusals/Refusal.Settlement.cs` and `Refusal.Cancellation.cs`.
  The wire records are mapped by `Core/Mapping/OpenItemMapping.cs` (lines, orders, the table report
  with the sum of the unsettled prices of the items that are not cancelled, the settlement).

## Invariants

- **Cancel mode reuses the settle view.** The purpose decides only what can be ticked, the footer,
  the title and which page the footer opens; the orders, the rows, the ticks and the reload are the
  same. Opening a table with `openTable` always returns to settling.
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
- **A cancelled item is shown and never settled.** The settle view lists it struck through, it
  cannot be ticked, alone or with its order or the whole table, and it counts toward neither the
  selected total nor the order's done count. Settling an item a waiter cancelled in the meantime is
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
- **The settle view is loaded by table id, and a stale table is never trusted.** It reads
  `/api/open-items/tables/{tableId}` through the latest request gate and again on `OrdersChanged` and
  `ConfigurationChanged`. When the laptop cannot be reached the orders leave the screen and the
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
  the title, the colours and states, no orders, the reload, the 404 in German and English, no table after a
  reload, closing on leaving, the ticks of an order just sent, a cancelled item struck through, not
  ticked and not counted), `OpenItems.ticking.spec.ts` (ticking
  rows, whole table, whole order, settling from the footer, the notice when somebody else settled
  first), `OpenItems.cancelling.spec.ts` (cancel mode: the title, a paid item tickable, the count in
  German and English; the cancel page: the list, the title and buttons, the cash in both languages, sending and returning
  to the table, a refusal with its reload, the reload, the reason gone after the reload and after
  back, no answer, back), `OpenItems.settleButton.spec.ts` (the button in German and English, the amount filled in,
  a free selection, Bar, Karte and Abbrechen shut on the amount page while settling),
  `OpenItems.settling.spec.ts` (a shorter amount with its reason, rounding up, cancelling, a refusal,
  a refusal naming an item cancelled in the meantime with its reload, the reload, the reason gone
  after the reload and after Abbrechen, no answer) and `OpenItems.paymentMethod.spec.ts` (card, and nothing paid for free items);
  `TablePlan.sheet.spec.ts` for "Position kassieren" and "Position stornieren" on the plan.
- `frontend/tests/phone/components/openItems`: `AmountPaidDialog.spec.ts` (including the title and
  the reload beside a refusal), `AmountPaidDialogShrinkingSelection.spec.ts` and
  `OpenPositionRow.spec.ts`; `frontend/tests/phone/components/BaseFullPageDialog.spec.ts`.
- `frontend/tests/phone/core`: `openItems.selection.spec.ts` (both purposes, the cash to give back), `openItems.settling.spec.ts` and
  `settlementSplit.spec.ts`; `frontend/tests/shared/core/statusTints.spec.ts`.
- `frontend/tests/phone/stores`: `openItems.table.spec.ts` (the read by id, the gate, a lost
  connection, the 404, a refusal, closing, the reloads), `openItems.settling.spec.ts` (including the
  notice staying behind at its table), `openItems.cancelling.spec.ts` (a paid item tickable, the
  cash, the request, the reload after success and the gate, a refusal, no answer, back to settling),
  `openItemsSettleRetry.spec.ts`, `openItems.justSent.spec.ts`,
  `openItemsAfterAnotherPhonePlacesAnOrder.spec.ts` and `order.settleRightAway.spec.ts`;
  `frontend/tests/phone/composables/useSettleDialog.spec.ts` and `useSendAndLeave.spec.ts`.
- Backend: `GastronomyApp.Infrastructure.Tests/Services/OpenItemsServiceTest.cs` (every read and
  settle rule on the real DbContext, nothing stored on a refusal, the two statements of the table
  read) and `Mapping/OpenItemMappingTest.cs`,
  `GastronomyApp.Api.Tests/Endpoints/OpenItemEndpointsTest.cs` (the one-table view by table id, a
  renamed table, settling read back through the one-table view, a selection spanning two tables),
  `FestivalScopeEndpointsTest.cs`.
