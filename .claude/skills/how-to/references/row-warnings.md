# Show a warning under a row

A refused action on one row of a list shows its sentence under that row and under no other. A refused
action on a whole screen or dialog shows one sentence in one place. Both read the refusal from the
answer of the laptop and turn it into text the same way.

## One warning per row: `useRowRefusals`

`frontend/src/admin/composables/useRowRefusals.ts`. Call it once in the component or composable that
owns the rows; it keeps one refusal per row id.

- `refusalTextOf(rowId)` is the sentence to render, or `null`. Pass it to the row as
  `BaseListRow`'s `refusal-text`, which draws it under the line, flush with the row; a line that
  is not a list row renders `BaseRefusalAlert` itself with `class="mb-2"`.
- `showRefusalOf(rowId, result)` takes the `AdminActionResult` a store action returned and stores the
  refusal when the action failed. It does nothing for a success, so an old warning survives an
  accepted action unless it was forgotten first.
- `refuse(rowId, messageKey, parameters?)` stores a refusal the screen found itself, before any
  request, such as an amount that is not a number.
- `forgetRefusalOf(rowId)` removes the warning. Call it before every new action on the row, and as
  soon as the person edits the row again (typing in its field), never only when a save succeeds.
- `isRefused(rowId)` says whether the row carries a warning.

Current consumers: `admin/composables/useFestivalItemRows.ts` (rows keyed by item id),
`admin/components/items/ItemIngredientsDialog.vue` (one for the recipe lines and one for the rows
of all ingredients, both keyed by ingredient id; a row's warning also goes when its amount row
closes),
`admin/components/festivals/FestivalStock.vue` (stock rows keyed by ingredient id),
`admin/components/HideableEntryList.vue` (the table plan's areas, keyed by their id; see
`hideable-rows.md`) and `admin/components/stations/RemovableStationRows.vue` (the station rows of the
festival page, keyed by station id).

Keep the rows' state in a component that stays mounted while the person moves between rows. A
refusal kept by a component that is keyed on the selection is lost unseen when the person selects
another row before the laptop answers.

## One warning per screen: `useRefusalDisplay`

`admin/composables/useRefusalDisplay.ts` is a single slot: `refusal`, `refusalText` and
`showRefusalOf(result)`, which returns `true` when the result was a refusal and clears the slot when
it was not. Clear it by setting `refusal.value = null`. `useEditSession` owns one for its dialog.
Use the row version only where several rows can carry a warning at once.

## A standing warning: `BaseWarningText`

A warning that is not an answer to an action, but a gap the screen finds in its data (an
ingredient that runs out soon, a table plan the waiters cannot use yet), is
`admin/components/BaseWarningText.vue`: the `mdi-alert` icon beside its `text` in the `warning`
colour, no box. Consumers: the stock rows of `festivals/FestivalStock.vue` and the warnings of the
Tischplan card in `festivals/FestivalTablePlan.vue`. A refusal stays a `BaseRefusalAlert`.

## The refusal itself: `BaseRefusalAlert`

`shared/components/BaseRefusalAlert.vue` is every refusal on screen: the warning box with its
`text`, `data-test="refusal"` unless the place gives it another name (`item-refusal`,
`category-refusal`, `plan-refusal`). The place adds its `v-if` and its spacing class
(`admin-section-gap` on a page, `mt-3` under a card's add line, `mt-4` in `BaseFormDialog`). Never
write the `v-alert` by hand. Consumers: `BaseListRow`, `BaseFormDialog`, every global tab, the
festival page and its cards, `ItemIngredientsDialog`, `IngredientDialog` (a refused unit delete,
`unit-refusal`, under the unit chips) and the table plan editor.

## One mapping to text: `adminErrorMessageText`

`admin/core/adminErrorMessage.ts` is the only place a refusal becomes a sentence. A store action
returns `{ kind: 'failed', message }` (`admin/core/adminActionResult.ts`), where `message` was built
by `adminErrorMessage(body)` from the laptop's `ApiErrorBody`: a missing body, or a `messageKey` the
German locale file does not hold, becomes `common.errors.actionFailed`. `adminErrorMessageText(t,
message)` translates it with its parameters, and also passes the count whenever the message has one:
`adminErrorMessage` takes it from a `count` parameter that is a whole number (or a string holding
one) and leaves it `null` otherwise. A component never calls `t(message.key)` on its own.

## Specs

`frontend/tests/admin/composables/useRowRefusals.spec.ts`, `useRefusalDisplay.spec.ts` and
`tests/admin/core/adminErrorMessage.spec.ts` cover the pieces. A screen spec answers the write with
`refusal(key, { status, parameters })` from `tests/support/laptop.ts` and reads the text under the
row; `tests/admin/components/items/ItemIngredientsDialog.recipeLines.spec.ts` shows both the warning
and its removal on typing.
