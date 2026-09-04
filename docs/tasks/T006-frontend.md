# T006: Frontend, the server-phone slice and its supporting admin pages

## 1. Objective

Build the Vue frontend for the one slice the product exists to deliver: a server enrols a phone,
builds an order, sends it, and can see whether it reached the kitchen. Build the admin pages that
slice needs to exist at all (items, locations, assignments, printers including the mock fault
control, invitations and the people list, starting an event session), plus the break-glass station
page and the always-visible header. Everything is test-first: `src/core/` modules get Vitest unit
tests before their implementation exists, and the one required order-placement flow gets a
Playwright spec.

Definition of done: `npm run build` in `frontend/` succeeds and writes into
`backend/GastronomyApp.Api/wwwroot`, and `npm test -- --run` in `frontend/` is green. The Playwright
spec is written and wired into `npm run test:e2e`, but is skipped by default (see section 6) because
it needs the real backend from T002 through T005, which this task does not build.

This document assumes REST endpoints and SignalR events exactly as `docs/spec.md` sections 5, 6, and
9 define them and does not repeat their reasoning; section 3 of this document restates the exact
shapes so a consistency reviewer can check this task against the four sibling backend tasks without
opening the spec.

## 2. Assumed from earlier tasks

The backend tasks (T002 to T005) are not written yet. This task assumes they implement the following
exactly as `docs/spec.md` describes them. Anything not listed here is out of scope for the frontend
and is read straight from the spec at implementation time rather than restated.

### 2.1 REST endpoints consumed by the server phone app

| Method | Path | Body | Response |
|---|---|---|---|
| POST | `/api/enrolment/redeem` | `{code, sixDigitCode, name, userAgent}`, exactly one of `code`/`sixDigitCode` | 200 `{deviceId, deviceToken, serverPerson:{id,name}, language}`; 400/404/410/422/429 with `{code, messageKey, parameters, details}` |
| GET | `/api/session` | (device auth) | 200 `{deviceId, serverPerson:{id,name}, eventSession:{id,name,isPractice}, language}`; 401 |
| PUT | `/api/session/language` | `{language: "de"\|"en"}` | 204 |
| GET | `/api/catalog` | (device auth) | 200 `{version, categories:[{name,sortOrder}], items:[{id,name,categoryName,priceCents,sortOrder,isAvailable,locationIds[]}], locations:[{id,name,sortOrder}], tableSuggestions:[{label,sortOrder}]}` |
| POST | `/api/orders` | `{clientOrderId, tableLabel, note, expectedTotalCents, lines:[{catalogItemId,quantity,note,productionLocationId}]}` | 201 or 200 `{orderId, globalOrderNumber, status, totalCents, expectedTotalCents, createdAtUtc, tickets:[{ticketId,locationId,locationName,sequenceNumber,status,lineIds[]}]}`; 400/401/409/422/503 |
| GET | `/api/printers/status` | | 200 `{locations:[{locationId,name,isOnline,isPaperEnd,isPaperNearEnd,isCoverOpen,isFaulty,lastChangedAtUtc}]}` |
| GET | `/api/health` | anonymous | 200 `{status, eventSession, printersOnline, printersTotal}` |

Order status values: `Accepted`, `NeedsAttention` (exact enum members confirmed against the
consistency review). Ticket status values: `Queued`, `Blocked`, `Printing`, `Unknown`, `Failed`, `Printed`,
`PrintedOnTestPrinter`, `HandledOnPaper`, per spec section 3.2.

Every error response, on every endpoint above, has the shape `{code, messageKey, parameters,
details}`. `details` is never sent to a device caller (spec 5.1); the frontend only ever reads
`messageKey` and `parameters`, never `code` or `details`, for rendering.

### 2.2 REST endpoints consumed by the admin pages

All under `/api/admin/...`, reachable only from the laptop (loopback or the laptop's own bound
address; a phone gets 404). This task consumes:

