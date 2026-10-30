# Add a global admin tab

A global tab lists one kind of thing with a form to create and edit it and a switch off that can be
undone. No tab shows every step below on its own, so take each piece from where it lives: the
Ausgabestellen tab, `frontend/src/admin/components/stations/StationsList.vue`, for the list, the
switch off and its disabled reason; the categories of the Artikel tab (`stores/categories.ts`,
edited through `useEditSession` in `items/ItemsList.vue`) for a store on `defineAdminList`; and the
list of all ingredients in the recipe popup (`items/ItemIngredientsDialog.vue`) for a list whose
entries are deleted. `StaffList.vue` and `StationsList.vue` use `useRefusalDisplay` directly, and
the stations store is built on `defineFestivalScopedList` instead of `defineAdminList`.

## Steps

1. **Route.** Add the name to `AdminSection` and to `ADMIN_SECTIONS` in
   `frontend/src/shared/router/route.ts`. The order of `ADMIN_SECTIONS` is the order of the tabs, and
   the path is `/admin/<section>`. `parentRouteOf` gives the new tab the festival list as its parent
   without a change, so back from it lands there.
2. **Mount it.** In `frontend/src/admin/views/AdminShell.vue` import the list component, add its
   `case` to `titleFor` (it ends in `assertNever`, so a missing case fails the type check) and add
   one `v-else-if="section === '<section>'"` line. The tab itself comes from `ADMIN_SECTIONS`.
