# Show a failed load with a reload on a phone screen

`LoadFailureNotice.vue` is the phone's one notice for a load that failed: it says what could not be
loaded and offers one reload. Today the ordering screen and the table plan screen use it. A phone
screen that shows a failed load uses this piece; it never builds a message, a button or a timer of
its own (frontend rule 8, and rule 4: nothing retries by itself, the waiter presses).

## The shared piece

- `frontend/src/phone/components/LoadFailureNotice.vue` is a warning (`data-test="load-failure-text"`)
  with the one reload button below it (`data-test="load-failure-reload"`, text
  `common.actions.reload`, "Neu laden" / "Reload"). It takes `text` and emits `reload`. The screen
  passes its own `class` for the spacing and its own `data-test` for the root when its spec has to
  find it.
- Consumers: `views/TablePlan.vue` (`data-test="table-plan-load-failed"`, text
  `phone.tablePlan.errors.loadFailed`, reload `tablePlan.load()`) and `views/Catalog.vue`
  (`data-test="catalog-load-failed"`, text `phone.catalog.errors.loadFailed`, reload
  `catalog.load()`).

## Steps

1. The store keeps `loadFailed` (and `hasLoaded`) beside the data and sets it only for the newest
   request: take a number from the latest request gate when the load starts and apply the answer,
   success or failure, only if it is still the newest (root rule 21). A success sets `loadFailed` back
   to false; a failure changes nothing else, so data the screen already shows stays.
2. Show `<LoadFailureNotice v-if="store.loadFailed" :text="t('phone.<area>.errors.loadFailed')"
   @reload="store.load()" />` where the data would be. The text names what could not be loaded, in
   the form of the existing ones: the fact first, then "Laden Sie die Seite neu." / "Reload the page."
3. Every load that succeeds removes the message by itself, whatever started it: the reload, the hub's
   `GroupsJoined` refetch and `ConfigurationChanged` all end in the same `load()`.
4. A load that fails must never touch what the waiter has not sent. The unsent draft order lives in
   `localStorage` and is changed only by the waiter or by a catalog that names another festival.

## Specs

`frontend/tests/phone/views/Catalog.loadFailure.spec.ts` mounts the ordering screen with the catalog
request failing: the sentence and the reload in both languages, the reload loading again, a failing
reload keeping the message, a later success and a `ConfigurationChanged` event removing it, and the
stored draft staying byte for byte the same. `tests/phone/stores/catalog.spec.ts` pins the flag and
the answers that arrive out of turn. `tests/phone/views/TablePlan.landing.spec.ts` covers the plan.
