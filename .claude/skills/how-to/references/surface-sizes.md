# Size a control, a heading or a text on the phone, the station tablet or the admin

Each of the three surfaces draws its controls and texts at its own size: the phone is held in one
hand at a loud festival and needs thumb-sized targets, the station tablet is read across a counter
and needs large text on its order cards, and the admin is read at a desk on the laptop and is
compact. Every size comes from the surface's own sizes files, and no component carries a height, a
padding, a density or a font size that its surface should decide.

## Where the sizes live

- `frontend/src/shared/styles/appVuetify.ts` is the one Vuetify setup. `APP_VUETIFY_OPTIONS` holds
  what every surface shares (buttons `flat`, fields `outlined` with `hideDetails: 'auto'`) and the
  alias `VBtnWide`. `main.ts` and `tests/setup.ts` both create Vuetify from it, so a spec sees the
  same defaults as the app. It also exports `surfaceDefaults(sizesClass, controlDefaults)`, which
  every surface uses to build its defaults (see Overlays below).
- **Phone:** `src/phone/styles/phoneSizes.ts` (`PHONE_DEFAULTS`: large buttons, comfortable
  fields, selects, checkboxes and lists, the app bar at 72px, `VBtnWide` at `x-large`, `flat` and
  `block`) and `phoneSizes.css` (the class `phone-sizes`). `App.vue` puts the class on `v-app` and
  wraps the app in `<v-defaults-provider :defaults="PHONE_DEFAULTS">` while
  `isDrawnAtPhoneSizes(screen)` from `shared/core/landing.ts` says so: on the five waiter screens
  and on the start screens (`DoorGate`, `EnrolQr`, `Welcome`, `StartingUp`), which are shown before
  the device knows whose it is.
- **Station tablet:** `src/station/styles/stationSizes.ts` (`STATION_DEFAULTS`: large buttons and
  chips, comfortable selects) and `stationSizes.css` (the class `station-sizes`). `StationPage.vue`
  wraps itself in the provider and carries the class on its root.
- **Admin:** `src/admin/styles/adminSizes.ts` (`ADMIN_DEFAULTS`) and `adminSizes.css` (the class
  `admin-sizes` with its `--admin-*` properties and `admin-*` classes). `AdminShell.vue` wraps
  itself in the provider and carries the class on its root.

## Texts on the phone and the station tablet

`src/shared/styles/surfaceText.css`, imported once by `main.ts`, holds the named text classes. Each
reads a custom property that `phone-sizes` and `station-sizes` set to their own value, so a shared
component such as `Welcome.vue` writes one class and takes the size of the surface it is on.

| Class | Use it for | Property |
|---|---|---|
| `app-page-title` | the title of a screen, the app bar title | `--app-page-title-size` |
| `app-section-title` | a section or column heading, the title of a card (the table on a station card) | `--app-section-title-size` |
| `app-item-name` | the name of an article or an order line | `--app-item-name-size` |
| `app-figure` | an amount read at a glance (order total, selected total, a price) | `--app-figure-size` |
| `app-text` | body text on an element that would otherwise get another size (a heading, a `v-card-text`, a button) | `--app-text-size` |
| `app-small-text` | a secondary line: a note, who took the order, a time, a column heading | `--app-small-text-size` |

Plain text inside a surface needs no class: the surface class sets `font-size` to
`--app-text-size`. A scoped style that needs one of these sizes reads the property
(`font-size: var(--app-text-size)`), never a number. Vuetify 4 has no `text-h4`, `text-h6`,
`text-body-1`, `text-body-2`, `text-subtitle-1`, `text-subtitle-2`, `text-caption` or
`text-overline`; never write them. The utility classes `font-weight-medium`, `font-weight-bold` and
`text-medium-emphasis` still exist and combine with the classes above.

## Steps

