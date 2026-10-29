# Size a control or a row on an admin screen

The admin is drawn smaller than the phone and the station tablet: a laptop screen read at a desk
needs no thumb-sized targets. Every admin size comes from one folder,
`frontend/src/admin/styles/`, and no admin component carries a height, a padding, a gap or a font
size of its own.

## Where the sizes live

- `adminSizes.ts` holds `ADMIN_DEFAULTS`, the Vuetify defaults for everything inside the admin:
  compact density for fields, selects, checkboxes, switches, lists, tables, the toolbar, the tabs
  and alerts, small chips, and buttons at `size: 'default'` with `density: 'comfortable'` (28px for
  a text button, 36px for an icon button). `AdminShell.vue` wraps the whole admin in
  `<v-defaults-provider :defaults="ADMIN_DEFAULTS">`.
- `adminSizes.css`, imported once by `AdminShell.vue`, defines the CSS custom properties on the
  class `admin-sizes` (`--admin-text-size`, `--admin-small-text-size`, `--admin-page-title-size`,
  `--admin-section-title-size`, `--admin-row-name-size`, `--admin-row-height`,
  `--admin-row-padding-y`, `--admin-row-padding-x`, `--admin-row-gap`, `--admin-row-spacing`,
  `--admin-card-padding`, `--admin-section-gap`, `--admin-block-gap`, `--admin-page-padding`) and
  the classes that use them.
- The class `admin-sizes` sits on the shell's root element. Dialogs, menus and tooltips are drawn
  outside the shell, so `ADMIN_DEFAULTS` gives them the class too: `contentClass` on `VDialog`,
  `VMenu` and `VTooltip`, and `menuProps.contentClass` on `VSelect`, `VAutocomplete` and
  `VCombobox`, whose own menus would otherwise miss it. A control that passes its own
  `content-class` or `menu-props` replaces that default and must include `admin-sizes` itself.
- `VDialog` and `VMenu` restart Vuetify's defaults from the app's root for their content and keep
  only what is nested under their own name. `ADMIN_DEFAULTS` is therefore built by
  `overlayDefaults`, which nests the control defaults under `VDialog` and `VMenu`, and again
  inside those, down to `DEEPEST_OVERLAY_NESTING` levels (a dialog opened from a dialog, with a
  select menu inside). Add a new control's defaults to `ADMIN_CONTROL_DEFAULTS`, never next to
  `ADMIN_DEFAULTS`, so they reach every overlay. Nothing outside the admin carries the class, which keeps the phone
  and the station tablet at their own sizes.

## Steps

1. **A Vuetify control** takes its size from `ADMIN_DEFAULTS`. Write no `density` or `size` on it.
   A component the admin does not cover yet gets its line in `ADMIN_CONTROL_DEFAULTS`, never a prop
   at the place of use.
2. **A heading or a text** uses a class: `admin-page-title` for the `h1` of a screen,
   `admin-section-title` for a card's `h2` or a category label, `admin-row-name` for the name in a
   list row and `admin-small-text` for a secondary line. Plain text needs no class: it inherits
   `--admin-text-size`. Vuetify 4 has no `text-h5`, `text-h6`, `text-body-1` or `text-subtitle-1`,
   so never write those.
3. **A list row** is `d-flex align-center flex-wrap admin-row`, a line of facts under it is
   `admin-row-details`, and a row card is separated from the next by `admin-row-spacing`.
4. **A card or a section** pads its body with `admin-card-body`, is separated from the next section
   by `admin-section-gap`, and a button below a list keeps its distance with `admin-block-gap`.
5. **A scoped style** that needs one of these sizes reads the custom property
   (`padding: var(--admin-row-padding-y) var(--admin-row-padding-x)`), never a number. A small
   Vuetify spacing class such as `mb-2`, `mt-3` or `ga-2` between two neighbours stays as it is.
6. **A shared component** under `src/shared/` that the phone also uses is never forked for the
   admin. It writes no size of its own, so inside the admin the defaults provider makes it compact
   and on the phone it keeps the app's defaults from `main.ts`.
7. **A drawing** keeps its own scale: the table plan editor's Konva stage computes its scale from
   the frame it is given, and only the frame and the toolbar around it take the admin sizes.

## Tests

`frontend/tests/admin/views/AdminShell.compactSizes.spec.ts` checks the sizes on both sides:

- a button and the shared `BaseIconAction` render compact inside the shell, and the same
  `BaseIconAction` keeps the default density in the phone's header;
- a dialog, a select's menu, a plain menu and a tooltip opened in the admin carry `admin-sizes` on
  their content, and a field inside an admin dialog is compact;
- a dialog opened on the phone does not carry the class.

`tests/setup.ts` gives the test Vuetify an empty `defaults` object, as `main.ts` gives the app its
defaults. Without one, a dialog's root reset leaves its content with no defaults at all, and the
overlay tests would prove nothing.

jsdom cannot measure layout, so a change to a value is checked by eye in the running app.
