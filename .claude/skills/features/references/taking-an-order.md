# Taking an order

A waiter builds a table's order on the phone: the categories screen shows one button per category with
the number of portions already taken from it, the table field and a bar with the item count and the
running total. Tapping a category lists its articles, one row each, where a tap adds one portion, the
Note button adds one with a note, and the rows under the article count the portions by note and
station and take them off again.

## Where the code lives

Frontend, under `frontend/src/phone/`:

- `views/Catalog.vue` is the whole ordering screen: the category buttons (`labelFor` adds the portion
  count from `core/categoryPortions.ts`), the table field (`components/review/TableField.vue`), the
  basket bar (`components/catalog/BasketBar.vue`) and, once a category is open, its articles in
  `components/catalog/ItemGrid.vue` and the button back to the categories. It decides where a new portion goes:
  `addItem` places it straight away or opens the station question, `chooseStation` answers it, and
  `goToTheSummary` checks the table name before it navigates to `/review`.
- `components/catalog/ItemRow.vue` is one article: tapping its name or its price adds one portion,
  the Note button adds one with a note, and each group of portions has its count, its station, its
  note, minus and plus.
  `ItemNoteDialog.vue` asks for a note or corrects one; `LineStationSheet.vue` asks which station is
  to make the article, with a note field when a portion is being added and without one when portions
  move to another station.
- `core/itemPositions.ts`: `positionsForItem` lists the draft lines of one article,
  `groupPositions` groups them by note and station, and `buildItemPositionsView` adds the count and
  the article total. `core/routingPreview.ts` (`needsStationChoice`, `candidateStations`),
  `core/estimates.ts` (`estimateRangeForItem`, the quickest and the slowest station's minutes),
  `core/estimateWording.ts`, `core/catalogItemState.ts`, `core/tableName.ts` and `core/totals.ts`.
- `core/draftCart.ts` holds the draft order (`DraftOrder`, `DraftLine`) and saves it to
  `localStorage` under `draftOrder` with every change; `core/basket.ts` turns it into the lines the
  basket shows.
- Stores: `stores/order.ts` (`useOrderStore`: `addItem`, `dropLine`, `noteLine`, `chooseStation`,
  `setTable`, `itemCount`, `totalCents`, `changesAreRefused`), `stores/catalog.ts`, and
  `stores/estimates.ts`, whose `load` the screen calls when it opens. `composables/useStationChoiceQuotes.ts`
  gives the station question its waiting times.
- Texts: `phone.catalog.*`, `common.labels.soldOut` and `common.estimates.*` in
  `shared/i18n/de.json` and `en.json`.

## Invariants

- **A note is never merged into another line (root rule 20).** Every portion is a draft line of its
  own (`addLine` appends one per tap), and `groupPositions` keys a group on the note and the station
  together, so `3 x Hotdog` with "ohne Ketchup" never joins the plain ones. Only the article's own
  count and total sum every group.
- **A station is chosen only for an article with several candidate stations.** `needsStationChoice`
  is true when the article lists more than one station. Such an article opens the station question
  before the portion is added (the Note button opens it with the note field focused), its group rows
  name the station as a button that moves those portions, and plus on a group adds at that group's
  station without asking. An article with one station is placed there at once and its group row
  names the station as plain text.
- **The draft is one order, never a list.** `draftCart.ts` saves the order on every change, a reload
  puts it back, and a stored draft that cannot be read is thrown away and reported once
  (`draftWasLost`). Nothing sends it on its own; see the frontend `CLAUDE.md` rule on the retry queue.
- **A frozen order takes no change.** While a send attempt is unresolved (`changesAreRefusedFor`), the
  order store ignores every change, and the ordering screen moves the waiter to the summary as it
  opens. A draft from another festival is dropped silently unless it is frozen (see the `festivals`
  page).
- **The total is a calculation aid.** Once a portion is on the order, the article shows
  "{count} × {price}" and its total, and the basket bar shows the item count and the order total on the
  categories screen only; the screen of one category leaves the order total out. `orderTotalCents`
  adds up the unit prices of the basket lines, and a line whose article left the menu adds nothing.
- **Sold out stops adding, not removing.** Tapping the name, the price, the Note button or plus adds
  nothing, and the waiting time gives way to "Ausverkauft"; minus still takes portions off.
- **The keyboard reaches adding once.** The name and the price add the same portion, and keyboard
  focus reaches that action once per article.
- **No summary without a table.** `goToTheSummary` refuses an empty or blank table name, marks the
  field and puts the cursor in it.

## Tests

- `frontend/tests/phone/components/catalog`: `ItemRow.row.spec.ts` (adding by name and by price, the
  keyboard order, the parts beside the price, sold out, the default station),
  `ItemRow.figures.spec.ts` (price, waiting time, counts and totals), `ItemRow.notes.spec.ts` (the
  note dialog, the note and station groups, the station question for a note) and
  `ItemRow.keyboardInset.spec.ts`, on `itemRowFixture.ts`.
- `frontend/tests/phone/views`: `Catalog.building.spec.ts` (adding, the table check, the frozen
  order), `Catalog.categories.spec.ts`, `Catalog.stationChoice.spec.ts` (the station question, also
  when the price is tapped) and `Catalog.waitingTime.spec.ts`, on `catalogFixture.ts`.
- `frontend/tests/phone/core`: `itemPositions.spec.ts`, `categoryPortions.spec.ts`,
  `routingPreview.spec.ts`, `draftCart.editing.spec.ts`, `draftCart.storage.spec.ts`,
  `draftCart.unreadable.spec.ts`, `totals.spec.ts`, `estimates.spec.ts`, `estimateWording.spec.ts`,
  `catalogItemState.spec.ts` and `tableName.spec.ts`.
- `frontend/tests/phone/stores`: `order.reload.spec.ts` (the unreadable draft, an order frozen by a
  reload) and `orderFestivalScope.spec.ts`; `frontend/tests/phone/composables/useStationChoiceQuotes.spec.ts`.
- jsdom has no layout and does not apply scoped styles, so no spec proves where the row's parts
  land on a phone. A change to the row's layout is measured in a real browser at 360 px with the
  root font at 100, 130 and 160 percent.
