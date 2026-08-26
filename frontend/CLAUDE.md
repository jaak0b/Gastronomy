# CLAUDE.md (frontend)

Rules for the Vue web app. The repository root `CLAUDE.md` binds here too; this file adds to it and
never contradicts it.

## The stack

Vue 3 + TypeScript + Vite + Pinia. Built into the backend's `wwwroot` and shipped inside the backend
executable. There is no separate deployment and no CDN: the app is served by the laptop on site.

Two audiences in one build:

- **The server phone app.** Used one-handed, at night, at a loud festival, by a volunteer who has never
  been trained on it. This is the primary surface and every design trade-off favours it.
- **The admin configuration UI.** Used on the laptop before the event: items and prices, production
  locations and their zones, item-to-location assignment, station and printer setup, device enrolment
  and revocation.

## Hard rules

1. **Keep the core framework-agnostic and modular.** Code in `src/core/` must not import Vue or Pinia
   and must not touch the DOM. Order building, price totalling, routing rules, and the offline retry
   queue are plain TypeScript, so they are testable without mounting anything.

2. **Exhaustive switches over union types.** Any branch on a discriminated union (an order state, a
   print outcome, a transport kind) must handle every member explicitly and end in `assertNever`. Never
   write an `else`, or a trailing `if`, that assumes whatever is left: it silently absorbs union members
   added later, turning a compile error into a runtime crash or, worse, a wrong but plausible result.
   This binds components exactly as it binds the core.

3. **No silently swallowed errors.** A failed order submission is never allowed to look like a success.
   The server placing the order must be able to tell, at a glance, whether it actually reached the
   kitchen. Three states, always distinguishable: not yet sent, sent and confirmed printed, failed with
   a stated reason.

4. **The offline queue is core logic, not a component concern.** An order queues in `localStorage` and
   retries through short WiFi dropouts. Its rules (what queues, how long, when it gives up, how it
   avoids double submission on reconnect) live in `src/core/` with tests, never inside a component.
   Remember that phones are online-only: there is no service worker, so the queue survives a dropout
   but not a closed tab. The UI must say so where it matters.

5. **Localization through vue-i18n, German and English complete in the same change.** No string
   literals in templates or components. A key present in one locale only is an incomplete change.

6. **UI text follows the `writing-ui-guidance` skill.** Read it before writing or changing any
   user-facing guidance, hint, warning, step instruction, or status message.

7. **No code comments**, per the root rules. Rename until the comment is redundant.

## Design constraints from the deployment

- **Touch targets are large.** One thumb, in the dark, possibly with gloves.
- **The order total is prominent**, because its only job is helping a human add up cash correctly.
- **Zone is chosen once at shift start** and shown persistently in the header, with a per-item override
  available but never required. Routing is otherwise automatic: an item's candidate locations filtered
  by the server's zone. Do not make the server pick a location per item in the normal flow.
- **Enrolment is a QR scan.** The QR carries the full URL including the laptop's current IP, so nothing
  in the app may depend on a remembered address. A 6-digit code is the fallback for a broken camera.
- **No feature may require a service worker**, a secure context, or an installed PWA.

## Testing

Two tiers, governed by their own skills in `.claude/skills/`:

- **Unit tests (Vitest, `tests/`)** are the internal correctness net. Core logic first: totals, routing,
  the retry queue, state transitions. See `writing-unittests`.
- **Web tests (Playwright, `e2e/`)** are owner-facing assurance that approved behaviour does not drift.
  Required for the order placement flow. See `writing-webtests`.

## Commands

```bash
npm install
npm run dev        # Vite dev server, proxying the API to the backend
npm run build      # vue-tsc typecheck plus production build
npm test           # Vitest
npm run test:e2e   # Playwright
```
