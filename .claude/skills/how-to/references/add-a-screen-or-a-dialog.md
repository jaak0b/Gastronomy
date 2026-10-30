# Add a screen, a level inside a screen, a dialog or a menu

The back button goes one step up inside the app, as the `back-button` page of `features` describes.
A new screen, dialog or menu joins that by saying where it sits. Nothing outside `shared/router/`
touches `window.history`.

## A screen with its own route

1. Add the route to `AppRoute` and `resolveRoute` in `frontend/src/shared/router/route.ts`.
2. Give it its parent in `parentRouteOf` in the same file: `null` for a first screen, otherwise the
   screen one level up. The switch ends in `assertNever`, so a missing case fails the type check. A
   new admin tab needs nothing here: every admin section except the festival list has the festival
   list as its parent. The waiter's header reads the same table, so a new waiter screen under home
   gets "Tischplan" without a change in `AppHeader.vue`.
3. Open it with `navigate(path)` from the tap. A move the screen makes on its own (a guard, the
   landing after the laptop answered) passes `navigate(path, 'redirect')`, which never adds an entry.
4. A way up on the screen ("Zurück", "Tischplan") is `navigate` to the parent's path. The router
   goes back to that entry when the history holds it, so the next back goes further up.

## A level inside a screen

Something the person opens on a screen without a route of its own (an area of the plan, a category,
a sheet) is a step. Open it with `openStep({ name, value, kind })` on the tap, close it with
`closeStep(name)` from its own button, and let the screen read `openStepValue(name)` instead of a ref
of its own, so back closes it and a back press to an older entry shows it again. A way up keeps a
`kind: 'level'` step and drops a `kind: 'dialog'` one. Keep the name in a constant beside the code
that reads it, as `AREA_STEP` in `phone/stores/tablePlan.ts`.

## A dialog

- A dialog built on `BaseConfirmDialog` or `BaseFormDialog` has its step already: back emits
  `cancel`, and is refused while the form dialog is `busy`.
- Every other dialog calls `useBackStep(close)` from `shared/composables/useBackStep.ts` in its
  setup, with what its own close button does. While that button is disabled, back is refused the
  same way: pass `{ refusesBackWhile: () => props.isSettling }` as `AmountPaidDialog` does, and the
  router keeps the dialog and its step.
- A dialog that stays mounted and opens through a model passes the model too, as `ItemNoteDialog`
  does: `useBackStep(() => { isOpen.value = false }, { isOpen })`.
- A dialog mounted with `v-if` closes its step when it unmounts, so closing it by its own button
  needs nothing more, and a button that leads to another screen just navigates.

## A menu or a dropdown list

- A `v-menu` takes its open state from `useBackStepForAMenu()` and binds it with `v-model`:
  `const stationChoiceIsOpen = useBackStepForAMenu()` and `<v-menu v-model="stationChoiceIsOpen">`,
  as `admin/components/festivals/StationSelect.vue` does.
- A `v-select` or `v-autocomplete` binds the same open state to its dropdown list with
  `v-model:menu`, one call per field: `<v-select v-model:menu="categoryListIsOpen">`, as
  `admin/components/items/ItemDialog.vue` does. Back then closes only the list, and the
  dialog or screen under it stays.
- A menu shown under its own `v-if` goes into a component of its own, rendered under that `v-if`,
  as `TablePlanToolbarHiddenTables.vue` is, so its open state ends with the menu.
- Back closes the menu; a selection or a click outside closes it and takes its step away again.

## Tests

- A new parent is one row in `frontend/tests/shared/router/parentRoutes.spec.ts`.
- A new dialog, menu or dropdown list gets a mounted test in `tests/AppBackPressInADialog.spec.ts`,
  `tests/AppBackPressInAMenu.spec.ts` or `tests/AppBackPressInADropdownList.spec.ts`:
  `shownOnAStartedRouterAt(path, load, props)` from `tests/support/browserHistory.ts` mounts it on
  a started router, then `pressBack()`, and the test reads what it emitted, whether its menu or list
  still shows (`isVisibleOnScreen` from `tests/support/dom.ts`), and `theAppWasLeftByBack()`.
- What back does across screens is a mounted spec built like `tests/AppBackPressOnThePhone.spec.ts`
  on `tests/waiterPhoneBackPressFixture.ts`: `aTabThatOpenedTheAppAt(path)` and
  `aBrowserThatCountsTheTap()` after `vi.resetModules()`, `startRouter()` from the freshly imported
  router, tap through the screens, then `pressBack()`. Wait for a move the router makes on its own
  with `theNextMoveThroughTheHistory()` before the next press. jsdom has no user activation of its
  own, so every spec that does not stub it replaces the entry on every move.