1. **A Vuetify control** takes its size from its surface's defaults. Write no `size` or `density`
   on it, and no `variant` or `hide-details` that only repeats `APP_VUETIFY_OPTIONS`. A control the
   surface does not cover yet gets its line in that surface's `*_CONTROL_DEFAULTS`.
2. **A full-width button on the phone** (the steps of an order, the actions of a sheet, the start
   screens, the category tiles) is `<v-btn-wide>`. It takes `x-large`, `flat` and `block` from
   `PHONE_DEFAULTS`; write only `color`, a `variant` other than `flat`, and its handlers. Every other
   button is `<v-btn>` at the surface size.
3. **A component that must differ from its surface** gets a named defaults object in the surface's
   sizes file and wraps the controls in `<v-defaults-provider>` with it, never a prop at the place
   of use. Today: `PHONE_NOTICE_BAR_DEFAULTS` (the notice bars under the phone's app bar, compact
   and square) and `PHONE_ORDER_GRID_DEFAULTS` (the tick boxes compact and the details button
   comfortable inside the dense grid of `OrderListCard`).
4. **A heading or a text** uses one of the classes above on the phone and the station tablet, and
   the `admin-*` classes in the admin: `admin-page-title`, `admin-section-title`, `admin-row-name`
   and `admin-small-text`.
5. **An admin list row, card or section** uses the admin layout classes: a row is
   `d-flex align-center flex-wrap admin-row`, the facts under it `admin-row-details`, a row card
   is separated by `admin-row-spacing`, a card body is `admin-card-body`, sections are separated by
   `admin-section-gap` and a button below a list keeps `admin-block-gap`. A scoped admin style reads
   the `--admin-*` property (`padding: var(--admin-row-padding-y) var(--admin-row-padding-x)`).
6. **A small Vuetify spacing class** such as `mb-2`, `mt-3` or `ga-2` between two neighbours stays
   as it is, and so does a layout size that belongs to one component's own geometry, such as the
   minimum height of a catalog row.
7. **A shared component** under `src/shared/` is never forked per surface. It writes no size of its
   own, so each surface's provider and class decide how it looks.
8. **A drawing** keeps its own scale: a Konva stage computes its scale from the frame it is given.

## Overlays

Dialogs, bottom sheets, menus and tooltips are drawn outside the surface's root, so
`surfaceDefaults` gives them the surface class too: `contentClass` on `VDialog`, `VBottomSheet`,
`VMenu` and `VTooltip`, and `menuProps.contentClass` on `VSelect`, `VAutocomplete` and `VCombobox`,
whose own menus would otherwise miss it. A control that passes its own `content-class` or
`menu-props` replaces that default and must include the surface class itself.

`VDialog` and `VMenu` restart Vuetify's defaults from the app's root for their content and keep only
what is nested under their own name. `surfaceDefaults` therefore nests the control defaults under
`VDialog` and `VMenu`, and again inside those, down to three levels (a dialog opened from a dialog,
with a select menu inside). Add a control's defaults to the surface's `*_CONTROL_DEFAULTS`, never
next to the built defaults, so they reach every overlay.

## Tests

- `frontend/tests/SurfaceSizes.spec.ts`: the phone screens carry `phone-sizes`, a phone button is
  large, a phone dialog and a bottom sheet carry `phone-sizes` with comfortable fields and
  `x-large` wide buttons; the station page carries `station-sizes`, its buttons are large, its
  language choice is comfortable, a card's table name uses `app-section-title` and the done
  dialog carries `station-sizes`; the admin opened in the app carries no `phone-sizes` and keeps
  its own sizes.
- `frontend/tests/admin/views/AdminShell.compactSizes.spec.ts`: the admin's buttons, shared icon
  action, dialogs, select menus, plain menus, tooltips and dialog fields are compact and carry
  `admin-sizes`, and a dialog opened from the phone header does not.
- `frontend/tests/shared/core/landing.spec.ts`: which screens `isDrawnAtPhoneSizes` draws at the
  phone sizes.

jsdom cannot measure layout, so a change to a value is checked by eye in the running app.
