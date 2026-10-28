# Add rows that are hidden, never deleted

Some admin lists hide their entries and show them again instead of deleting them: the festivals, a
festival's areas and an area's tables. They look and behave the same everywhere, through shared
pieces in `frontend/src/admin/components/`. Use these; never rebuild a row, a confirmation or a
tooltip beside them (frontend rule 8).

## The shared pieces

- `HideableEntryList.vue` draws a whole list of named entries the way the festival page's station
  card draws its rows. Its heading line takes the list's heading through the `heading` slot and puts
  "Ausgeblendete anzeigen" at its right; the `notices` slot sits between that line and the rows, for
  warnings about the whole list, and receives `showsHidden`, so a warning tells the admin to tick
  the box only while it is unticked. A row has the name as text, the "Ausgeblendet" chip, a
  `details` slot for what is particular to the entry (the area's table count and "Tischplan
  öffnen"), the up and down arrows when `moveThrough` is given, the pencil, the trash icon that
  hides or the open eye "Einblenden" on a hidden row, and a refusal under the row, flush with it.
  "Neu…" sits at the right below the rows. It owns the edit session (`useEditSession`), so "Neu…"
  and the pencil open the entity's dialog through the `dialog` slot, which receives `entry` (null
  for a new one), `errorText`, `save` and `cancel`. It takes the entries and how to read them (`idOf`, `nameOf`,
  `isHidden`), `hidingRefusalOf` (the reason the laptop would refuse to hide, or `null`), the words
  (`newLabel`, `hideTitle`, `hideBody`), `saveThrough`, `hideThrough`, `showThrough` and the optional
  `moveThrough` (store actions returning an `AdminActionResult`; `moveThrough` also receives whether
  "Ausgeblendete anzeigen" is ticked, which the move request carries so a move passes over the
  hidden entries the admin cannot see), `rowTestName` for the rows'
  `data-test`, and `isSelected` to highlight a row and scroll it into view. It emits `saved` with
  the answer and the mode, for a screen that selects what was just created. Consumers:
  `festivals/FestivalTablePlan.vue` (areas, with the arrows) and `tablePlan/TablePlanTableList.vue`
  (tables). Both put `tablePlan/TablePlanNameDialog.vue` in the slot: the name only, with the
  title, the field's label and the saved name as props, a limit of 40 characters, and the name as
  the draft it saves.
