# The back button

On the phone, the station tablet and the laptop, the hardware or browser back button goes one step
back inside the app, without loading it again. A step is a screen, a dialog or a menu the person
tapped into: on the phone, back from the order screen shows the sheet of the table it was opened
from, then the plan of that area, then the area list. Back with a dialog or a menu open closes it and
stays on the screen. From a first screen (the area list or the plan of the only area, the station
screen, the festival list in the admin) back reaches the back-button trap, which returns the device
to the app. A tab opens through the start screen (`DoorGate.vue`), whose one tap builds that trap.

## Where the code lives

Frontend, under `frontend/src/shared/`:

- `router/route.ts`: the routes and `parentRouteOf(route)`, the parent table. The waiter's home is a
  first screen; the order screen, the settle view and the orders of a table sit one level under it,
  the summary under the order screen. The station screen and an invitation are first screens. In
  the admin the festival list is the first screen, every other tab and the page of one festival sit
  one level under it, and the editor of an area under the page of its festival. `isSameRoute`
  compares two routes. The waiter's header (`phone/components/header/AppHeader.vue`) takes its way
  up from `parentRouteOf`: "Tischplan" where the parent is home, and on home itself "Tischpläne" over
  the plan of an area when the festival has more than one area.
- `router/historyMoves.ts`, plain TypeScript without Vue. Every history entry records a `Place`: the
  route and the steps open on it, each a `BackStep` of kind `level` (an open area, an open category)
  or `dialog` (the sheet of a table, a dialog, a menu). `historyMoveFor` decides what a change does
  to the history: `add` an entry, `replace` the current one, `goBack` to an entry the history
  already holds, `goBackAndReplace` the entry above a parent further down, or `stay`. It is given the
  current entry minus the steps closed since it was written (`currentEntryMinusClosedSteps`), so a
  dialog that closes with the move counts. `aTapMayAddAnEntry` says whether the change may add an
  entry, and `placeToOpen` which steps a screen opens with.
- `router/router.ts`: `navigate(path, reason)` with the reason `'tap'` (the default) or
  `'redirect'`, `openStep(step, onBack)` where `onBack` is a `BackHandler` (`close` and
  `refusesBack`), `closeStep(name)`, `openStepValue(name)`, `currentRoute`, `parentRouteOf`,
  `startRouter` and `startOverAt`. It shows a change at once and writes the history after Vue's next
  render. It keeps a copy of this document's entries, stamps each entry with its number, follows a
  back or forward press through `popstate`, and tells the trap the address shown once a move has
  settled.
- `composables/useBackStep.ts`: `useBackStep(close, { isOpen, refusesBackWhile })` gives a dialog
  its step, and `useBackStepForAMenu()` gives a `v-menu` its open state (bound with `v-model`) with a
  step. `BaseConfirmDialog.vue` and `BaseFormDialog.vue` call `useBackStep`, so back works like their
  cancel and is refused while the form dialog is `busy`. The other dialogs call it themselves:
  `SettingsSheet`, `UnsentOrderDialog`, `LineStationSheet`, `ItemNoteDialog` (with its own
  `isOpen`), `AmountPaidDialog` (refused while it settles), `CancelItemsDialog` (refused while it
  cancels), `ItemHistoryDialog` and `StationDoneDialog`. The menus: the station choice of an
  article (`admin/components/festivals/StationSelect.vue`) and the two menus of
  `admin/components/tablePlan/TablePlanToolbar.vue`. The menu of `TablePlanDrawing.vue` has no step
  yet.
- The steps of the phone: `phone/stores/tablePlan.ts` keeps the opened area as the step `area`,
  `phone/views/TablePlan.vue` the sheet of a tapped table as the step `table` (kind `dialog`), and
  `phone/views/Catalog.vue` the open category as the step `category`.
- Sending: `phone/composables/useSendAndLeave.ts` decides where the phone goes once the laptop
  accepted an order, in the continuation of the send the waiter started, whichever screen is on
  display when the answer comes.
- The trap: `router/backButtonTrap.ts`, beside the router and never importing it,
  `views/DoorGate.vue`, `frontend/public/door.html` and the boot script in `frontend/index.html`; the
  frontend `CLAUDE.md` says how it works. `returnPathFor` there is where a reload through the doors
  lands, which is not the parent of a screen: the order flow starts again at home, as
  `routeToStartAt` in `route.ts` does after any reload.

## Invariants

