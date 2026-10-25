# CLAUDE.md (frontend)

Rules for the Vue web app. The repository root `CLAUDE.md` binds here too; this file adds to it and
never contradicts it.

## Three audiences in one build

- **The server phone app.** Used one-handed, at night, at a loud festival, by a volunteer who has never
  been trained on it. This is the primary surface and every design trade-off favours it.
- **The station screen.** A tablet standing at a production location, read across a counter by
  somebody with both hands busy. Designed for a tablet first and still usable on a phone.
- **The admin configuration UI.** Used on the laptop before the event, in a single column; there is
  no phone layout for it.

## Hard rules

1. **Keep the core framework-agnostic and modular.** Code in any `core/` folder, under `src/shared/`
   or under a surface, must not import Vue or Pinia and must not touch the document. Browser storage
   (`localStorage`) is allowed there, because the draft cart and the device language live in it.
   Order building, price totalling, routing rules, draft cart persistence and submission identity
   are plain TypeScript, so they are testable without mounting anything.

2. **Exhaustive switches over union types.** Any branch on a discriminated union (an order status, a
   delivery mode, a device session) must handle every member explicitly and end in
   `assertNever`. Never
   write an `else`, or a trailing `if`, that assumes whatever is left: it silently absorbs union members
   added later, turning a compile error into a runtime crash or, worse, a wrong but plausible result.
   This binds components exactly as it binds the core.

3. **No silently swallowed errors.** A failed order submission is never allowed to look like a success.
   The server placing the order must be able to tell, at a glance, whether it actually reached the
   laptop. Three states, always distinguishable: not yet sent, sent and accepted by the laptop (the
   answer carries the order number), failed with a stated reason.

4. **There is no offline retry queue, and nothing may grow into one.** What this exists to prevent:
   a waiter building up a pile of orders on the phone that flush themselves when the WiFi returns.
   An order that lives only on a phone is lost when the shift ends and the page is closed, and
   nobody ever knows it existed. Sending is always a deliberate act by a person, so at most one
   order is ever in play, no timer sends anything, and nothing is sent without a human pressing a
   button.

   `localStorage` holds that one order as a **draft cart** so a reload does not lose half-built
   work, and beside it what the phone knows about sending it: whether it has been handed over, how
   many attempts were made, and what the last answer was. That record exists so the phone can tell
   the waiter the truth after a reload, never so it can act on its own. One attempt a human started
   stops waiting after ten seconds and says so; the order stays on screen with a retry the waiter
   presses themselves, which is the opposite of giving up on it.

   **A reply is knowledge and silence is not.** An answer from the laptop, yes or no, tells the
   phone what exists, and the waiter may act on it: a refusal with a reason means no order was
   created, so they fix what it names and send again. Silence means the order may already be at the
   laptop, so it is locked against every change until the waiter learns which it was. Never treat
   the two the same.

   **A reasoned refusal after silence unlocks the order; everything else keeps it locked.** When a
   retry comes back with a 400 or 422 carrying our error envelope, the laptop searched for the
   submission id, found nothing, and refused by one of our own rules, so no order exists and the
   waiter may fix what the refusal names, drop a line that cannot be ordered, and send again. A
   401 takes the signed-out path as before. Every other answer, a 500, a 503, a 429, a lost
   network, or anything without our envelope, leaves the order locked, because none of it proves
   the first attempt did not arrive. The unlocks are an allowlist: the two statuses above with the
   envelope, and nothing else.

   Any implementation that gives the phone a list, a timer that sends, or a rule for sending
   without a person pressing something has rebuilt the queue under another name and must be
   rejected in review. The client-generated submission id that makes retry safe against a lost
   response lives in `src/phone/core/` with tests.

5. **Localization through vue-i18n, German and English complete in the same change.** No string
   literals in templates or components. A key present in one locale only is an incomplete change.