- `stations/RemovableStationRows.vue` draws station rows that are removed, not hidden: the name, a
  `badges` and an `actions` slot, the trash icon inside `BaseActionWithRefusalReason`
  (`removalRefusalOf`; the festival page's comes from `admin/composables/useStationRefusalReasons.ts`), the question before the removal, `removeThrough`, and the refusal under its
  row. Consumers: `festivals/FestivalStations.vue` and `tablePlan/TablePlanStationList.vue`.
- `BaseListRow.vue` is one row of any of these lists: the line between rows, the tint on every
  second row (`position`), the stronger tint of the selected row (`isSelected`), and scrolling a row
  into view when it becomes selected. `HideableEntryList`, `RemovableStationRows` and the article
  rows of `FestivalItems.vue` use it; a list grouped under headings numbers its rows on through the
  groups with `numberRowsAcrossGroups` (`admin/core/alternatingRows.ts`).
- `BaseHeadingWithCheckbox.vue` is a heading with its checkbox at the right of the same line, the
  way the Ausgabestellen, Artikel, Zutaten, Einheiten and Kellner tabs place "Deaktivierte anzeigen";
  the Feste tab places "Ausgeblendete Feste anzeigen" the same way. Consumers: the six global tabs
  and `HideableEntryList.vue`.
- `BaseActionWithRefusalReason.vue` disables an action the laptop would refuse and names the reason
  in a tooltip, as `admin-list-screen.md` step 7 describes. The slot gets `refused` for the button's
  `:disabled`; the wrapper carries the tooltip (`data-test="refusal-reason"`), because a disabled
  button sends no hover. Consumers: the article rows of `FestivalItems.vue` and of the Artikel tab,
  the ingredient rows of the Zutaten tab, the unit rows of the Einheiten tab, the station rows of
  the Ausgabestellen tab, the festival list's crossed-out eye, `HideableEntryList.vue` and
  `RemovableStationRows.vue`.
- `BaseTrashButton.vue` is the red trash icon of the festival page, the Artikel, Zutaten, Einheiten
  and Kellner tabs, the recipe dialog's lines, the hiding icon of `HideableEntryList.vue` and the
  removal icon of `RemovableStationRows.vue`.
- `shared/components/BaseIconAction.vue` is an icon button whose name shows as a tooltip
  (`data-test="icon-action-label"`): the crossed-out eye "Ausblenden" of the festival list, the
  categories' crossed-out eye "Kategorie abschalten" on the Artikel tab, and on the phone the
  details button at the end of every row of the shared order list. Its `location` prop (`top` by
  default, `bottom` for a control at the top of the screen) says where the tooltip opens; the
  waiter's bar (`AppHeader.vue`) uses it for the reload and the settings gear.
  `BaseEditButton.vue` is the pencil on it, named "Bearbeiten" (`admin.common.actions.edit`), the
  only way a row offers its edit dialog: every row of `HideableEntryList.vue`, the category
  headings and article rows of the Artikel tab and of the festival page, the station rows of the
  festival page and of the Ausgabestellen tab, the Zutaten and Einheiten tabs, the festival list (it
  opens the festival page) and the Kellner tab (it changes the name).
  `BaseShowButton.vue` is the open eye on it, with the word of its action as `label`: "Einblenden"
  on every hidden row (the festival list and `HideableEntryList.vue`), and "Artikel aktivieren",
  "Kategorie einschalten", "Zutat aktivieren", "Einheit aktivieren", "Ausgabestelle einschalten" and
  "Kellner wieder aufnehmen" for a deactivated entry of the global tabs.
- `BaseMoveButtons.vue` is the pair of arrows that emits `move` with `up` or `down`
  (`data-test="move-up"`, `"move-down"`). Consumers: the category headings of the Artikel tab and the
  area rows. Each store sends one move after the other through `admin/core/oneAtATime.ts`. A move
  swaps the row with the next row in that direction that is on the admin's screen: an area's move
  request carries the state of "Ausgeblendete anzeigen", and the Artikel tab shows every category,
  deactivated ones with their chip, so a category's move counts every category.
- `BaseHideConfirmDialog.vue` asks before hiding, with the festival list's "Ausblenden" confirm label
  and an optional sentence on what follows. Consumers: `FestivalsList.vue`, `HideableEntryList.vue`.
- `BaseRemoveConfirmDialog.vue` asks before removing, with "Entfernen". Consumers: the article rows
  of the festival page and `RemovableStationRows.vue`.
- `BaseSearchAddLine.vue` is the festival page's type-to-search field and add button, with a slot
  for the "Neu…" button beside them. Consumers: `FestivalStations.vue`, `FestivalItems.vue` and
  `tablePlan/TablePlanStationList.vue`.

## The order of a row's actions

Up and down first, then the pencil, then hiding or removing (the trash icon, the crossed-out eye,
or the open eye in their place). Text buttons, such as "Tischplan öffnen", "Zutaten bearbeiten",
"Kopieren", "Tablet einrichten" or "Telefon neu einrichten", come before the icons. A category row
reads up, down, pencil, crossed-out eye; an area row reads "Tischplan öffnen", up, down, pencil,
trash.

## Steps

1. The store needs `hide`, `show`, a create and a save action that reload through the latest
   request gate, and the view needs whatever says the laptop would refuse to hide (the table plan
   carries `hasItemsToHandOutOrSettleAtTheRunningFestival` per area and table for that, already
   false unless the festival runs, so the screen reads it alone). Entries the admin orders get a
   move action as well, whose request says whether hidden entries are on screen.
2. Write the entity's dialog on `BaseFormDialog` the way `StationDialog.vue` is written: the fields,
   `:save-disabled` while a required value is missing, `:error-text` for the laptop's refusal. An
   entity that has only a name uses `TablePlanNameDialog.vue` instead of a dialog of its own.
3. Mount `HideableEntryList` with the entries, the words and the actions, put the heading in its
   `heading` slot and the dialog in its `dialog` slot.

## Specs

`tests/admin/components/festivals/FestivalPage.tablePlan.spec.ts` and `.tablePlanAreas.spec.ts`
(areas) and `tests/admin/components/tablePlan/TablePlanEditor.tables.spec.ts` (tables) show the
rows and the order of their actions (read the matching `data-test` values of one row in document
order), the dialog (`saveTheTablePlanNameDialog` in `tests/support/formDialogs.ts`), the disabled
trash icon with its tooltip (hover the `hide-entry-wrapper`, then read `refusalReasonsOnScreen()`
from `tests/support/dom.ts`), the pencil and the open eye (hover it, then read
`iconActionLabelsOnScreen()`), the arrows, the confirmation and the checkbox. The pencil and the
eye of the global tabs are in `FestivalsList.spec.ts`, `ItemsList.categories.spec.ts`,
`ItemsList.items.spec.ts`, `IngredientsList.listing.spec.ts`, `UnitsList.listing.spec.ts`,
`StationsList.switchingOff.spec.ts` and `StaffList.spec.ts`; `UnitsList.switchingOff.spec.ts` covers
the Einheiten tab's disabled deactivation with its tooltip and its confirmation.
