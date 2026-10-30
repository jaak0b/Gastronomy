# Stripe a list

A striped list shows two tints: every row carries a light one and every second row a stronger one, so
no row has the colour of the card or screen behind it and the rows still alternate. One shared
definition paints them, for every admin list (through `BaseListRow`, see `hideable-rows.md`) and for
the article list of the phone's order screen (`phone/components/catalog/ItemGrid.vue`). Use it for
any new striped list; never write a tint into a component.

## The shared pieces

- `frontend/src/shared/theme.ts` holds the strengths as the theme variables `stripe-opacity` and
  `stripe-strong-opacity`, one pair per theme: light 0.05 and 0.09, dark 0.04 and 0.08. Vuetify
  writes them as `--v-stripe-opacity` and `--v-stripe-strong-opacity`. The tint is the theme's
  `on-surface` colour at that opacity, so it is neutral and carries no meaning (frontend rule 6). The
  pairs are chosen so that the light stripe differs from the surface and from the background by a
  contrast ratio of about 1.10 in both themes, and the stronger stripe by about 1.19 in the light
  theme and 1.25 in the dark one, which is about the 0.08 the admin lists used before. A different
  strength is changed here and nowhere else.
- `frontend/src/shared/styles/stripedRows.css`, imported once by `main.ts`, defines `.striped-row` and
  `.striped-row.striped-row-strong`. They set the background colour and nothing else.
- `frontend/src/shared/core/stripedRows.ts`: `stripeClassesOf(position)` returns the two class flags
  for a row. Position counts from 0, so the first row has the light tint only and every row at an odd
  position has the stronger one as well.

## Steps

1. Put `:class="stripeClassesOf(position)"` on the element that is one row, with `position` the row's
   index in its list. Striping restarts with each list. A list grouped under headings numbers its
   rows on through the groups with `numberRowsAcrossGroups` (`admin/core/alternatingRows.ts`) and
   hands the number to `BaseListRow`, which applies the classes.
2. Give the row its own padding and radius in its own scoped style: `BaseListRow` adds the 6px
   radius, the phone's rows have none. The shared stylesheet never sets them.
3. Never select a row by `:nth-child` and never write a `rgba(var(--v-theme-on-surface), ...)` of
   your own for a stripe.

## Specs

- `frontend/tests/shared/core/stripedRows.spec.ts` pins which positions get which classes.
- `frontend/tests/shared/theme.spec.ts` pins, in both themes and over both grounds, that the light
  stripe stays clearly apart from the ground and the stronger stripe from the light one.
- `frontend/tests/admin/components/BaseListRow.spec.ts` and
  `frontend/tests/phone/components/catalog/ItemGrid.spec.ts` read the classes of each row by its
  `data-test`.
- jsdom does not apply a stylesheet, so no spec proves the painted colour. That is checked by eye in
  the running app, in the light and the dark theme.