- **Only a tap adds an entry, one per level.** A move one level deeper (a screen whose parent is on
  display, or a step opened on the screen on display) adds an entry only while
  `navigator.userActivation.isActive`, and not after a back or forward press until the next tap,
  because Chrome then stops counting the earlier activation. Every other change replaces the current
  entry. A browser without the API always replaces, so back falls through to the trap, which returns
  the device to the app; back never leaves the app.
- **A move up adds nothing.** A move to a screen the history already holds goes back to its entry,
  with the levels it had and without its dialogs: "Tischplan" on the order screen returns to the
  plan of the area and "Tischpläne" on the plan returns to the area list, and the next back goes on
  up from there.
- **Siblings replace each other.** A move to a screen with the same parent replaces the entry; when
  the parent lies further down, the router goes back to the entry above it and replaces that one.
  The admin tabs therefore replace one another, and back from any tab or from a festival's page lands
  on the festival list.
- **A redirect never adds an entry.** A move a screen makes on its own passes `'redirect'`: the
  locked order to the summary, an order screen without a table, a settle view or an order overview
  without a table, an unknown festival or area, and the move after sending.
- **Once the laptop accepts an order, the phone goes where sending leads**, also when back was
  pressed while the order was on its way. "Senden und später kassieren" lands on the plan of the
  area the order was taken in, with that area still open (the area list or the plan of the only
  area when no area was open); "Senden und kassieren" puts the settle view in place of the order
  screen, so back from it reaches the sheet of the table. Back never reaches the sent order.
- **Forward never shows what the app itself left behind.** Entries the router went back past or
  wrote over (a sent order's screens, a dialog or menu closed by its own button, the screen a way up
  left) are dropped from its copy, and a forward press onto one returns at once. Forward still
  returns to an entry the person went back from, as long as nothing was written since: the sheet of
  a table opens again, because the router holds it; a dialog or a menu does not, because its owner
  holds whether it is open.
- **A dialog closed by its own button takes its entry with it**, and so does a menu closed by a
  selection or a click outside. A dialog whose button leads to another screen gives its entry to
  that screen. The sheet of a table stays in the history when one of its buttons leads on, so back
  returns to it.
- **A dialog can refuse back.** While a form dialog saves, the amount page settles or the cancel
  page cancels, back keeps the dialog open: the router steps forward to its entry again, so the next
  back after the answer closes it.
- **The trap returns to the address shown.** After every settled move (a tap, a redirect, a way up,
  a back or forward press) the trap holds the address on display, so a reload through the doors
  lands where the person was.
- **Back never touches the draft.** Leaving the order screen by back keeps the unsent order in
  `localStorage` exactly as the way up does, and nothing here loads the page again.
- **The station screen is one screen.** Back closes the hand-out confirmation when it is open and
  otherwise reaches the trap.

## Tests

- `frontend/tests/shared/router/historyMoves.spec.ts` (deeper, siblings, up, after sending, a
  redirect, a dialog closed by its button or giving its entry away, nothing changed; whether a tap
  may add an entry: active, passed, no API, after a back press; the steps a screen opens with),
  `parentRoutes.spec.ts` (the parent table) and `historySteps.spec.ts` (the router on the jsdom
  history: a tapped move, no API, a tap after a back press, a redirect, a move up, a step closed by
  back and by its own button, the address the doors return to after a tap and after a way up, a
  forward press after a way up).
- Mounted: `frontend/tests/AppBackPressOnThePhone.spec.ts` (the sheet to the plan with the area
  kept, the order screen to the area list one step at a time, the trap from the first screen, a
  dialog, the way up and then back, the draft kept, "Tischpläne" and back),
  `AppBackPressAfterSending.spec.ts` (the landing on the area's plan, back pressed while the order
  was on its way, back after sending, forward after sending, "Senden und kassieren" and three back
  presses), `AppBackPressInADialog.spec.ts` (every dialog, the busy form dialog, the amount page
  while it settles, the cancel page while it cancels), `AppBackPressInAMenu.spec.ts` (the station
  choice, both toolbar menus, a menu closed by its selection) and `AppBackPressInTheAdmin.spec.ts`
  (tabs and then back, the trap from the festival list, the editor of an area, its way back and then
  back), on `tests/waiterPhoneBackPressFixture.ts` and `tests/support/browserHistory.ts`.
- The trap: `shared/router/doorPage.spec.ts` and `backButtonTrapArming.spec.ts`.
- Whether a real Chrome or Safari skips an entry is not something jsdom can show; check that on a
  phone.