| Method | Path | Body | Response |
|---|---|---|---|
| GET/POST/PUT | `/api/admin/locations`, `/api/admin/locations/{id}` | `{name,sortOrder,slipLanguage}` | list / 201 / 200 |
| POST | `/api/admin/locations/{id}/deactivate` | | 200 or 409 naming open tickets or orphaned items |
| POST | `/api/admin/locations/{id}/regenerate-access-key` | | 200 with the new break-glass URL |
| GET | `/api/admin/locations/{id}/station-card` | | 200, printable card |
| GET/POST/PUT | `/api/admin/items`, `/api/admin/items/{id}` | `{name,categoryName,priceCents,sortOrder,locationIds[]}` | list / 201 / 200; 422 when `locationIds` empty |
| POST | `/api/admin/items/{id}/availability` | `{isAvailable}` | 200, the sold-out toggle, never refused |
| POST | `/api/admin/items/{id}/deactivate` | | 200 or 409 during a live session |
| GET | `/api/admin/server-people` | | 200, one row per person with phone state, last seen, user agent, outstanding invitation |
| PUT | `/api/admin/server-people/{id}` | `{name}` | 200, the rename |
| POST | `/api/admin/server-people/{id}/revoke-device` | | 200 or 409 (no phone set up) |
| POST | `/api/admin/server-people/{id}/deactivate` | | 200 |
| POST | `/api/admin/enrolment/invitations` | `{}` or `{serverPersonId}` | 201 `{invitationId, qrUrl, sixDigitCode, expiresAtUtc, serverPerson}` |
| GET/PUT | `/api/admin/printers`, `/api/admin/printers/{locationId}` | full printer configuration | 200 |
| POST | `/api/admin/printers/{locationId}/test-print` | | 202 |
| POST | `/api/admin/printers/{locationId}/reconnect` | | 202, names locations sharing that endpoint |
| POST | `/api/admin/mock/{locationId}/fault` | `{fault, mode}` | 200; `fault` one of `None, PaperEnd, CoverOpen, ConnectTimeout, DropSocketEarly, DropSocketMidJob, UnknownOutcome`; `mode` one of `Once, Sticky`; 422 when transport is not `Mock` |
| GET | `/api/admin/event-session` | | 200 current session plus what blocks starting a new one |
| POST | `/api/admin/event-session` | `{name, isPractice, confirmedName}` | 201; 409 with blocking conditions |

Out of scope for this task's screens, read but not built here: `/api/admin/orders`, the print-history
endpoint, CSV export/import, backup, diagnostics, log, `POST /api/admin/printers/discover` and its
`PrinterDiscovered` event, and `GET/PUT /api/admin/table-suggestions` plus
`.../from-last-session`. The consistency review found none of these five has a backend in this vertical
slice (T005 excludes discovery and table-suggestions management from its scope); the printer discovery
button and the table-suggestions admin screen are dropped from this task accordingly (see section 6 and
section 7). The table suggestion chips on the review screen survive unaffected, because they read
`tableSuggestions[]` off `GET /api/catalog`, which is in scope regardless.

### 2.3 Station break-glass endpoints

| Method | Path | Response |
|---|---|---|
| GET | `/station/{accessKey}` | the SPA shell in station mode |
| GET | `/api/station/{accessKey}/locations` | 200, active production locations plus each one's print-capability, the filter's contents |
| GET | `/api/station/{accessKey}/tickets?locationId=` | 200, open tickets oldest first, each carrying `canAcknowledge`, `reprintCount`, and, when `canAcknowledge` is false, the reason key |
| POST | `/api/station/{accessKey}/tickets/{ticketId}/acknowledge` | 200 moves to `HandledOnPaper`; 409 with `station.takeRefused` or `station.alreadyTaken` |
| GET | `/api/station/{accessKey}/status` | 200, the selected location's printer status |

An unknown or regenerated key answers 404 on every one of these.

### 2.4 SignalR

One hub at `/hub`. Device auth as an `access_token` query parameter; the station page connects with
its access key. Events this frontend consumes:

