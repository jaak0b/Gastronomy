# Settling a table

The phone's Offene Posten screen lists every table that still owes something, and a table name typed
into its search field shows that table's orders one by one. The waiter ticks what the guest pays for,
taps Abrechnen under the selected total and, on the amount page, confirms what the table actually
handed over, in cash or by card.

## Where the code lives

Frontend, under `frontend/src/phone/`:

- `views/OpenItems.vue` is the screen: the reload button, the table field
  (`components/review/TableField.vue`), the list or the looked-up table, the notice after settling,
  and the bottom bar with the selected total and the one Abrechnen button that opens the amount page.
- `components/openItems/`: `OpenTablesList.vue` and `OpenTablePanel.vue` (the tables, folded until
  opened), `OpenTableLookup.vue` (the orders of one table, with a tick per order and for the whole
  table), `OpenPositionRow.vue` (one item), `SettleNotice.vue`, and `AmountPaidDialog.vue`, the amount
  page: the selected total, the fields, then Bar abrechnen and Mit Karte abrechnen, or Abrechnen alone
  when nothing is to be paid, and Abbrechen.
- `components/AmountPaidFields.vue`: the amount field, filled in with the selected total, and the
  reason field that appears once the amount falls short.
- `core/openItems.ts`: the selection (`withItemToggled`, `withWholeTable`, `withWholeOrder`,
  `isHeldBackByAnotherTable`), `selectedAmountCents`, `isPaymentNoticeNeeded`,
  `isAPaymentMethodNeeded`, `canTheAmountBeSettled` and `noticeAfterSettling`.
  `core/settlementSplit.ts` spreads the amount paid over the ticked lines.
- `stores/openItems.ts` (`useOpenItemsStore`: `load`, `loadTableReport`, `toggleItem`,
  `setWholeTable`, `setWholeOrder`, `settle`, `notice`, `isSettling`) and
  `composables/useSettleDialog.ts` (`amountPaidIsOpen`, `settleTheAmountPaid`).
  `composables/useSendAndLeave.ts` opens the screen with the items of an order just sent ticked
  (`openTableAndSelectItemsOnceLoaded`).
- Texts: `phone.openItems.*` in `shared/i18n/de.json` and `en.json`.

Backend, under `backend/`:

- `GastronomyApp.Api/Endpoints/OpenItemEndpoints.cs` under `/api/open-items`: the list,
  `/table-names`, `/table` (`Handlers/OpenItemQueryHandler.cs`) and `POST /settle`
  (`Handlers/OrderItemSettlementHandler.cs`). Wire records in `GastronomyApp.Contracts/OpenItems/`
  (`SettleItemsRequest`, `SettleLineRequest`, `SettlementView`).
- `GastronomyApp.Core/Services/OrderItemSettlementService.cs` checks and applies a settlement, and
  `OpenItemsService.cs` reads the open items, the table names and one table's orders. Refusals are in
  `Core/Refusals/Refusal.Settlement.cs`, the queries in
  `Infrastructure/Repositories/OpenItemRepository.cs`.

## Invariants

- **Every settlement names the amount paid.** The Abrechnen button opens the amount page for every
  selection, a free one included, and the request carries a paid price for every line
  (`splitSettlement`).
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
  no answer came it closes, keeps the selection, and the screen tells the waiter to reload the list
  (`useSettleDialog`).
- **One table per settlement.** Once an item is ticked, no item of another table can be ticked
  (`isHeldBackByAnotherTable`), and the laptop refuses a selection that spans several tables.
- **Whoever settled first keeps it.** An item another waiter already settled stays as it was, and the
  notice names the amount to hand back to the guest (`noticeAfterSettling`). The same waiter settling
  an item again overwrites their own price, so sending again after a lost answer settles nothing twice.

## Tests

- `frontend/tests/phone/views`, on `openItemsFixture.ts`: `OpenItems.settleButton.spec.ts` (the
  button in German and English, the amount filled in, a free selection, Bar, Karte and Abbrechen shut
  on the amount page while settling), `OpenItems.settling.spec.ts`
  (a shorter amount with its reason, rounding up, cancelling, a refusal, no answer),
  `OpenItems.paymentMethod.spec.ts` (card, and nothing paid for free items), `OpenItems.tables.spec.ts`
  (the list, one table at a time, the notice when somebody else settled first),
  `OpenItems.lookup.spec.ts` (looking up one table and settling from it) and
  `OpenItems.lateAnswer.spec.ts`.
- `frontend/tests/phone/components/openItems`: `AmountPaidDialog.spec.ts`,
  `AmountPaidDialogShrinkingSelection.spec.ts` and `OpenPositionRow.spec.ts`.
- `frontend/tests/phone/core`: `openItems.selection.spec.ts`, `openItems.settling.spec.ts` and
  `settlementSplit.spec.ts`.
- `frontend/tests/phone/stores`: `openItems.settling.spec.ts`, `openItemsSettleRetry.spec.ts`,
  `openItems.list.spec.ts`, `openItems.lookup.spec.ts`, `openItems.justSent.spec.ts`,
  `openItemsAfterAnotherPhonePlacesAnOrder.spec.ts` and `order.settleRightAway.spec.ts`;
  `frontend/tests/phone/composables/useSettleDialog.spec.ts`.
- Backend: `GastronomyApp.Core.Tests/Services/OrderItemSettlementServiceTest.cs`,
  `OrderItemSettlementServiceOverlapTest.cs` and `OpenItemsServiceTest.cs`,
  `GastronomyApp.Api.Tests/Endpoints/OpenItemEndpointsTest.cs`,
  `GastronomyApp.Api.Tests/Mapping/OpenItemMappingTest.cs` and
  `GastronomyApp.Infrastructure.Tests/Repositories/OpenItemRepositoryTest.cs`.
