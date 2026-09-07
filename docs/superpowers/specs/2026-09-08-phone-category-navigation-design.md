# The waiter picks a category first

Date: 2026-09-08

## Why

The phone shows every category as a tab across the top and the items of one category below it. A tab
is a small target, the tabs scroll sideways when there are more than a few, and a waiter holding the
phone in one hand at a loud festival at night hits the wrong one. The categories became real objects
with a colour and an order in the change of 2026-09-07, and this change spends that: a category
becomes a full width coloured button, and the items sit one level below it.

The decision behind it, in the owner's words: three taps that are easy to hit beat two taps where a
mistap is likely.

This change touches the phone's ordering screen only. The summary screen, the open items screen, the
station page and the admin pages are untouched, and the backend is untouched.

## What the waiter sees

### Level one, the ordering screen

- The categories, one full width button per row, in the order the admin set, each painted in its own
  colour with the lettering colour derived from that colour by `frontend/src/core/letteringColour.ts`,
  the same derivation the admin's headings already use.
- A button whose category already contributes to the order carries the count: "5 x Getränke". A
  category contributing nothing carries its name alone. The count comes from
  `portionsOfCategory` in `frontend/src/core/categoryPortions.ts`, which already produces exactly this
  number for the tabs.
- Below the buttons, the table name field and the note field for the kitchen, unchanged and in their
  present order.
- At the bottom, the basket bar with the running total and the button to the summary, unchanged.

### Level two, one category's items

- The category's name at the top, written on the category's colour, so the waiter can see where they
  are without reading the item names.
- The items of that category, rendered by the existing `ItemGrid` and its rows, with the same
  controls, the same estimates, the same notes and the same station choice. This code is reused, not
  rewritten, and its behaviour does not change.
- At the bottom, one full width button back to the categories. No basket bar, no summary button and
  no total on this level.

## Navigation and the back gesture

Only one level is on screen at a time. The address does not change: no route is added and
`frontend/src/core/route.ts` keeps the routes it has.

The phone's own back gesture must return the waiter from level two to level one in exactly one press,
and on level one it must still not leave the app. The router already owns this concern:
`frontend/src/router.ts` pushes a repeat of the current history entry so that a back press on a server
screen lands inside the app rather than outside it. The category step extends that mechanism rather
than calling `window.history` from the view, per hard rule 6.

The router gains a way for a screen to say that it has opened a step inside itself, and to be told
when a back press should close that step instead of changing the route. The on screen back button
closes the same step through the same path, so both ways back run the same code.

A reload lands the waiter on level one with the order intact, because the draft cart already survives
a reload. Nothing about the draft cart changes here, and nothing is stored about which category was
open.

## Edge cases the code answers

- **A category with nothing orderable** cannot be reached: the laptop already leaves such a category
  out of the catalog it sends.
- **The admin switches off the category the waiter is standing in.** The phone refetches the catalog
  over the hub, the category disappears, and the waiter is dropped back to level one rather than left
  on a list that no longer exists. Anything already in their order stays in it.
- **The laptop holds no categories at all.** Level one shows nothing but the table name field, the
  note field and the basket bar. This is the state before the admin has entered a menu, and the admin
  overview is where that is said, not here.

## No backend change

`GET /api/catalog` already returns the categories with `categoryId`, `name`, `colourHex` and
`sortOrder`, and the items with their `categoryId`. The frontend types in
`frontend/src/core/apiTypes.ts` already carry all of it. Nothing on the server moves for this change.

## Localization

Both languages in the same change, per hard rule 8.

| Key | German | English |
|---|---|---|
| `catalog.categoryWithCount` | {count} x {name} | {count} x {name} |
| `catalog.backToCategories` | Zurück zu den Kategorien | Back to the categories |

## What is deleted

Per hard rule 17, in this same change, with a repository search quoted as the evidence.

- `frontend/src/components/catalog/CategoryTabs.vue` and every test that covered the tabs.
- The whole Playwright suite, which the owner decided to drop because it skips itself unless two
  environment variables are set, one of them a single use enrolment code, so nobody runs it and a
  suite that skips reports green without proving anything:
  `frontend/e2e/` including `order-placement.spec.ts`, `order-placement.flow.md` and
  `playwright.config.ts`; the `test:e2e` script and the `@playwright/test` dependency in
  `frontend/package.json` and the lock file; the web tests paragraph and the `npm run test:e2e` line
  in `frontend/CLAUDE.md`; and the sentence in root rule 4 that requires end to end coverage of the
  order placement flow and the station production flow, because the rule cannot outlive the only tool
  that satisfied it.

The `writing-webtests` skill in `frontend/.claude/skills/` stays where it is. The owner keeps it for
a later web test suite, so it is not a trace of the deleted one.

The walkthrough in `docs/manual-verification.md` remains what actually proves the flow, and it gains
the extra tap this change introduces.

## Testing

Unit, in `frontend/tests/`:

- The count shown on a category button, including a category contributing nothing.
- Closing the category step on a back press, and that a back press on level one does not leave the
  app.
- Dropping back to level one when the open category disappears from the catalog.

Component:

- Level one draws one button per category, in the server's order, in the category's colour, and draws
  no items.
- Tapping a button shows that category's items and the back button, and hides the basket bar.
- The back button returns to level one with the order intact.

Documentation, in the same change: `docs/spec.md` where it describes the ordering screen, and
`docs/manual-verification.md` where the walkthrough taps through the menu.