| Event | Payload | Frontend reaction |
|---|---|---|
| `OrderAccepted` | `{orderId, globalOrderNumber, tableLabel, totalCents, tickets[]}` | station page: refetch tickets |
| `TicketStatusChanged` | `{orderId, globalOrderNumber, ticketId, locationId, locationName, sequenceNumber, status, failureReason, printerHasPaper, messageKey, parameters}` | station page: refetch tickets |
| `PrinterStatusChanged` | `{locationId, locationName, isOnline, isPaperEnd, isPaperNearEnd, isCoverOpen, isFaulty, waitingTicketCount, lastDetail}` | phone: header banner; station page: re-evaluate `canAcknowledge`-gated rows; admin printer screen: update indicator |
| `CatalogChanged` | `{version}` | phone refetches `/api/catalog` |
| `EnrolmentCompleted` | `{serverPersonId, serverPersonName, deviceId}` | admin people list gains a row / QR panel closes |
| `DeviceRevoked` | `{deviceId}` | that phone clears its token, keeps its draft, shows enrolment |
| `EventSessionStarted` | `{eventSessionId, name, isPractice}` | phones show the one line notice, draft untouched |

The station page never renders a payload's ticket fields directly; every one of the two events it
receives triggers a refetch of `GET /api/station/{accessKey}/tickets`, because neither event carries
`canAcknowledge` and only the server computes it (spec 6.2, 5.6).

`PrinterDiscovered` and `POST /api/admin/printers/discover` are dropped from this task's scope, for the
same reason as section 2.2's note: nothing in this vertical slice implements printer discovery on the
backend. `PrintersList.vue` in section 3.1 offers manual address entry only; the discovery button and
its result list are not built here.

Reconnect behaviour assumed from spec 6.3: automatic reconnect intervals `0, 2, 5, 10, 30`, then every
30 seconds; on every reconnect the client refetches rather than assuming it missed nothing; if the hub
never connects, the phone polls `GET /api/printers/status` every 15 seconds instead, written once in
the store.

### 2.5 Draft cart and submission identity rules assumed from spec section 9

Restated here because rule 4 of `frontend/CLAUDE.md` says an implementation that reintroduces a queue
under a different name must be rejected in review, and a coder working from this document alone must
not be able to miss it:

- The draft cart holds **one order**, the one currently on screen. No list, no timer, no `state`
  field, no retry loop, no ordering, no head, no ages.
- It lives in `localStorage` under the key `draftOrder` and is written on every change (a line added,
  a quantity changed, a station chosen, a note typed, the table entered).
- It is cleared **only** when the order is accepted (a 200 or 201 from `POST /api/orders`). It is
  never cleared by a failure, a reload, a revocation, or a re-enrolment.
- `clientOrderId` is generated once, the moment the server first taps send, written into the draft
  before the request starts, and reused for every retry of that same order. Nothing regenerates it:
  not a retry, not a reload, not a re-enrolment, not a new event session. A new id is created only
  when the next order is started (after the current one is accepted and the draft is cleared).
- A failed submission leaves everything on screen exactly as it was, with the retry button under the
  message. Nothing is sent automatically. There is no background resubmission and no give-up window
  for the submission itself.
- A 200 response (the retried duplicate) is rendered exactly like a 201: same confirmation, same
  order number, no wording that distinguishes the two cases.

## 3. Architecture

Per `frontend/CLAUDE.md` rule 1: `src/core/` is plain TypeScript, no Vue import, no DOM. Pinia stores
own SignalR consumption, the reconnect-refetch rule, and the 15 second polling fallback, written once.
Components stay thin: they read a store and call a store action, and hold no business rule of their
own. Every discriminated-union switch (order status, ticket status, print outcome kind, catalog item
state) ends in `assertNever`, per `frontend/CLAUDE.md` rule 2.

### 3.1 File layout