3. **Store.** `frontend/src/admin/stores/<things>.ts` is a Pinia store over `defineAdminList` from
   `stores/adminList.ts`: pass `path`, the zod `listSchema` and `entrySchema` from
   `shared/api/generatedSchemas`, `entriesOf`, `idOf` and `requestBodyOf`, and return `entries`,
   `hasLoaded`, `loadFailed`, `load`, `create`, `update` (the ingredients store wraps it as `save`),
   `setActive` plus a `listen()`. The store writes its definition once through `adminListDefinition(...)` and hands it to
   `defineAdminList`, which holds only what every list has (`entries`, `load`, `loadFrom`, `update`,
   `showCreated`, `forgetCreatedEntries`), and to the layers the laptop offers routes for: `withCreating(list, definition)` adds
   `create` and `createThenReload` and belongs to a list that has a create route (the waiters come in
   through enrolment, so the staff list has none); `withActivation(list, definition)` adds `setActive` and
   belongs to a list that has `/activate` and `/deactivate` routes (the festivals use `/hide` and `/show`,
   so the festivals store has none); `withDeleting(list, definition)` adds `deleteEntry` and belongs to
   a list whose entries are deleted for good on `DELETE <path>/{id}` (the units and ingredients stores
   hand it out as `deleteUnit` and `deleteIngredient`). After a delete the layer forgets the entries `create` showed ahead of the laptop's
   list, then reads the list again through the latest request gate, so neither a just-created entry
   nor a read that started earlier brings the deleted entry back. A store applies only the layers its
   endpoints have, e.g.
   `withActivation(withCreating(defineAdminList(definition), definition), definition)`. A list that depends on
   the running festival uses `defineFestivalScopedList` (`stores/festivalScopedList.ts`) instead,
   which also returns `loadedFestivalId`, the festival the entries were read for (none for the global
   list), so the festival page counts the list as loaded only for its own festival.
   The store takes the route of each call it makes from the screen that uses it, so a route is
   whatever the laptop's service for that screen offers, and a screen without an activate call
   passes none. Loads go through the latest request gate.
   The route each layer calls (method and the part after the list's `path`) is written once, in
   `ROUTES_OF_EACH_ADMIN_LIST_LAYER` in `stores/adminList.ts`, and the layers build their addresses
   from it. `tests/shared/apiRouteVocabulary.spec.ts` expands the `path` of every `adminListDefinition`,
   `defineAdminList` or `defineFestivalScopedList` call with the layers its store composes (for
   `defineFestivalScopedList`, the layers `festivalScopedList.ts` composes) and fails for any route
   the laptop does not serve with that method. It reads `path` only as the first key of the
   definition, written as a literal `'/api/...'` string, and it fails, naming the file, for any source
   file that calls one of those three functions without producing a route that way, so a store can
   never drop out of the check unnoticed (`festivalScopedList.ts` itself is the one exception, because
   its routes are expanded through each store that calls it). A new store therefore writes its `path`
   first and as a literal, and a new layer adds its routes to the table.
4. **Reload.** `listen()` returns
   `useAdminLoadStateStore().listenToTheLaptop(['ConfigurationChanged'], load, loadFailed)` from
   `stores/adminLoadState.ts`, never the connection store's own `listenToTheLaptop`: passing
   `loadFailed` is what puts the list under the admin's load failure bar while a screen shows it
   (`tests/admin/stores/adminLoadState.spec.ts` fails for an admin store that skips it).
   A list whose entries carry a flag derived from orders (the stations'
   `hasItemsToHandOutAtTheRunningFestival`, the table plan's `hasItemsToHandOutOrSettleAtTheRunningFestival`) listens to `OrdersChanged` too, or a button the flag
   disables stays wrong until the screen is opened again. The component calls it in `onMounted`,
   then `await store.load()`, and calls the returned release in `onUnmounted`. Why the events, and
   what the entity owes the laptop side, is in the `architecture` skill.
5. **The list component.** The heading is `BaseHeadingWithCheckbox`
   (`admin/components/BaseHeadingWithCheckbox.vue`) with the `h1` in its slot and its checkbox on a
   `showsDeactivated` ref (`checkbox-test-name="show-deactivated"`, text
   `admin.common.actions.showDeactivated`, in German "Deaktivierte anzeigen"), which decides whether
   deactivated entries are listed, so a row is listed when `isActive || showsDeactivated`. A `v-chip` with `admin.common.labels.deactivated` marks a deactivated
   entry, which offers activating in place of switching off: the open eye `BaseShowButton.vue` with
   the action's own word as its `label` ("Ausgabestelle aktivieren"). A row's actions follow the order in
   `hideable-rows.md`: text buttons, then the pencil, then switching off or on. The screen shows no
   load failure of its own: the shared bar under the tabs reports it (see the `admin-screens` page
   of `features`). Each entry is one `BaseListRow` (`hideable-rows.md`) with the name, its chips
   in `facts` and its buttons in `actions`. The text for an empty list is
   `shared/components/BaseEmptyListText.vue` (see `empty-list-text.md`), shown when
   `showsTheEmptyListText(store, store.<entries>.length)` says so: the list has loaded, the load did
   not fail and the store holds no entry.
6. **Create and edit.** `useEditSession` (`admin/composables/useEditSession.ts`) takes a `saveThrough`
   that creates when nothing is being edited and saves otherwise. The id field has the entity's own
   name, as in the recipe popup:

   ```ts
   useEditSession<AdminIngredientView, IngredientDraft, AdminIngredientView | null>({
     saveThrough: (draft, ingredient) =>
       ingredient === null
         ? ingredients.create(draft)
         : ingredients.save(ingredient.ingredientId, draft),
   })
   ```

   The row offers editing through the pencil, `admin/components/BaseEditButton.vue`, never a
   "Bearbeiten" text button.
   The dialog is its own component on `BaseFormDialog` (`shared/components/BaseFormDialog.vue`), mounted
   with `v-if="session.isOpen"`, `:error-text="session.refusalText"`, `@save="session.save"` and
   `@cancel="session.close"`. The session closes on success and stays open showing the refusal.
   `BaseFormDialog` puts the cursor into its first field once the dialog has opened (Vuetify's
   `after-enter`), so a dialog never sets `autofocus` itself. A spec that checks this mounts with
   Vue's real transitions (`stubs: { transition: false }`, as `BaseFormDialog.spec.ts` does), because
   a stubbed transition never finishes entering.
7. **Switch off.** Switching off asks first through `BaseConfirmDialog`; its confirm calls
   `store.setActive(id, false)` and hands the result to `session.showRefusalOf`. Reactivating asks
   nothing. When the entry's own data already says the laptop will refuse, the switch off is disabled
   and its tooltip names the reason, through `admin/components/BaseActionWithRefusalReason.vue` around
   `BaseTrashButton.vue` (the recipe popup does this for deleting an ingredient from
   `usedByItemNames`, and `StationsList.vue` from
   `hasItemsToHandOutAtTheRunningFestival` and `strandedItemCountWhenSwitchedOff` through
   `admin/composables/useStationRefusalReasons.ts`, naming the reason the laptop checks first when
   both apply; see `hideable-rows.md`). The tooltip is the
   text of the refusal the laptop would send, cut to its first sentence when the rest names controls
   the screen does not have. A
   refusal that comes back anyway is one page level `BaseRefusalAlert`
   (`shared/components/BaseRefusalAlert.vue`, `class="admin-section-gap"`), shown only while the
   form dialog is closed, because an open dialog shows its
   own refusal through `:error-text`.
8. **Delete.** An entry the laptop deletes for good is offered by `admin/components/BaseDeleteAction.vue`:
   the trash inside `BaseActionWithRefusalReason`, disabled with `refusal-reason` when the entry's
   own data says it is in use, and `BaseConfirmDialog` with the `confirm-title`, `confirm-body` and
   `confirm-label` the screen passes. `trash-test-name` names the trash and, with `-wrapper`, the
   tooltip's wrapper. It emits `asked` when the trash is pressed, so the screen forgets an earlier
   refusal, and `confirmed` once the admin confirms; the screen then calls the store's delete and
   shows a refusal where it shows its others (under the row through `useRowRefusals`, or under the
   unit chips). The screen calls the delete, not the component, because a successful delete reloads
   the list and removes the row, and the component with it, before the answer is back. A tap, Enter
   and Space on the trash stay inside it, so it works inside a clickable chip; every other key passes.
   Its question keeps the admin's button sizes inside a chip with small buttons, because
   `surfaceDefaults` nests the control defaults under `VDialog`. Consumers: the ingredient rows of
   the recipe popup and the unit chips of `ingredients/IngredientDialog.vue`.
9. **Texts.** `admin.<section>.{title,labels,actions,messages}` and, for refusals the laptop sends,
   `errors.admin.<section>.*`, in `shared/i18n/de.json` and `en.json` in the same change. Read
   `no-ai-slop` and `writing-ui-guidance` first.
10. **The laptop side.** The entity carries `[Raises(HubEvent.ConfigurationChanged)]`; the routes sit in
   `backend/GastronomyApp.Api/Endpoints/Admin<Things>Endpoints.cs` and call one method of the area's service each. See the `change-a-wire-contract` and
   `add-a-refusal` pages for the contract and the refusals.

## Specs

Copy the Ausgabestellen set in `frontend/tests/admin`: `stores/<things>.spec.ts` for the store, a
fixture module beside the list specs (`stationsListFixture.ts` gives `mountList` and the scenario
helpers), list specs split by scenario (`.creating`, `.emptyList`, `.refusals`, `.switchingOff`),
and a dialog spec (`StationDialog.spec.ts`). A failed load of the new tab is shown by the shared bar, so add a case for the tab to
`components/AdminLoadFailureBar.spec.ts` instead of testing a failure in the list. Add the store to `stores/screenListening.spec.ts` and the section to
`views/AdminShell.spec.ts` and `shared/router/adminRoute.spec.ts`. How to write them is in
`frontend-test-with-the-laptop`.
