# Add a global admin tab

A global tab lists one kind of thing with a form to create and edit it and a switch off that can be
undone. The Zutaten tab, `frontend/src/admin/components/ingredients/IngredientsList.vue`, is the one
to copy. Of the global tabs only it opens its form through `useEditSession`: `StaffList.vue` and
`StationsList.vue` use `useRefusalDisplay` directly, and the stations store is built on
`defineFestivalScopedList` instead of `defineAdminList`.

## Steps

1. **Route.** Add the name to `AdminSection` and to `ADMIN_SECTIONS` in
   `frontend/src/shared/router/route.ts`. The order of `ADMIN_SECTIONS` is the order of the tabs, and
   the path is `/admin/<section>`.
2. **Mount it.** In `frontend/src/admin/views/AdminShell.vue` import the list component, add its
   `case` to `titleFor` (it ends in `assertNever`, so a missing case fails the type check) and add
   one `v-else-if="section === '<section>'"` line. The tab itself comes from `ADMIN_SECTIONS`.
3. **Store.** `frontend/src/admin/stores/<things>.ts` is a Pinia store over `defineAdminList` from
   `stores/adminList.ts`: pass `path`, the zod `listSchema` and `entrySchema` from
   `shared/api/generatedSchemas`, `entriesOf`, `idOf` and `requestBodyOf`, and return `entries`,
   `loadFailed`, `load`, `create`, `update` (the Zutaten store wraps it as `save`), `setActive` plus a
   `listen()`. A list that depends on
   the running festival uses `defineFestivalScopedList` (`stores/festivalScopedList.ts`) instead.
   The factory fixes the routes the laptop must offer: `GET <path>` answers the list view,
   `POST <path>` answers the created entry, `PUT <path>/{id}`, `POST <path>/{id}/activate` and
   `POST <path>/{id}/deactivate` answer any success. Loads go through the latest request gate.
4. **Reload.** `listen()` returns `useConnectionStore().listenToTheLaptop(['ConfigurationChanged'], load)`.
   A list whose entries carry a flag derived from orders (the stations'
   `hasItemsToHandOutAtTheRunningFestival`, the table plan's `hasItemsToHandOutOrSettle`) listens to `OrdersChanged` too, or a button the flag
   disables stays wrong until the screen is opened again. The component calls it in `onMounted`,
   then `await store.load()`, and calls the returned release in `onUnmounted`. Why the events, and
   what the entity owes the laptop side, is in the `architecture` skill.
5. **The list component.** The heading is `BaseHeadingWithCheckbox`
   (`admin/components/BaseHeadingWithCheckbox.vue`) with the `h1` in its slot and its checkbox on a
   `showsDeactivated` ref (`checkbox-test-name="show-deactivated"`, text
   `admin.common.actions.showDeactivated`, in German "Deaktivierte anzeigen"), which decides whether
   deactivated entries are listed, so a row is listed when `isActive || showsDeactivated`. A `v-chip` with `admin.common.labels.deactivated` marks a deactivated
   entry, which offers activating in place of switching off. A `v-alert` with
   `admin.common.errors.loadFailed` reports `store.loadFailed`, and the text for an empty list shows
   only when the list is empty and the load did not fail.
6. **Create and edit.** `useEditSession` (`admin/composables/useEditSession.ts`) takes a `saveThrough`
   that creates when nothing is being edited and saves otherwise. The id field has the entity's own
   name:

   ```ts
   useEditSession<AdminIngredientView, IngredientDraft, AdminIngredientView | null>({
     saveThrough: (draft, ingredient) =>
       ingredient === null
         ? ingredients.create(draft)
         : ingredients.save(ingredient.ingredientId, draft),
   })
   ```

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
   `BaseTrashButton.vue` (`IngredientsList.vue` does this from `activeItemNames`, and
   `StationsList.vue` from `hasItemsToHandOutAtTheRunningFestival` and
   `strandedItemCountWhenSwitchedOff` through `admin/composables/useStationRefusalReasons.ts`, naming
   the reason the laptop checks first when both apply; see `hideable-rows.md`). The tooltip is the
   text of the refusal the laptop would send, cut to its first sentence when the rest names controls
   the screen does not have. A
   refusal that comes back anyway is one page level `v-alert` (`type="warning"`,
   `data-test="refusal"`), shown only while the form dialog is closed, because an open dialog shows its
   own refusal through `:error-text`.
8. **Texts.** `admin.<section>.{title,labels,actions,messages}` and, for refusals the laptop sends,
   `errors.admin.<section>.*`, in `shared/i18n/de.json` and `en.json` in the same change. Read
   `no-ai-slop` and `writing-ui-guidance` first.
9. **The laptop side.** The entity carries `[Raises(HubEvent.ConfigurationChanged)]`; the routes sit in
   `backend/GastronomyApp.Api/Endpoints/Admin<Things>Endpoints.cs`. See the `change-a-wire-contract` and
   `add-a-refusal` pages for the contract and the refusals.

## Specs

Copy the Zutaten set in `frontend/tests/admin`: `stores/<things>.spec.ts` for the store, a fixture module
beside the list specs (`ingredientsListFixture.ts` gives the stubbed laptop and `mountList`), list
specs split by scenario (`.listing`, `.editing`, `.switchingOff`), and a dialog spec. Add the store to
`stores/screenListening.spec.ts`. How to write them is in `frontend-test-with-the-laptop`.