```
frontend/
  src/
    core/
      draftCart.ts
      submission.ts
      basket.ts
      routingPreview.ts
      totals.ts
      catalogItemState.ts
      tableLabel.ts
      apiTypes.ts
      apiError.ts
    stores/
      session.ts
      catalog.ts
      order.ts
      printerStatus.ts
      connection.ts
      admin/
        locations.ts
        items.ts
        printers.ts
        people.ts
        eventSession.ts
      station.ts
    components/
      enrolment/
        QrLanding.vue
        SixDigitFallback.vue
        NameField.vue
      header/
        AppHeader.vue
        SettingsSheet.vue
      catalog/
        CategoryStrip.vue
        ItemGrid.vue
        ItemButton.vue
        LineStationSheet.vue
        BasketBar.vue
      review/
        LineList.vue
        TableField.vue
        TotalDisplay.vue
        SendFailurePanel.vue
      station/
        StationWarning.vue
        StationFilter.vue
        StationTicketRow.vue
        StationLanguageSwitch.vue
      admin/
        overview/AdminOverview.vue
        locations/LocationsList.vue
        locations/LocationForm.vue
        items/ItemsList.vue
        items/ItemForm.vue
        items/AssignmentEditor.vue
        printers/PrintersList.vue
        printers/PrinterForm.vue
        printers/MockFaultPanel.vue
        people/PeopleList.vue
        people/InvitationPanel.vue
        event/EventSessionPanel.vue
        NotOnLaptop.vue
    views/
      EnrolQr.vue
      EnrolCode.vue
      Catalog.vue
      Review.vue
      StationPage.vue
      admin/AdminShell.vue
      (one view per admin list above, routed under /admin/...)
    router/
      index.ts
    locales/
      de.json
      en.json
    App.vue
    main.ts
  tests/
    core/
      draftCart.spec.ts
      submission.spec.ts
      basket.spec.ts
      routingPreview.spec.ts
      totals.spec.ts
      catalogItemState.spec.ts
      tableLabel.spec.ts
      i18nCoverage.spec.ts
    stores/
      connection.spec.ts
    components/
      review/SendFailurePanel.spec.ts
      catalog/ItemButton.spec.ts
      station/StationTicketRow.spec.ts
  e2e/
    playwright.config.ts                    (T001's file, unchanged path)
    order-placement/
      order-placement.spec.ts
      order-placement.flow.md
```

### 3.2 `src/core` module list with exported signatures

`apiTypes.ts` has no behaviour; it is the shared type module every other core file and every store
imports from, so the wire shapes in section 2 exist exactly once.

```typescript
export interface CatalogItem {
  id: string
  name: string
  categoryName: string
  priceCents: number
  sortOrder: number
  isAvailable: boolean
  locationIds: string[]
}
export interface Catalog {
  version: string
  categories: { name: string; sortOrder: number }[]
  items: CatalogItem[]
  locations: { id: string; name: string; sortOrder: number }[]
  tableSuggestions: { label: string; sortOrder: number }[]
}
export interface DraftLine {
  catalogItemId: string
  quantity: number
  note: string | null
  productionLocationId: string | null
}
export interface DraftOrder {
  tableLabel: string
  note: string | null
  lines: DraftLine[]
  clientOrderId: string | null
}
export type OrderStatus = 'Accepted' | 'Printing' | 'Printed' | 'NeedsAttention'
export type TicketStatus =
  | 'Queued' | 'Blocked' | 'Printing' | 'Unknown' | 'Failed'
  | 'Printed' | 'PrintedOnTestPrinter' | 'HandledOnPaper'
export type PrintFailureReason =
  | 'PaperEnd' | 'CoverOpen' | 'Unreachable' | 'Timeout' | 'SocketDropped'
  | 'PrinterError' | 'StationDisabled' | 'StationFaulty' | 'TicketResolvedByHuman'
export interface OrderSubmitRequest {
  clientOrderId: string
  tableLabel: string
  note: string | null
  expectedTotalCents: number
  lines: DraftLine[]
}
export interface OrderSubmitResponse {
  orderId: string
  globalOrderNumber: number
  status: OrderStatus
  totalCents: number
  expectedTotalCents: number
  createdAtUtc: string
  tickets: {
    ticketId: string
    locationId: string
    locationName: string
    sequenceNumber: number
    status: TicketStatus
    lineIds: string[]
  }[]
}
```

`apiError.ts`

```typescript
export interface ApiErrorBody {
  code: string
  messageKey: string
  parameters: Record<string, string | number>
  details: string | null
}
export function isApiErrorBody(value: unknown): value is ApiErrorBody
```

`draftCart.ts`, no Vue, reads and writes `localStorage` directly (the one core module allowed to
touch `localStorage`, because the draft's persistence rule belongs with the draft's shape, not with a
store):