6. **Colour carries meaning, one meaning per colour.** Surfaces, navigation and structure stay
   neutral; saturated colour is reserved for meaning, and at most one filled coloured control appears
   per view. The palette lives in `src/shared/theme.ts` and UI code uses its tokens, never raw hex values:
   - `primary` neutral contrast: every action and the active navigation, the only interactive colour.
   - `together` blue and `individual` orange: the two delivery modes, on card borders, mode words and
     chips.
   - `success` green, `warning` amber and `error` red: status only. A healthy server, an item that is
     sold out, a refusal. Never an action and never a delivery mode. Inside a chart, `warning` marks
     only a point that is itself a warning, such as a run-out time inside the warning window.
     `warning` is dark enough in both themes to read as text, which `tests/shared/theme.spec.ts`
     checks, so a warning sentence uses `warning` itself.
   - `chartData` teal: measured data in a chart, its line, the area under it and the forecast drawn
     from it. Only charts use it, never a control or a status. Reference lines in a chart (an
     available amount, a second forecast, the current moment) stay neutral. A chart that needs a
     second data series adds its own token here first.
   Colour carries meaning together with a word or an icon, or as a summary whose details are one tap
   away in words: a table on the waiter's plan shows what has been handed out in its fill and what
   has been settled in its border, and tapping it leads to the settle view that says the same in
   words. A new control takes a token from this list; it never invents a hue and never borrows a
   status colour for an action.

7. **No aria-labels.** `aria-label` and `aria-labelledby` are forbidden in every template and
   component, and no locale key exists only to feed one. Every control says what it does through its
   visible text or its icon, and a test selects an element by its `data-test` attribute or its
   text, never by an accessibility attribute. A review lists any aria attribute as a finding.

8. **New features behave like the existing ones.** When a screen needs something the app already
   does elsewhere (hiding and showing, removing, a confirmation, a list row with a name field, a
   checkbox that reveals hidden entries), it looks, reads and behaves the same as that place and
   uses the same component or composable. Example: when the laptop would refuse to hide or remove
   something, the button is disabled with a tooltip naming the reason, as
   `how-to/references/admin-list-screen.md` describes, never a warning after the click. Where that
   place's code is not shared yet, it moves into a shared home both use. The brief names, for every
   such control, the place it follows and the shared piece to use or create.

## Design constraints from the deployment

- **Touch targets are large.** One thumb, in the dark, possibly with gloves.
- **The order total is prominent where it is shown**, because its only job is helping a human add
  up cash correctly.
- **There are no zones.** An item that has exactly one candidate production location routes
  automatically, and no location control renders for it. That is the normal case and it must be
  completely invisible. Only an item with more than one candidate location asks the server to choose,
  and that choice belongs to the order line, not to a session, a device or a shift. Never introduce a
  remembered location preference or a shift-start selection screen.
- **A device tab opens through the start screen, and the back-button trap stays behind it.** Chromium
  and WebKit skip history entries created by a page that navigated the user on without any
  interaction, so no automatic back trap survives on its own. The start screen (`DoorGate.vue`) needs
  one tap: that activation makes the screen the unskippable anchor. Only then is the chain of real
  `door.html` pages built, and every back press skips the chain and lands on the anchor, which
  rebuilds it and returns the device to the app. The trap is its own module,
  `src/shared/router/backButtonTrap.ts`, beside the router and never importing it. Never build the
  chain at load, never remove the start screen, and never let a tab that sits behind the trap grow
  its history, which is what `navigate` doing `replaceState` there preserves. The address of the
  chain carries the page's last-modified stamp so a cached door from an older build can never be
  used.

## Testing

Governed by its own skill in `.claude/skills/`:

- **Unit tests (Vitest, `tests/`)** are the internal correctness net. Core logic first: totals, routing,
  draft cart persistence, submission identity across retries, state transitions. See
  `writing-unittests`. A test finds what it acts on by the name our code gave it: a page element by
  the `data-test` attribute its template carries, a shape drawn on a Konva canvas by the `id` or
  `name` our code set on it. Never by its position among its siblings (`[0]`, `.at(-1)`) or by a
  styling class. What our code never names, such as a Vuetify field's message line, is found by the
  class its library gives it. Konva drawings are tested on the `canvas` package, which jsdom uses
  for real drawing, so a test can tap a shape at a point.

## Commands

```bash
npm install
npm run dev        # Vite dev server, proxying the API to the backend
npm run generate:api  # regenerate the wire shapes from the backend's OpenAPI document
npm run build      # generate:api, then vue-tsc typecheck plus production build
npx vitest run <spec path>  # Vitest
```

The frontend has no formatter. Never run prettier or any other tool that rewrites files: it reformats
whole files nobody meant to touch. Edit by hand in the style of the surrounding code.
