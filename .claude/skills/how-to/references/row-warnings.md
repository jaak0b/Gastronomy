# Show a warning under a row

A refused action on one row of a list shows its sentence under that row and under no other. A refused
action on a whole screen or dialog shows one sentence in one place. Both read the refusal from the
answer of the laptop and turn it into text the same way.

## One warning per row: `useRowRefusals`

`frontend/src/admin/composables/useRowRefusals.ts`. Call it once in the component or composable that
owns the rows; it keeps one refusal per row id.

- `refusalTextOf(rowId)` is the sentence to render, or `null`. Render it under the row in a
  `v-alert` with `type="warning"`, `variant="tonal"` and `data-test="refusal"`.
- `showRefusalOf(rowId, result)` takes the `AdminActionResult` a store action returned and stores the
  refusal when the action failed. It does nothing for a success, so an old warning survives an
  accepted action unless it was forgotten first.
- `refuse(rowId, messageKey, parameters?)` stores a refusal the screen found itself, before any
  request, such as an amount that is not a number.
- `forgetRefusalOf(rowId)` removes the warning. Call it before every new action on the row, and as
  soon as the person edits the row again (typing in its field), never only when a save succeeds.
- `isRefused(rowId)` says whether the row carries a warning.

Current consumers: `admin/composables/useFestivalItemRows.ts` (rows keyed by item id),
`admin/components/items/ItemIngredientsDialog.vue` (recipe lines keyed by ingredient id),
`admin/components/festivals/FestivalStock.vue` (stock rows keyed by ingredient id),
`admin/components/HideableEntryList.vue` (the table plan's areas and tables, keyed by their id; see
`hideable-rows.md`) and `admin/components/stations/RemovableStationRows.vue` (the station rows of the
festival page and of the table plan editor, keyed by station id).

Keep the rows' state in a component that stays mounted while the person moves between rows. A
refusal kept by a component that is keyed on the selection is lost unseen when the person selects
another row before the laptop answers.

## One warning per screen: `useRefusalDisplay`

`admin/composables/useRefusalDisplay.ts` is a single slot: `refusal`, `refusalText` and
`showRefusalOf(result)`, which returns `true` when the result was a refusal and clears the slot when
it was not. Clear it by setting `refusal.value = null`. `useEditSession` owns one for its dialog, and
`ItemIngredientsDialog.vue` uses one more for the add row beside its per row warnings. Use the row
version only where several rows can carry a warning at once.

## One mapping to text: `adminErrorMessageText`

`admin/core/adminErrorMessage.ts` is the only place a refusal becomes a sentence. A store action
returns `{ kind: 'failed', message }` (`admin/core/adminActionResult.ts`), where `message` was built
by `adminErrorMessage(body)` from the laptop's `ApiErrorBody`: a missing body, or a `messageKey` the
German locale file does not hold, becomes `errors.admin.actionFailed`. `adminErrorMessageText(t,
message)` translates it with its parameters, and also passes the count whenever the message has one:
`adminErrorMessage` takes it from a `count` parameter that is a whole number (or a string holding
one) and leaves it `null` otherwise. A component never calls `t(message.key)` on its own.

## Specs

`frontend/tests/admin/composables/useRowRefusals.spec.ts`, `useRefusalDisplay.spec.ts` and
`tests/admin/core/adminErrorMessage.spec.ts` cover the pieces. A screen spec answers the write with
`refusal(key, { status, parameters })` from `tests/support/laptop.ts` and reads the text under the
row; `tests/admin/components/items/ItemIngredientsDialog.recipeLines.spec.ts` shows both the warning
and its removal on typing.