```typescript
export const DRAFT_STORAGE_KEY = 'draftOrder'
export function emptyDraft(): DraftOrder
export function loadDraft(): DraftOrder
export function saveDraft(draft: DraftOrder): void
export function clearDraft(): void
export function addLine(draft: DraftOrder, line: DraftLine): DraftOrder
export function setLineQuantity(draft: DraftOrder, index: number, quantity: number): DraftOrder
export function removeLine(draft: DraftOrder, index: number): DraftOrder
export function setLineNote(draft: DraftOrder, index: number, note: string | null): DraftOrder
export function setLineStation(draft: DraftOrder, index: number, locationId: string | null): DraftOrder
export function setTableLabel(draft: DraftOrder, tableLabel: string): DraftOrder
export function setOrderNote(draft: DraftOrder, note: string | null): DraftOrder
```

Every mutator returns a new `DraftOrder` and calls `saveDraft` itself, so a component or store never
forgets to persist a change. None of these functions accept or produce anything resembling a list of
orders or a `state` field beyond the shape in section 2.5.

`submission.ts`:

```typescript
export function ensureClientOrderId(draft: DraftOrder): DraftOrder
export function buildSubmitRequest(draft: DraftOrder, expectedTotalCents: number): OrderSubmitRequest
```

`ensureClientOrderId` returns the draft unchanged if `clientOrderId` is already set, and otherwise
returns a copy with a freshly generated UUID (via `crypto.randomUUID()`) written in and persisted
through `saveDraft`. It is the only place a `clientOrderId` is ever created; nothing else in the
codebase may call `crypto.randomUUID()` for this purpose.

`basket.ts`:

```typescript
export interface BasketLineView {
  catalogItemId: string
  name: string
  unitPriceCents: number
  quantity: number
  note: string | null
  productionLocationId: string | null
  candidateLocationIds: string[]
  isSoldOut: boolean
}
export function buildBasketView(draft: DraftOrder, catalog: Catalog): BasketLineView[]
export function basketItemCount(draft: DraftOrder): number
```

`routingPreview.ts`, shared logic with the backend's `OrderRoutingResolver` fixture set per spec
11.1, kept in its own module so the "never asks with one candidate" rule has one true home:

```typescript
export function candidateLocations(item: CatalogItem): string[]
export function needsStationChoice(item: CatalogItem): boolean
```

`totals.ts`:

```typescript
export function lineTotalCents(line: BasketLineView): number
export function orderTotalCents(lines: BasketLineView[]): number
export function formatPrice(cents: number, locale: 'de' | 'en'): string
```

`formatPrice` produces `"10,50 €"` for `de` and `"€10.50"` for `en`, per spec 8.7.

`catalogItemState.ts`:

```typescript
export type CatalogItemState = 'available' | 'soldOut'
export function itemState(item: CatalogItem): CatalogItemState
export function isLineFlaggedSoldOut(line: BasketLineView, catalog: Catalog): boolean
```

A deactivated item is never modelled here, because spec 5.3 says it is absent from the payload
entirely; `CatalogItemState` therefore has exactly two members, not three.

`tableLabel.ts`:

```typescript
export function isTableLabelValid(label: string): boolean
export function suggestionMatches(query: string, suggestions: string[]): string[]
```

### 3.3 Pinia stores

Each store owns exactly the concerns `frontend/CLAUDE.md` assigns to stores: SignalR consumption,
reconnect-refetch, and (in `connection.ts` only) the 15 second polling fallback, written once and
reused by every store that needs it rather than duplicated.

```typescript
// stores/connection.ts
export const useConnectionStore = defineStore('connection', () => {
  // owns the SignalR HubConnection lifecycle, exposes `state: 'connected' | 'reconnecting' | 'offline'`
  // on every reconnect, calls every registered refetch callback exactly once
  // if the hub never reaches 'connected' within its first attempt, starts a 15s polling timer
  //   that calls the same refetch callbacks; the timer is cancelled the moment the hub connects
  function registerRefetch(callback: () => Promise<void>): void
  function onEvent<T>(eventName: string, handler: (payload: T) => void): void
})
```

