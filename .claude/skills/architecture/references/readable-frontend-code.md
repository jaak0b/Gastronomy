# Readable TypeScript and Vue: what the owner wants, by example

## Decision

The same question as for C# (`readable-code.md`): can a developer who has never seen the code read the
screen's rule straight out of it? Every pair below is a real change from this repository's history
(the waiter's order list, October 2026). The "before" compiled and passed its tests; the owner still
rejected it. Copy the "after".

The skill pages are a guideline. Where following one literally produces code like a "before" below,
write the "after" and say in the report which page you departed from and why (root rule 26).

## The principles

1. **One screen element, one component.** When two screens show the same thing (an order card, a
   filter), they use one component with a prop for what differs, never two copies that drift.
2. **One rule, one function.** What can be ticked, what a screen asks the laptop for and what it
   starts with are each one function in `core/`, keyed by the screen's purpose and ending in
   `assertNever`. Components ask that function; they never restate its rule.
3. **The laptop's numbers are the truth.** A figure the server can compute over all the data (a
   counter, a total) comes from the wire, never from counting the items the phone happened to
   receive.
4. **A sentence on screen must be true for every state that reaches it.** When a new control can
   change what the list shows, recheck every text that describes the list.
5. **Types live where the rule lives.** `api/` imports from `core/`, not the other way round.

## Pairs

### The same heading written twice

Before (`OverviewOrderCard.vue` and `TableOrders.vue` at c33487b): two components each build the
order heading.

```ts
// OverviewOrderCard.vue
function takenByText(): string {
  return t('common.labels.takenBy', { time: formatFestivalMoment(props.order.createdAtUtc, props.language), name: props.order.staffMemberName })
}

// TableOrders.vue
function takenByTextFor(order: TableOrderRecordView): string {
  return t('common.labels.takenBy', { time: formatFestivalMoment(order.createdAtUtc, props.language), name: order.staffMemberName })
}
```

After (`components/orderList/OrderListCard.vue`): one card for all three screens; the screen passes
its `purpose` (or none for the read-only overview), and the card decides the tick column from it.

### A component that restates a core rule

Before (`OpenPositionRow.vue` at 3f5a517): the row had its own `canBeTicked` next to an `isDisabled`
prop, so "what may be ticked" was decided in the store and again in the row.

```ts
const canBeTicked = computed(() => {
  switch (props.productionState) {
    case 'produced':
    case 'notProduced':
      return !props.isSettled
    ...
```

After (`core/openItems.ts`): one function per rule, keyed by purpose, exhaustive. The card and the
store both call it.

```ts
export function canBeSelected(item: TableOrderRecordItemView, purpose: SelectionPurpose): boolean {
  switch (purpose) {
    case 'settling':
      return item.settledAtUtc === null && !isCancelled(item.state)
    case 'cancelling':
      return !isCancelled(item.state)
    default:
      return assertNever(purpose)
  }
}
```

### A one-off toggle beside a filter that already exists

Before: Kassieren and Stornieren had their own "Alle Positionen anzeigen" box, while the overview
had a full filter.

After: one `OrderListFilter.vue` on all three screens, and the screen's starting point is one
function.

```ts
export function defaultFilterFor(purpose: SelectionPurpose): TableSearchFilter {
  switch (purpose) {
    case 'settling':
      return { orderStatus: null, paymentStatus: 'notPaid', includeCancelled: false }
    case 'cancelling':
      return { orderStatus: null, paymentStatus: null, includeCancelled: false }
    default:
      return assertNever(purpose)
  }
}
```

### Counting what arrived instead of reading what is true

Before: the order card counted "x / y zubereitet" over the items in the answer. Under the payment
filter it showed "1 / 1" for an order whose second item was still waiting at the station.

After: the laptop sends `notCancelledItemCount` and `handedOutItemCount` per order, computed over all
items regardless of the filter, and the card shows those.

### A sentence that stopped being true

Before: the empty text was chosen by purpose only ("An diesem Tisch ist nichts mehr zu kassieren."),
which became false once the waiter could pick a filter that hides everything.

After (`views/OpenItems.vue`): the text depends on whether the waiter changed the filter.

```ts
const emptyText = computed(() => {
  if (!filterIsTheDefault.value) {
    return t('phone.orderOverview.messages.noOrderMatches')
  }
  ...
})
```

## Smells to stop at

- A second component that renders the same thing as an existing one.
- A component computing what a `core/` function already decides.
- A count or sum taken over a filtered answer.
- A per-screen toggle where a shared control exists.
- An on-screen sentence written before the screen gained a new way to change its list.
- `core/` importing a type from `api/`.

## Tests

None of its own: the pairs point at code that its own specs cover. The review uses this page: a
change that matches a "before" is a finding.
