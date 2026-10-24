# Settling a table

A waiter taps a table on the plan and "Abrechnen", and the phone shows that table's orders one by
one, with how far each is handed out and what is already paid. The waiter ticks what the guest pays
for, taps Abrechnen under the selected total and, on the amount page, confirms what the table
actually handed over, in cash or by card.

## Where the code lives

Frontend, under `frontend/src/phone/`:

- `views/OpenItems.vue` (route `/open-items`) is the settle view of one table, titled "Abrechnen"
  like the button that opens it: the reload button ("Neu laden"),
  the sentence when the table could not be loaded, the notice after settling, the table's orders and
  the bottom bar with the selected total and the one Abrechnen button that opens the amount page. It
  goes back to the plan when no table is open, as after a reload, and closes the table when the
  waiter leaves. The header (`components/header/AppHeader.vue`) shows the table's current name and
  "Tischplan".
- `components/openItems/`: `TableOrders.vue` (the orders of the table, each card tinted and bordered
  by how far it is handed out, with a tick per order and for the whole table), `OpenPositionRow.vue`
  (one item, tinted by whether it is handed out), `SettleNotice.vue`, and
  `AmountPaidDialog.vue`, the amount page: the selected total, the fields, then Bar abrechnen and Mit
  Karte abrechnen, or Abrechnen alone when nothing is to be paid, and Abbrechen.
- `components/AmountPaidFields.vue`: the amount field, filled in with the selected total, and the
  reason field that appears once the amount falls short.
- `core/openItems.ts`: the selection over one table's `TableOrderReportView` (`unsettledItemsIn`,
  `withItemToggled`, `withWholeTable`, `withWholeOrder`, `withoutItemsThatAreGone`),
  `selectedAmountCents`, `isPaymentNoticeNeeded`, `isAPaymentMethodNeeded`, `canTheAmountBeSettled`
  and `noticeAfterSettling`; the hand-out and selection states are the generated `ShareState`. The
  colours of a card and a row come from `shared/core/statusTints.ts` (`statusTokenOf`,
  `statusTintCssOf`), the same mapping and strength the table plan's drawing uses.
  `core/settlementSplit.ts` spreads the amount paid over the ticked lines.
- `stores/openItems.ts` (`useOpenItemsStore`: `openTable`, `openTableAndSelectItemsOnceLoaded`,
  `closeTable`, `load`, `listen`, `toggleItem`, `setWholeTable`, `setWholeOrder`, `settle`,
  `report`, `loadFailureKey`, `notice`, `isSettling`) and `composables/useSettleDialog.ts`
  (`amountPaidIsOpen`, `settleTheAmountPaid`). `views/TablePlan.vue` opens a table with `openTable`;
  `composables/useSendAndLeave.ts` opens it with the items of an order just sent ticked.
- Texts: `phone.openItems.*` in `shared/i18n/de.json` and `en.json`.

Backend, under `backend/`:

- `GastronomyApp.Api/Endpoints/OpenItemEndpoints.cs` under `/api/open-items`: `/tables/{tableId}`
  (`Handlers/OpenItemQueryHandler.cs`) and `POST /settle`
  (`Handlers/OrderItemSettlementHandler.cs`). Wire records in `GastronomyApp.Contracts/OpenItems/`
  (`TableOrderReportView` with the table's `TableId` and current `TableName`,
  `TableOrderRecordView`, `TableOrderRecordItemView`; `SettleItemsRequest`, `SettleLineRequest`,
  `SettlementView`).
- `GastronomyApp.Core/Services/OrderItemSettlementService.cs` checks and applies a settlement, and
  `OpenItemsService.cs` reads one table with its orders (`ReadTableWithOrdersAsync`). Refusals are in
  `Core/Refusals/Refusal.Settlement.cs`, the queries in
  `Infrastructure/Repositories/OpenItemRepository.cs` (`FindTableWithOrdersAsync`,
  `FindForSettlementAsync`).