`session.ts` owns `deviceToken`, `serverPerson`, `language`, redemption, and revocation handling
(`DeviceRevoked` clears the token, keeps the draft). `catalog.ts` owns the cached `Catalog`,
refetching on `CatalogChanged` and on reconnect. `order.ts` owns the draft cart wrapper actions,
delegating every rule to `src/core/draftCart.ts` and `submission.ts`.
`printerStatus.ts` owns the header/basket banners from `PrinterStatusChanged`. `station.ts` owns the
break-glass page's filter, ticket list (refetch-only, never payload-rendered, per section 2.4), and
the ten-second undo timer described in spec 8.9. The `admin/*` stores are thin wrappers around their
REST calls with no draft-cart-like state of their own.

## 4. Ordered steps

1. **Core modules, test-first.** Delete `frontend/src/core/placeholder.ts` and
   `frontend/tests/core/placeholder.spec.ts` first, per T001's own rule that the first task adding a
   real `src/core/` module retires its placeholder. Then, for each module in section 3.2, write its
   Vitest spec before the module exists (TDD red-first, root rule 3), run it, paste the failing
   output, then implement. Cover in this order: `draftCart` (persistence, restore,
   clear-only-on-acceptance, no list/timer/state field), `submission` (id generated once, reused by
   retry/reload/re-enrolment, new order gets a new id), `totals`, `basket`, `routingPreview`,
   `catalogItemState`, `tableLabel`.
2. **i18n resource files.** Copy every string table in `docs/spec.md` section 8 into
   `src/locales/de.json` and `src/locales/en.json` verbatim, key for key, exactly as printed in the
   spec's tables. Do not paraphrase, shorten, or "improve" a single string; the spec's tables are the
   product's approved copy. Every `{count}` key listed in spec 8.1 goes through vue-i18n plural
   forms with its own singular form written alongside the general one. Confirm every key exists in
   both files with an identical placeholder set (this is also asserted in
   `tests/core/i18nCoverage.spec.ts`, a small script-driven test comparing the two JSON files' key
   sets and placeholder sets).
3. **Router and app shell.** `router/index.ts` wires the screen map from spec 8.2. `App.vue` mounts
   `AppHeader.vue` on every server-facing route and nothing on the station route beyond its own page.
4. **Enrolment.** `EnrolQr.vue` and `EnrolCode.vue`, backed by `session.ts`. Redeem, store the token,
   render `enrol.orderHeld` when a draft already exists in `localStorage` at the moment enrolment
   completes (this is the direct product of a device revocation while a draft was open, per spec 9.4;
   its component test seeds a draft first and asserts the sentence renders).
5. **Header.** `AppHeader.vue` and `SettingsSheet.vue`, backed by `connection.ts` and
   `printerStatus.ts`. Component test drives every banner combination listed in spec 8.5, including
   `header.stationWaiting` appended to an existing banner rather than shown on its own.
6. **Catalog and basket.** `Catalog.vue` and its children, backed by `catalog.ts` and `order.ts`.
   Component tests: a sold-out item renders greyed with `catalog.soldOut` and is not tappable; a
   one-candidate item never opens `LineStationSheet.vue`; a two-candidate item opens it once per line
   add and the choice is per-line, never remembered for the next line.
7. **Review.** `Review.vue`, backed by `order.ts`. Component tests: send button disabled while the
   table field is empty with `review.tableMissing` under it; a failed submission leaves every line,
   quantity, station, and the total exactly as they were with `review.sendFailed` and the retry
   button; a second consecutive failure adds `review.sendFailedAgain`; `review.totalChanged` renders
   when the response's `totalCents` differs from the request's `expectedTotalCents`.
8. **Break-glass station page.** `StationPage.vue`, backed by `station.ts`. Component test for
   `StationTicketRow.vue` covers `canAcknowledge` true/false rendering, the `station.reprint` chip,
   and the ten-second undo delay (`station.takenPending`, the undo button, and that undo cancels
   the pending send without ever calling the acknowledge endpoint; use a fake timer, not a real
   ten-second wait).
