# CLAUDE.md (frontend)

Rules for the Vue web app. The repository root `CLAUDE.md` binds here too; this file adds to it and
never contradicts it.

## The stack

Vue 3 + TypeScript + Vite + Pinia. Built into the backend's `wwwroot` and shipped inside the backend
executable. There is no separate deployment and no CDN: the app is served by the laptop on site.

Three audiences in one build:

- **The server phone app.** Used one-handed, at night, at a loud festival, by a volunteer who has never
  been trained on it. This is the primary surface and every design trade-off favours it.
- **The station screen.** A tablet standing at a production location, read across a counter by
  somebody with both hands busy. Designed for a tablet first and still usable on a phone.
- **The admin configuration UI.** Used on the laptop before the event: items and prices, production
  locations, item-to-location assignment, station setup, device enrolment and revocation.

## Hard rules

1. **Keep the core framework-agnostic and modular.** Code in `src/core/` must not import Vue or Pinia
   and must not touch the DOM. Order building, price totalling, routing rules, draft cart persistence
   and submission identity are plain TypeScript, so they are testable without mounting anything.

2. **Exhaustive switches over union types.** Any branch on a discriminated union (an order state, a
   production status, a delivery mode, a device kind) must handle every member explicitly and end in
   `assertNever`. Never
   write an `else`, or a trailing `if`, that assumes whatever is left: it silently absorbs union members
   added later, turning a compile error into a runtime crash or, worse, a wrong but plausible result.
   This binds components exactly as it binds the core.

3. **No silently swallowed errors.** A failed order submission is never allowed to look like a success.
   The server placing the order must be able to tell, at a glance, whether it actually reached the
   laptop. Three states, always distinguishable: not yet sent, sent and accepted by the laptop (the
   answer carries the order number), failed with a stated reason.

4. **There is no offline retry queue, and nothing may grow into one.** A failed submission leaves the
   order on screen exactly as it was and offers a retry the server taps themselves. No timers, no
   background resubmission, no give-up window, no queue data structure. `localStorage` holds the
   in-progress order as a **draft cart** so a reload does not lose half-built work: a draft is one
   order, with no list, no timer and no state field. Any implementation that gives it those has
   rebuilt the queue under another name and must be rejected in review. The client-generated
   submission id that makes retry safe against a lost response lives in `src/core/` with tests.

5. **Localization through vue-i18n, German and English complete in the same change.** No string
   literals in templates or components. A key present in one locale only is an incomplete change.

6. **UI text follows the `writing-ui-guidance` skill.** Read it before writing or changing any
   user-facing guidance, hint, warning, step instruction, or status message.

7. **No code comments**, per the root rules. Rename until the comment is redundant.

## Design constraints from the deployment

- **Touch targets are large.** One thumb, in the dark, possibly with gloves.
- **The order total is prominent where it is shown**, because its only job is helping a human add
  up cash correctly. The screen listing one category's items leaves it out so the items own the
  screen; the categories screen and the summary both carry it.
- **There are no zones.** An item that has exactly one candidate production location routes
  automatically, and no location control renders for it. That is the normal case and it must be
  completely invisible. Only an item with more than one candidate location asks the server to choose,
  and that choice belongs to the order line, not to a session, a device or a shift. Never introduce a
  remembered location preference or a shift-start selection screen.
- **Enrolment is a QR scan.** The QR carries the full URL including the laptop's current IP, so nothing
  in the app may depend on a remembered address. A 6-digit code is the fallback for a broken camera.
  The same QR flow enrols a waiter's phone and a station's tablet; the invitation says which of the
  two it belongs to, and the device stores that kind beside its token.
- **The device kind decides the screen.** A station tablet lands on the station screen and stays
  there; a waiter's phone keeps the ordering flow. That decision lives in `src/core/landing.ts`, not
  in a component.
- **A delivery mode is chosen once, before sending, and never afterwards.** The server picks, per
  station, whether that station hands its part of the order out together or as each item is ready.
  No screen may offer to change it after the order is sent.
- **No feature may require a service worker**, a secure context, or an installed PWA.

## Testing

Governed by its own skill in `.claude/skills/`:

- **Unit tests (Vitest, `tests/`)** are the internal correctness net. Core logic first: totals, routing,
  draft cart persistence, submission identity across retries, state transitions. See
  `writing-unittests`.

## Commands

```bash
npm install
npm run dev        # Vite dev server, proxying the API to the backend
npm run build      # vue-tsc typecheck plus production build
npm test           # Vitest
```
