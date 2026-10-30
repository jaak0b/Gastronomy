# Show the text of an empty list

Every list in the app that has no entry shows the one shared text "Keine Einträge" / "No entries",
the same on every screen and every surface. This is for lists only. A dropdown, an autocomplete, a
select, a menu and a button are not lists: they keep their own texts, such as the tooltip of a
disabled add button and the no-data text of a search field. A hint or an instruction about what to
do next is a different text with its own place (`row-warnings.md`).

## The shared pieces

- `frontend/src/shared/components/BaseEmptyListText.vue` is a paragraph in medium emphasis that
  shows `common.labels.noEntries`. It takes no props. The screen passes its own `class` for the
  spacing (`admin-section-gap` on an admin tab, `pa-4` on the phone's table plan) and, where its
  spec has to tell two states apart, its own `data-test`; without one the paragraph carries
  `data-test="empty-list-text"`. It writes no size, so the text size of the surface applies
  (`surface-sizes.md`).
- `frontend/src/shared/core/emptyList.ts` holds the one condition, `showsTheEmptyListText(load,
  entryCount)`: the list has loaded, the load did not fail, and it has no entry. Never write that
  condition again beside it. `load` is a `ListLoadState` (`hasLoaded`, `loadFailed`), so a store
  passes itself: `showsTheEmptyListText(ingredients, ingredients.ingredients.length)`. `entryCount` is the count of
  the list itself, not of the rows a box such as "Deaktivierte anzeigen" lets through: a list whose
  entries are all deactivated is not empty, and the text never states what the person chose.
  `NOTHING_TO_LOAD` is the load state of a list that nothing loads (the draft order on the review
  screen).
- The stores hand out the state. Every store built on `defineAdminList` or `defineFestivalScopedList`
  returns `hasLoaded` and `loadFailed` (festivals, categories, items, ingredients, units, stations,
  staff). A list that belongs to a festival (stations, items, the table plan) also returns
  `loadedFestivalId`, the festival its entries were read for, or none for the global list. The
  festival page counts such a list as loaded only for its own festival, through
  `loadStateAtTheFestival(store, festivalId)` in `admin/core/festivalListLoad.ts`, because the global
  answer carries no festival flag and would make every card look empty while this festival's own
  answer is on its way. The phone's catalog and table plan stores and the station store return them too, and the
  phone's `useOpenItemsStore` and `useOrderOverviewStore` derive them from the report they hold and
  the failure key.

## Where it shows

- Admin: the Feste, Kellner and Ausgabestellen tabs; the Artikel tab, once when
  the laptop sent no category and under every category that has no article
  (`ItemsListCategorySection.vue`, its prop `showsEmptyListText`); the festival page's station card,
  article card and table plan card, each only once this festival's own list has arrived
  (`HideableEntryList.vue` takes the plan's `loadState`; the `noArea` warning of the table plan card
  reads the same state); both columns of `ItemIngredientsDialog.vue`, the recipe lines and the
  list of all ingredients, whose count is the whole list and never what the search lets through.
- Phone: `OrderOverview.vue` and `OpenItems.vue` (the settle and cancel views) when the laptop finds
  no order, `data-test="no-orders"`; `TablePlan.vue` for a festival without areas (`no-tables`) and
  for an area without tables (`no-tables-in-area`); `Catalog.vue` for a catalog without categories
  (`no-categories`) and for an opened category without articles (`no-items`); `Review.vue` for an
  order without a line (`no-lines`); `ItemHistoryDialog.vue` for a history without an event
  (`history-empty`).
- Station tablet: both open columns and the overview board of `StationPage.vue`
  (`StationOpenBoard.vue`, its prop `showsEmptyListText`), and the done view
  (`data-test="nothing-done"`).

## Steps

1. Let the store return `hasLoaded` and `loadFailed`, or build a `ListLoadState` from what the
   screen already knows (`ItemHistoryDialog.vue` does).
2. Compute `showsTheEmptyListText(state, entries.length)` in the component that owns the state and
   show `<BaseEmptyListText v-if="..." />` where the rows would be. A child that only receives the
   rows takes the result or the state as a prop, as `ItemsListCategorySection.vue` and
   `HideableEntryList.vue` do.
3. Never add a key of the list's own, such as a "none yet" or "nothing left" text, and never a
   sentence that depends on a filter.
4. Failure and refusal messages, loading states, status labels such as "Kein Telefon eingerichtet",
   instructions and hints, and confirmations keep their own texts: they are not the text of a list
   without entries.

## Specs

- `frontend/tests/shared/core/emptyList.spec.ts` covers the condition.
- `frontend/tests/shared/components/BaseEmptyListText.spec.ts` covers the text in German and
  English and a `data-test` passed in by the screen.
- Each list's own spec asserts the text once the list has loaded and is empty, and that it stays away
  before the laptop answered and after a failed load, and on the festival page while this festival's
  own list is on its way after a global one arrived (`FestivalPage.emptyLists.spec.ts`), for example
  `tests/admin/components/festivals/FestivalsList.spec.ts`, `FestivalPage.emptyLists.spec.ts`,
  `tests/station/views/StationPage.screen.spec.ts` and `tests/phone/views/Catalog.emptyLists.spec.ts`.