9. **Admin pages.** Items with the sold-out toggle, locations, assignments, printers including the
   mock fault panel, invitations and the people list with its three row actions
   (`admin.people.newCode`, `admin.people.rename`, `admin.people.revoke`), event session start.
   `NotOnLaptop.vue` renders `admin.notOnLaptop` when the admin route is opened from a non-loopback
   origin (the frontend cannot itself detect this; it renders whatever the backend's 404-vs-page
   decision from spec 5.1 already produced, so this view exists for the one case the backend does
   serve a page: opening `/admin` itself, per spec 5.1's own carve-out).
10. **The one required Playwright spec.** Add `frontend/e2e/order-placement/order-placement.spec.ts`
    and its co-located `order-placement.flow.md` inside T001's existing
    `frontend/e2e/playwright.config.ts`; do not add a second Playwright config. Add
    `"test:e2e": "playwright test --config e2e/playwright.config.ts"` to `frontend/package.json`,
    since T001 installed `@playwright/test` but wrote no script for it. Mark the spec so `npm test`
    (Vitest) never touches it and so a run without a live backend stays green (section 6).
11. **SignalR client package.** Add `@microsoft/signalr` to `frontend/package.json`. This is an
    addition beyond T001's allowed frontend package list (Vite/Vue/TypeScript plus `pinia`,
    `vue-i18n`, `vitest`, `@vue/test-utils`, `jsdom`, `@playwright/test`); it is stated here as an
    architect-approved exception on the same footing as the backend's QRCoder and SignalR.Client
    exceptions, because `connection.ts` (section 3.3) needs a real `HubConnection` and nothing on
    that package list provides one.
12. **Full verification pass**, section 5.

## 5. Constraints

- `src/core/` never imports `vue`, `pinia`, or touches `window`/`document`, except `draftCart.ts`'s
  documented, deliberate use of `localStorage` (section 3.2). Every other module in `src/core/` is
  pure functions over plain data.
- Every discriminated-union switch (`TicketStatus`, `OrderStatus`, `CatalogItemState`, the mock fault
  kind, the printer transport kind on the admin side) ends in `assertNever`, per
  `frontend/CLAUDE.md` rule 2. No `else`, no trailing catch-all `if`.
- No offline retry queue, ever, in any form. `draftCart.ts` holds one order with no list, no timer,
  no state field, per root rule and `frontend/CLAUDE.md` rule 4; section 2.5 above restates the exact
  shape so this cannot drift during implementation.
- No string literals in templates or components. Every user-facing string goes through vue-i18n, and
  every key exists in both `de.json` and `en.json` in the same change, per root rule 8 and
  `frontend/CLAUDE.md` rule 5.
- Before writing or changing any user-facing string, apply the `no-ai-slop` skill to the prose and
  the `writing-ui-guidance` skill to its structure and placement. Because this task's strings are
  copied verbatim from the spec's approved tables (step 2 above), most of that work is already done
  by the spec; the skills bind any string this task writes that is not already in a spec table (a
  fallback message, a component's internal label not covered by section 8's tables, or a later
  addition), and bind the review of every copied string as well.
- Unit tests follow `writing-unittests`: one behaviour per test, arrange-act-assert visibly
  separated, expected literals visible in the test body, no math recomputing what production code
  computes, watched red before the implementation exists. Component tests target observable output
  (rendered text, disabled state, emitted events), never internal store calls.
- The one Playwright spec follows `writing-webtests`' two-phase discipline adapted to this project:
  a flow spec (`order-placement.flow.md`) is written first, listing the exact user journey step by
  step and the exact literal values expected at each step (order number pattern, slip counts, total
  formatting), before any test code exists; the spec is then transcribed mechanically into
  `order-placement.spec.ts`. It drives the real UI end to end (redeem, build a basket, review, send)
  against the real backend, never importing a store or seeding state directly, matching that skill's
  principle 1. It is a single full-journey test, not a set of micro assertions on individual buttons.
- No `assertNever` violation and no TypeScript `any` anywhere in `src/core/` or `src/stores/`.
- No code comments, per root rule 7.
- No em-dash character anywhere, including inside this document if it is edited further, per root
  rule 10.

## 6. Verification

Run both from `frontend/`, quote the real output:

1. ```powershell
   npm run build
   ```
   Success: the vue-tsc typecheck and Vite build complete with zero errors, and
   `backend/GastronomyApp.Api/wwwroot/` contains the built output afterward.

2. ```powershell
   npm test -- --run
   ```
   Success: every Vitest spec under `tests/` passes, with a summary showing zero failed. This
   command must stay green with no backend running and no Playwright browser installed; the e2e spec
   lives outside Vitest's `include` glob and is never picked up by this command.