## Invariants

- **Every settlement names the amount paid.** The Abrechnen button opens the amount page for every
  selection, a free one included, and the request carries a paid price for every line
  (`splitSettlement`). Giving something away is that same act with the amount set to zero, never a
  second kind of settlement.
- **What a settlement stores.** `OrderItem.SettledAtUtc` is the paid flag (null means still open, so
  a flag and a timestamp can never disagree), `ChargedPriceCents` is what was actually collected, and
  `PaymentNotice` is the reason typed when less than the displayed price was collected. The phone
  sends one price per line and the laptop stores exactly that price; it never spreads an amount
  across items. What a table still owes is derived from these on every read, never stored.
- **The full amount is filled in.** The amount field starts at the selected total, so the ordinary
  case is Abrechnen and then Bar or Karte.
- **A short amount needs a typed reason.** Below the selected total the reason field appears and the
  confirm buttons stay disabled until something is written in it (`canTheAmountBeSettled`). The laptop
  refuses a line paid below its price without a reason (`PaymentNoticeMissing`). Paying more than the
  total needs no reason.
- **Cash or card unless nothing is to be paid.** An amount above zero offers Bar and Karte, and zero
  offers Abrechnen alone and sends `none`. The laptop refuses money paid without cash or card, and a
  payment method named when nothing was paid.
- **The amount page stays open on a refusal and closes otherwise.** A refusal keeps what was typed
  and shows the reason on the page. An accepted settlement closes it and clears the selection. When
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
  reload, closing on leaving, the ticks of an order just sent), `OpenItems.ticking.spec.ts` (ticking
  rows, whole table, whole order, settling from the footer, the notice when somebody else settled
  first), `OpenItems.settleButton.spec.ts` (the button in German and English, the amount filled in,
  a free selection, Bar, Karte and Abbrechen shut on the amount page while settling),
  `OpenItems.settling.spec.ts` (a shorter amount with its reason, rounding up, cancelling, a refusal,
  no answer) and `OpenItems.paymentMethod.spec.ts` (card, and nothing paid for free items);
  `TablePlan.sheet.spec.ts` for "Abrechnen" on the plan.
- `frontend/tests/phone/components/openItems`: `AmountPaidDialog.spec.ts`,
  `AmountPaidDialogShrinkingSelection.spec.ts` and `OpenPositionRow.spec.ts`.
- `frontend/tests/phone/core`: `openItems.selection.spec.ts`, `openItems.settling.spec.ts` and
  `settlementSplit.spec.ts`; `frontend/tests/shared/core/statusTints.spec.ts`.
- `frontend/tests/phone/stores`: `openItems.table.spec.ts` (the read by id, the gate, a lost
  connection, the 404, a refusal, closing, the reloads), `openItems.settling.spec.ts` (including the
  notice staying behind at its table),
  `openItemsSettleRetry.spec.ts`, `openItems.justSent.spec.ts`,
  `openItemsAfterAnotherPhonePlacesAnOrder.spec.ts` and `order.settleRightAway.spec.ts`;
  `frontend/tests/phone/composables/useSettleDialog.spec.ts` and `useSendAndLeave.spec.ts`.
- Backend: `GastronomyApp.Core.Tests/Services/OrderItemSettlementServiceTest.cs`,
  `OrderItemSettlementServiceOverlapTest.cs` and `OpenItemsServiceTest.cs`,
  `GastronomyApp.Api.Tests/Endpoints/OpenItemEndpointsTest.cs` (the one-table view by table id, a
  renamed table, settling read back through the one-table view, a selection spanning two tables),
  `FestivalScopeEndpointsTest.cs`, `GastronomyApp.Api.Tests/Mapping/OpenItemMappingTest.cs` and
  `GastronomyApp.Infrastructure.Tests/Repositories/OpenItemRepositoryTest.cs`.