The Playwright spec (`npm run test:e2e`) is defined but marked `test.skip` with a stated reason
("requires the backend from T002-T005 and a practice event session, per spec 11.3") until a sibling
task wires it into a real run against the completed backend. Do not delete or leave it unwritten;
write the full spec now so the consistency review and the eventual backend task have something
concrete to run against, and note in `order-placement.flow.md` exactly which backend task must land
before the skip is lifted.

## 7. Out of scope

- CSV admin surfaces (`/api/admin/export/orders.csv`, `/api/admin/catalog/import`), the admin log
  viewer, the backup button, the diagnostics screen, printer discovery UI beyond what step 9 needs
  for the mock fault panel (the discovery button and result list for a real network printer are
  in scope for `PrintersList.vue`; the diagnostics/log/backup screens are not, per the task brief).
- Anything the slice does not touch: table suggestions management screen is in scope (spec 8.8 names
  it as part of catalog setup this slice needs); the admin orders list/detail beyond what section 2.2
  already excludes is not.
- The desktop window and anything under `desktop/`.
- The backend itself: REST endpoints, SignalR hub, EF Core entities, printer transports. This task
  only consumes the shapes in section 2.
- Camera-based QR scanning inside the web app; the spec is explicit this cannot exist over plain
  HTTP (spec 8.3).
- A service worker, PWA install, or anything requiring a secure context.
- Running the Playwright spec against a live backend; that is a later task's job once T002-T005 exist.

## 8. Assumptions and ambiguities, with the reading chosen

- **Order status enum members, corrected by the consistency review (ruling 5).** `OrderStatus` is the
  full four-member set spec 3.1's state table and T002's `OrderStatus` enum both give:
  `Accepted`, `Printing`, `Printed`, `NeedsAttention`. The acceptance response carries one of those
  four and the phone reads it as it is given, so the enum it types against is the backend's own.
- **`admin.notOnLaptop` rendering path.** Spec 5.1 says the admin page is served from every address
  but only loopback gets a working admin API; a phone opening `/admin` gets one sentence. Chosen
  reading: the frontend's router still mounts `NotOnLaptop.vue` for every `/admin/...` path (not only
  the bare `/admin` root), because a phone that has bookmarked a deep admin link should see the same
  sentence rather than a broken screen; the actual gate is the backend's 404 on the admin API calls
  that view would otherwise make, so no admin data ever reaches a non-loopback caller regardless of
  which admin route it opened.
- **Where `EnrolCode.vue`'s and `EnrolQr.vue`'s shared name field lives.** Spec 8.4 says the name
  field is "the one from section 8.3". Chosen reading: both views import the same `NameField.vue`
  component rather than duplicating the input, so the single-term-per-concept rule (spec 8.1) is
  enforced by having one component rather than by two components agreeing to use the same string key.
- **Station page route and the SPA shell.** Spec 5.6 lists `GET /station/{accessKey}` as returning
  "the single page app shell, in station mode." Chosen reading: this is the same Vue app and the same
  `index.html` as every other route (there is one build, one `wwwroot`), and "station mode" means the
  Vue Router matches `/station/:accessKey` to `StationPage.vue` and mounts no `AppHeader.vue`, no
  device-token session store, and no draft-cart logic on that route; it is a routing distinction, not
  a second build target.
- **`admin.assignment.preview` and `admin.assignment.previewChoice` live computation.** Spec 8.8 shows
  these as live text under the assignment editor. Chosen reading: they are computed from
  `routingPreview.ts`'s `candidateLocations`/`needsStationChoice` against the in-progress (unsaved)
  form state, not from the last-saved catalog, so an admin sees the routing consequence of a checkbox
  they have not yet submitted.
- **Table suggestion chips ordering.** Spec 8.7 shows suggestion chips above the table field but does
  not state a match algorithm. Chosen reading: `tableLabel.ts`'s `suggestionMatches` does a
  case-insensitive substring match against `catalog.tableSuggestions`, preserving each suggestion's
  `sortOrder` from the admin's configured list (spec 5.5's table-suggestions endpoint), never
  re-sorting by match relevance, so the admin's deliberate ordering is not silently overridden by a
  fuzzy-match heuristic.
