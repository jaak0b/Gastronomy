# Revision log for `docs/spec.md`

This log belongs to an earlier version of the specification, one built around thermal printers and
printed slips. The fire department has since chosen tablets, printing has been removed from the
product entirely, and `docs/spec.md` has been rewritten around the station tablet. Read this file as
a record of how those earlier findings were dispositioned. Every entry about printers, print jobs,
slips or paper describes a mechanism that no longer exists, and the section numbers it cites are
those of the old document rather than the current one.

What this file is: a disposition for every one of the 39 findings in `docs/spec-review.md`, followed by
a record of the three structural changes the owner asked for during this pass.

Dispositions used:

* **Fixed**: the spec changed, and the line says what now happens.
* **Dissolved**: the finding described a defect in a mechanism the owner has since removed. Nothing was
  patched, because there is nothing left to patch. The line says which removal did it.
* **False positive**: the finding is wrong about what the spec said. None occurred.
* **Owner decision needed**: a product trade-off that is not the author's to make. The line names the
  trade-off and the open question it became.

Counts: 15 blocking (12 Fixed, 3 Dissolved), 24 non-blocking (21 Fixed, 3 Dissolved), 0 false
positives, 0 deferred to the owner as a disposition.

Exactly one numbered finding became an open question as well as being fixed: B10, which became open
question 10. Three more open questions came from parts of the review that are not numbered findings:
ranked missing piece 2 became open question 2, ranked missing piece 12 became open question 9, and the
challenge to the admin loopback decision rewrote open question 3. In the other direction, N12 closed
the old open question 6, which was removed from the list.

---

## Blocking findings

| # | The finding, in one line | Disposition | What the spec says now |
|---|---|---|---|
| B1 | The per-location counter scope key is 90 characters and the column is 80, so no order can ever be accepted | Fixed | `NumberCounter` has a composite primary key of `CounterKind`, `EventSessionId?` and `ProductionLocationId?` (2.13). No string is built or parsed, and 4.1 gives the three counter kinds in a table. |
| B2 | The break-glass page can mark a live, queued ticket as handled, silently dropping the order | Fixed | 5.6 defines the returned set on the server: `Failed`, `Unknown`, `Blocked`, and `Queued` behind a printer offline longer than the give-up window. Acknowledge refuses anything else with a stated reason. 8.10 says the page is empty during a working evening and why that is the whole safety of it. |
| B3 | Tickets can park forever behind a disabled printer or a session change, while the phone says "Wird gedruckt" | Fixed | The give-up window runs from `LocationTicket.CreatedAtUtc` (3.2), so a held ticket reaches `Failed` with `FailureReason: StationDisabled` and a phone message with an action in it. The worker enqueues non-final tickets of **any** session on startup (7.3). Starting a session refuses while any ticket is non-final or any question is open (2.3). The admin overview counts waiting slips. |
| B4 | The `Unknown` question is unanswerable when the placing phone dies, is revoked, or is set up again | Fixed | `POST /api/admin/orders/{id}/tickets/{ticketId}/resolve` exists (5.5) with its admin strings. Orders, order detail, resolve and reprint are scoped by `ServerPersonId`, not `DeviceId` (5.4). SignalR gained a `person:{serverPersonId}` group (6.1). An admin can reassign a phone to a different person for a shift handover (5.5). |
| B5 | Voiding an order does not cancel its queued print jobs, so a cancelled order can still be cooked | Dissolved | Owner decision: order cancellation is removed from version 1 entirely. There is no void endpoint, no `Voided` order or ticket state, and no `Cancelled` print job outcome anywhere in the spec. 1.5 records the reasoning and 3.6 says what a server does instead. |
| B6 | Every station is created on a mock printer that reports every order as successfully printed | Fixed | A mock station's ticket reaches `PrintedOnTestPrinter`, never `Printed` (3.2, 7.8). In a real event that maps to `NeedsAttention` (3.1) and the phone says no slip is on the pile (`ticket.testPrinter`). Starting a real event refuses while any active station is on the mock (2.3), and `EventSession.IsPractice` makes the mock the expected transport during a practice run. |
| B7 | The spec contradicts itself on whether revoking a device destroys its unsent orders | Dissolved | Owner decision: there is no queue of unsent orders. The spec now states in one place (6.2, 9.4) that a revoked phone keeps its half-built draft order, and `enrol.orderHeld` tells the reader so on the enrolment screen. |
| B8 | The offline queue is wrongly declared unrecoverable, and a given-up entry blocks every later order | Dissolved | Owner decision: the automatic retry queue is removed. Both halves of the finding described the queue's behaviour. Section 9 is rebuilt around a draft cart plus a manual retry, and 9.2 states plainly that `localStorage` survives a closed tab, a killed browser and a reboot. |
| B9 | The order status projection contradicts the ticket machine and its mapping is never given | Fixed | 3.1 no longer draws the order status as a machine. It is a recomputation, given as a five row priority table that is total over every combination of ticket states, with the reason the diagram was the wrong shape. 11.1 tests the table exhaustively. |
| B10 | A change of the laptop's IP strands every enrolled phone and its stored orders | Fixed | Giving the laptop a fixed address is checklist step 9 in both languages, with the two ways a volunteer can do it. 5.5 states the consequence and the recovery outright. `admin.overview.addressChanged` names the previous and current address. Advertising a hostname instead became open question 10, because it costs a dependency. |
| B11 | An item can become unroutable and acceptance has no defined answer | Fixed, partly Dissolved | The zone removal deleted the fallback rule the finding was mostly about. What remained is fixed: the candidate set is the item's **active** locations (2.6), an item cannot be saved with none (2.5), a station cannot be deactivated while it is the last one for an active item (2.4), and an item cannot be deactivated during a live event. A station chosen by the server that was switched off in the meantime falls to the lowest `SortOrder` candidate and the slip prints which station was chosen. |
| B12 | The headline retry rule contradicts its own table one line below | Fixed | 7.6 splits `SocketDropped`, `Timeout` and `PrinterError` by byte count, so zero bytes always retries and any bytes never does. `BytesWritten` is defined in 2.11 and 7.2 as bytes handed to the socket, with the note that acknowledgement is unknowable. |
| B13 | Enrolment lockout is global, trivially triggered by a stranger, and has no unlock | Fixed | Failures are counted per source address and lock only that address (2.8). A 410 on a consumed or expired code is explicitly a race, not a failed attempt. `GET /api/admin/enrolment/locks` and `POST /api/admin/enrolment/unlock` exist (5.5) with admin strings. `enrol.error.codeUsed` now says to scan again, which is the instruction that works during a briefing. |
| B14 | The print queue is unbounded and a slowly failing station generates questions for an hour | Fixed | 7.6 adds a station circuit breaker: two consecutive `Unknown` or `Timeout` outcomes, or a queue depth of ten, sets `PrinterStatus.IsFaulty`, pushes it to every phone, and fails every waiting ticket in one transaction. The give-up window starts at ticket creation. The admin reconnect action clears it. **Superseded in part, see the note below.** |
| B15 | The phone labels two different numbers "Nr." and the `Unknown` question names the wrong one | Fixed | One word per number, everywhere: "Bestellung 137" / "Order 137" and "Bon 042" / "Slip 042". The slip header prints `BON 042` / `SLIP 042`. `orders.row`, `orders.ticket`, `orders.detailTitle`, `station.row` and `ticket.unknown.action` all changed, and the rule is stated in the conventions, in 4.4 and in 8.1. |

### What changed in the spec after these dispositions were written

This table records what the spec said when each finding was closed, and two of those sentences no
longer describe the current document. Both are left in place rather than rewritten, because a
disposition that is edited to match today's spec stops being a record of anything. What follows is the
difference.

* **`TimedOut` was renamed to `Timeout`.** B12 and B14 above were written before section 7.2 unified the
  outcome spelling across `PrintDispatchOutcome`, `PrintAttempt.Outcome` and `PrintJob.FailureReason`.
  The enum member has been `Timeout` since that pass, and the two mentions above are quoted in the
  current spelling so a reader searching the spec for the name finds it. Nothing about either
  disposition changed, only the word.
* **The circuit breaker's queue depth trigger was removed.** B14 was closed with two triggers, two
  consecutive unknown outcomes or a queue of ten waiting tickets. The owner has since removed the depth
  trigger entirely, because it turned an ordinary paper change into a reported station fault: ten
  orders in four minutes is normal at a busy bar, so the roll running out failed ten tickets and told
  ten servers their order had failed about a minute before all ten printed correctly. The two-unknowns
  trigger, which is what actually answers B14's finding, is unchanged. Queue depth is now shown as
  information on the station banner and the admin overview and read by nothing. Section 7.6 carries the
  reasoning.
* **The give-up window no longer runs while the cause is known**, which touches B3 above. B3 was closed
  by making the window run from `LocationTicket.CreatedAtUtc` so that nothing could park forever. The
  window is now suspended while the station's blocking cause is known and fixable (paper end, cover
  open, station disabled, the mock's folder unwritable), and B3's guarantee is carried instead by a 20
  minute outer bound measured from the same field, which no cause suspends. B3 remains fixed: a ticket
  still cannot sit unresolved with the phone saying "Wird gedruckt" all evening.
* **The break-glass page lists every open ticket**, which touches B2 above. B2 was closed by restricting
  the listing, on the reasoning that a helper could acknowledge a live queued ticket. The owner has
  since asked for the full listing, because a station whose printer has died has to work every order off
  the screen and not only the failed ones. B2's actual danger was never the listing, it was the button,
  and the restriction moved onto the button: a row offers "Übernommen" only when its station genuinely
  cannot print, and the endpoint refuses the rest. B2 remains fixed, by a different mechanism.
* **The rolling enrolment code was replaced by one invitation per person**, which supersedes B13 above
  and touches N1, N3 and N6. B13 was closed by making the lockout per source address, adding an unlock
  endpoint and treating a consumed code as a race rather than a failed attempt. None of that exists any
  more. The admin now creates one invitation for one person, the waiter scans it and types their own
  name, and at most one invitation is outstanding at a time, so the race B13's machinery existed to
  distinguish from an attack cannot occur. **B13 is dissolved rather than fixed:** there is no failure
  counter per address, no lock, no unlock endpoint and nothing on the admin screen about locks. What
  remains is a cap on guessing the six digit fallback, counted on the invitation itself rather than on
  an address, which deliberately cannot lock anybody out because the QR stays valid throughout.
  Consequently N1's "rotation service's in-memory state" no longer exists, and N6's `admin.devices.*`
  string group is gone with the screen it named, though the terminology rule it settled still holds.
* **Two counters were rekeyed from the location to the printer address**, which touches N3 above. N3 was
  closed by making the process id counter a persisted row per location. Since two stations may now share
  one printer, a per-location counter could hand the same process id to one socket twice and let a late
  echo from a timed out job confirm the next job as printed. The counter is keyed by printer endpoint,
  and the print workers were rekeyed the same way so that one printer is served by exactly one
  connection. N3 remains fixed, by a counter with a wider key.

## Non-blocking findings

| # | The finding, in one line | Disposition | What the spec says now |
|---|---|---|---|
| N1 | `SixDigitDisplay` is an ERD column and is described as never persisted | Fixed | Removed from the ERD. 2.8 states that the six digits and the QR URL of the current code live in the rotation service's in-memory state. |
| N2 | A test print has no representation in the model | Fixed | `PrintJob.Kind` gained `Test`, `LocationTicketId` is nullable, and `ProductionLocationId` was added to the job so a test print belongs to a printer rather than an order (2.11). |
| N3 | The process id counter has no home and 4.1 denies it exists | Fixed | It is a `NumberCounter` row of kind `PrinterProcessId` per location (2.13, 4.1), so it survives a crash. 2.11 and 7.4 add that an echo is accepted only on the socket that sent the job. |
| N4 | The admin UI is unreachable from the address the overview tells the volunteer to use | Fixed | The admin API accepts loopback plus the laptop's own bound addresses (5.1). The admin page is served from every address and renders `admin.notOnLaptop` with the laptop's URL when it is not the laptop. |
| N5 | Two localization mechanisms produce the same message and the device's language is ignored | Fixed | The error envelope carries `code`, `messageKey` and `parameters`, and the client renders it in its own language from its own resource file (5.1). `Accept-Language` is gone. Slips stay server-rendered from resx, because a printer has no resource file. |
| N6 | German drifts between einrichten, anmelden and abmelden for one concept | Fixed | German is "einrichten" and "Einrichtung entfernen", English is "set up" and "remove", across `enrol.*`, `admin.enrol.*`, `admin.devices.*` and `admin.overview.ready`. |
| N7 | "Leave this page open" is stated three times at three strengths | Dissolved | Owner decision: with no background sending there is no such rule to state. The header no longer carries a queue count, `order.notSent` and `order.giveUp` no longer exist, and section 9 says in one place that nothing sends while the page is closed. |
| N8 | A price change between the catalog fetch and acceptance is invisible to everyone | Fixed | The request carries `expectedTotalCents`. A difference never rejects the order and comes back in the response, and `review.totalChanged` tells the server the new total to say to the guest (5.4, 8.7). |
| N9 | "Positionen" counts lines on one screen and units on the slip | Fixed | "Position" is one line and "Artikel" is one unit, stated in 7.7 and used consistently. The basket bar reads `{count} Artikel, {total}` and the slip footer reads `Artikel gesamt: 6`. |
| N10 | `IsPaperNearEnd` is captured, pushed and never shown | Fixed | `admin.overview.paperNearEnd` and `admin.printers.paperNearEnd` exist. 7.5 states that it is deliberately not shown on the phones, because the person who can act is at the laptop or the station. |
| N11 | A blocked ticket can be overtaken, producing a transient gap in the pile | Fixed | 7.3 and 4.2 state that a printer's jobs are attempted strictly in `LocationSequenceNumber` order and that a blocked job holds the station. An end to end scenario covers it. |
| N12 | Copying `gastronomy.db` can miss the WAL file, so the backup is silently incomplete | Fixed | 10.5 keeps WAL and adds a backup button that runs `VACUUM INTO` a dated file, plus an automatic copy when a session ends. Checklist step 22 tells the volunteer to copy that file. This also closed the old open question 6, which has been removed. |
| N13 | SQLite contention, disk full and file permissions are never mentioned | Fixed | 4.1 states a 5 second busy timeout. 5.1 states that a failed write returns 503 with `DatabaseUnavailable` and a user-worded message, and that startup verifies the database directory is writable and refuses to start with a plain sentence when it is not. |
| N14 | `beforeunload` is not a reliable backstop on these phones | Dissolved | Owner decision: nothing is pending in the background, so there is no backstop to be unreliable. `beforeunload` appears nowhere in the spec. The draft cart is restored on load instead. |
| N15 | Nothing forbids the camera API and a future developer will reach for it | Fixed | 8.3 states that the scan happens in the phone's own camera app and that the web app never accesses the camera, with the secure context reason. |
| N16 | A device language endpoint exists with no screen and no strings | Fixed | A settings sheet opens from the header (8.5) with `settings.language`, `settings.languageGerman`, `settings.languageEnglish` and `settings.person`. The zone screen the reviewer suggested as its home no longer exists. |
| N17 | `ItemLocationAssignment.Priority` has no UI and produces a tie the admin did not configure | Fixed, and dissolved by the zone removal | `Priority` is deleted. With more than one candidate the server chooses per line, so there is no automatic tie to break. `ProductionLocation.SortOrder` is documented as the only ordering, used for the display order and for the stale choice case. |
| N18 | The CSV import and export formats are undefined | Fixed | 5.5 specifies both: UTF-8 with a byte order mark, semicolon delimiter, the column list for each, comma decimals in the export, `3,50` / `3.50` / `3` accepted in the import, stations separated by `\|`, a row with no station rejected, and nothing imported when any row fails. |
| N19 | `POST .../resolve` is not scoped to the placing device | Fixed | Resolve, reprint and order detail require the caller's `ServerPersonId` to match the order, or an admin caller (5.4). A different person gets 403. `ticket.unknown.answered` was reworded, since the only other answerer is the same person on another surface. |
| N20 | Three German strings read awkwardly | Fixed | `enrol.success` is "Das Telefon ist eingerichtet." `review.tableHelp` is "Tragen Sie den Tisch ein, damit das Tablett zum richtigen Tisch kommt." `zone.intro`, the third one, no longer exists. |
| N21 | Price formatting in English is not idiomatic | Fixed | 8.7 specifies `10,50 €` in German and `€10.50` in English. |
| N22 | The slip's timestamp is undefined for reprints | Fixed | 7.7 states that the time on the slip is always the order time, and that a reprint prints its reprint time once, under the reprint banner. |
| N23 | The two-column slip line has no defined overflow | Fixed, partly dissolved | The `Bereich` half of that line went with the zones, so every line on the slip is now a single field. 7.7 states that item names, table labels, server names and station names all wrap onto an indented continuation line and that nothing is ever dropped. |
| N24 | Five horizontal rules have no blank line before them | Fixed | Every `---` in the file has a blank line before it, checked mechanically over the finished document. |

## Challenges to the author's judgment calls

| Challenge | Response |
|---|---|
| Table label as free text with suggestions holds up | Kept unchanged, as the owner confirmed. Adopted the reviewer's addition: `POST /api/admin/table-suggestions/from-last-session` offers the labels servers actually typed, so the second festival starts with a real list. 2.7 also states that "Tisch 12" and "T12" are accepted as two labels for one table, because nothing aggregates by table. |
| The routing fallback's principle holds but its implementation does not | Dissolved by the zone removal. There is no fallback rule, because there is no filter that can empty the candidate set. 2.6 states the invariant and 2.4 and 2.5 enforce it. |
| Admin loopback holds, but the admin page is the wrong case for a 404 | Adopted in full (N4). Also adopted the reviewer's stronger point: the task that needs mobility is knowing a printer stopped, which already reaches every phone, so checklist step 19 now tells the person running the evening to carry a phone. Open question 3 was rewritten around that. |
| The 32 character access key is fine, its distribution is the defect | Adopted. Each station has a printed card with a QR code to its break-glass URL, printed from the admin and also printed on the test slip during setup, and checklist step 14 says to tape it inside the printer lid. 2.4 states why the key is stored in plaintext and that it is kept out of the request log. |
| No total on the slip holds up | Kept unchanged. |
| Five minutes for a ticket holds, ten minutes for the phone is the wrong shape | The five minute window is kept and its clock start is now defined (3.2). The ten minute window is gone entirely with the queue, so the reviewer's redesign of it was not needed. |
| The `EventSession` entity is the best structural decision | Kept, with all three risks closed: 2.3 refuses to start a session while orders are recent, tickets are open or questions are unanswered, the duplicate numbering direction is now stated alongside the gap direction, and 7.3 enqueues tickets from previous sessions. |

## Missing pieces the reviewer ranked

| # | Piece | Response |
|---|---|---|
| 1 | Correcting or cancelling an order after it has printed | **Owner-rejected for version 1.** Cancelling in software does nothing to paper that already exists, and a cancel button would tell the server an order was withdrawn while the kitchen went on cooking it. Section 3.6 and the 1.5 non-goals carry that reasoning, and `order.changedMind` gives the server the sentence the reviewer said the product owed them, in the order detail where they are already looking. |
| 2 | A second printer as a station's fallback | Open question 2, with the reviewer's argument recorded. It is a real feature, and version 1 is being cut towards the simplest thing that works. |
| 3 | Printer discovery | Adopted. `POST /api/admin/printers/discover` scans the laptop's own /24 for port 9100 and pushes `PrinterDiscovered`. Checklist step 13 is now "search, then tap", with the self test as the fallback. |
| 4 | An end-of-evening check | Adopted as checklist step 21 in both languages, before the backup. |
| 5 | Unanswered `Unknown` tickets at the end of the night | Adopted. Starting a session refuses while a question is open, and `admin.event.blockedQuestions` names how many. |
| 6 | Shift handover of a phone | Adopted. `PUT /api/admin/devices/{id}` takes a `serverPersonId`, with `admin.devices.reassign` and its help string. Earlier orders stay with the person who placed them. |
| 7 | Logging and diagnosis | Adopted. 10.4 specifies a rolling daily file next to the database kept fourteen days, `GET /api/admin/log` serves it, and the diagnostics screen links to it. |
| 8 | Break-glass link distribution | Adopted. See the station card above. |
| 9 | Near-end paper warning | Adopted (N10). |
| 10 | Recovery when the laptop's address changes | Adopted (B10). |
| 11 | A practice mode separate from the event | Adopted. `EventSession.IsPractice` exists, practice orders are excluded from the export, the test printer is expected during a practice run, and checklist step 6 uses it. |
| 12 | What the black console window means | Partly adopted. The checklist says it in bold in both languages and `admin.overview.console` says it on screen. A tray icon or a guarded close is open question 9, because it is a real change to a single-file console program. |

---

## Change 1: the Zone concept is removed

Zones are gone. `CatalogItem` keeps its one or more candidate production locations, and that assignment
is the whole of the kitchen versus bar split. An item with one candidate routes silently and renders no
control. An item with more than one is chosen by the server on the order line, at the moment the line
is added, and the choice belongs to that line and to nothing else.

Parts of the spec this touched:

| Where | What changed |
|---|---|
| Conventions | "zone names" removed from the list of admin-typed data |
| 1.3, 1.5 | Break-glass cross reference renumbered. A non-goal row added recording why zones were killed, so the change request is answered once |
| 2.2 ERD | `Zone` entity deleted, together with `ProductionLocation.ZoneId`, `Device.CurrentZoneId`, `Order.ZoneId` and `TableSuggestion.ZoneId`. `TableSuggestion` now has no relationship at all |
| 2.4 Zone | Section deleted. Sections 2.5 through 2.14 renumbered to 2.4 through 2.13, and every cross reference updated |
| 2.4 ProductionLocation | `ZoneId` removed. A deactivation gate added so a location cannot be the last one for an active item |
| 2.5 CatalogItem | The "at least one assignment" invariant is now a hard save gate, and an item with zero locations never appears in the catalog |
| 2.6 Routing | Rewritten. `Priority` deleted. Zone filtering deleted. **The fallback that fired when zone filtering matched nothing is deleted**, with the invariant that makes it unnecessary stated in its place |
| 2.7 Table naming | `TableSuggestion.ZoneId` removed. Suggestions are one list |
| 2.8 Device | `CurrentZoneId` removed. `Language` added, because the zone screen was going to be the language toggle's home |
| 2.9 Order and OrderLine | `Order.ZoneId` removed. `OrderLine.WasManuallyRouted` replaced by `ChosenProductionLocationId`, which is the server's choice rather than a flag derived from it |
| 3.5 | The "wrong zone" rows are gone; the disabled station and test printer rows replace them |
| 5.2 | The redemption response no longer returns `zones` or `currentZoneId`. `PUT /api/session/zone` deleted |
| 5.3 | `zones` removed from the catalog payload, `zoneId` removed from locations and table suggestions |
| 5.4 | `locationOverrideId` per line replaced by `productionLocationId`. The 428 "no zone chosen" status is gone. A 422 case added for a line that omits the station for an ambiguous item |
| 5.5 | The whole `/api/admin/zones` block deleted. Location create and update no longer take a `zoneId`. Item assignments are a plain list of location ids with no priority. Table suggestions are no longer scoped by zone |
| 6.2 | `PrinterStatusChanged` no longer filters banners by the server's zone |
| 7.7 | `Bereich: Innen` removed from the slip header, which also removed the two-column line N23 was about. A footer line added for the case where the chosen station differs from the printing one |
| 8.2, 8.5 | The zone screen is deleted from the screen map. The header's zone chip is replaced by the order list link and the settings button |
| 8.5 Choosing the zone | Section deleted with all five `zone.*` strings. Sections 8.6 through 8.12 renumbered to 8.5 through 8.11 |
| 8.6 Catalog | `catalog.changeLocation`, `catalog.locationAutomatic` and `catalog.locationOverridden` replaced by `line.whereTitle`, `line.whereHelp`, `line.station` and `line.changeStation`, and the screen description rewritten around the per-line question |
| 8.7 Review | Suggestion chips are no longer filtered by zone. The station on a line is changeable here |
| 8.9 Admin | `admin.zones.*` deleted. `admin.overview.missingZone` deleted. `admin.assignment.help` and the preview strings rewritten around one candidate versus several |
| 10.1, 10.2, 10.3 | "Areas" removed as configuration step 1 and as checklist step 3 in both languages, and the assignment step reworded |
| 11.1, 11.3 | `OrderRoutingResolver` and `routingPreview` tests rewritten. The zone filtering and fallback cases are replaced by the one candidate, several candidates, and stale choice cases. Two end to end scenarios added for the per-line choice |
| 12 | Nothing about zones remains in the open questions |

## Change 2: order cancellation is removed

Requested by the owner during this pass. There is no void endpoint, no `Voided` order status, no
`Voided` ticket state, no `Cancelled` print job outcome, no cancellation slip, and no cancel control on
any screen. 1.5 records the reasoning, and 3.6 gives the replacement: the server walks over and tells
the station, and `order.changedMind` says so in the order detail. The accepted cost, that a mistaken
order stays in the database forever, is stated in 1.5, and no cleanup mechanism was invented.

This dissolved B5 and removed the `Cancelled` state that had four declarations and no producer.

## Change 3: the automatic offline queue is removed

Requested by the owner during this pass. Section 9 is rebuilt rather than patched.

* **The draft cart.** One order, in `localStorage` under `draftOrder`, written on every change and
  restored on page load. 9.2 names it a draft cart and states in the spec that any implementation
  giving it a list, a timer or a state field has rebuilt the queue and should be rejected in review.
* **Manual retry only.** On failure the order stays on screen in full, with `review.sendFailed` and a
  retry button. No timers, no background loop, no give-up window for a submission. After the second
  failure `review.sendFailedAgain` adds the paper instruction, while the retry button stays.
* **Idempotent submission**, which is the risk the automatic queue was implicitly covering. The phone
  generates `clientOrderId` once, when the server first taps send, and reuses it for every retry of
  that order. It is regenerated by nothing: not a retry, not a reload, not a re-enrolment, not a new
  event session. It is stored on the order row under a unique index and kept forever, because orders
  are never deleted, so nothing expires and no cleanup job exists to be forgotten. A submission
  carrying an id that was already accepted finds the row inside the same `BEGIN IMMEDIATE` transaction
  that would otherwise insert, and returns 200 with the original order, its original numbers and its
  original tickets. No second order, no second print job. The 200 body is identical to the 201 it
  repeats, and 9.3 requires the phone to show it exactly as a first-time success, because to the server
  it is the same event.
* **Two end to end scenarios** cover it: a retry after a lost response produces exactly one order and
  exactly one slip per location, and a retry after a genuine connection failure produces exactly one
  order.

This dissolved B7, B8, N7 and N14.

---

## The one thing to settle before implementation: settled

`frontend/CLAUDE.md` used to carry the old model as a hard design constraint: "Zone is chosen once at
shift start and shown persistently in the header, with a per-item override available but never
required. Do not make the server pick a location per item in the normal flow." The revised spec
contradicted that file, and this pass left it alone, because changing a `CLAUDE.md` was outside what
was asked.

That file has since been updated and the contradiction is gone. Its design constraints now open with
"There are no zones", say that an item with exactly one candidate production location routes
automatically with no control rendered, put the choice for an item with several candidates on the order
line rather than on a session, a device or a shift, and forbid a remembered location preference and a
shift-start selection screen outright. Nothing is left to do here. A reader who came looking for the
constraint described above will not find it, because it is no longer there.

---

# Second review pass: dispositions for `docs/spec-review-2.md`

A disposition for every one of the 31 findings in the second adversarial review, which raised 9
blocking and 22 non-blocking findings against the 4056 line document. The dispositions are the same
four as above.

Counts: 9 blocking, all Fixed. 22 non-blocking: 21 Fixed, 0 false positives, 1 Owner decision needed.
No finding was dropped, and none was dissolved, because the owner removed no mechanism during this
pass.

Five judgment calls inside the blocking findings were decided by the architect before the pass began
and are recorded as settled rather than argued again: the outcome mapping depends on the transport kind
and lives only in 7.6; a `Printing` ticket is never acknowledgeable by anybody; the circuit breaker
follows the workers to the printer endpoint; the break-glass page joins one site-wide station group;
and the desktop's three seams are the locked data folder, the elevated relaunch of the same executable,
and the general repair action.

## Blocking findings

| # | The finding, in one line | Disposition | What the spec says now |
|---|---|---|---|
| B1 | The 7.6 outcome table sends every mock print to `Printed`, reopening the trap 7.8 exists to close | Fixed | 7.6's table gained a transport column and its `Confirmed` row split in two: `Network` or `Agent` maps the ticket to `Printed`, `Mock` maps it to `PrintedOnTestPrinter`. 7.6 states in its opening that it is the single mapping and that 7.8 and 3.5 restate it, with 7.6 binding on any disagreement. 7.4 step 9 now points at 7.6 instead of "section 3". 7.8's fault table says its ticket column is read out of 7.6. 11.1's `RetryPolicy` row tests both `Confirmed` rows by name. |
| B2 | The break-glass acknowledge rule permits a `Printing` ticket, which 5.6 asserts one line later that it forbids | Fixed | 5.6 states `canAcknowledge` as one boolean expression: the ticket is `Failed`, `Unknown` or `Blocked`, **or** (the location cannot print right now **and** the ticket is not `Printing`). A paragraph says in its own sentence that a `Printing` ticket is never acknowledgeable under any station condition, and why: bytes may be leaving the socket. 8.10 matches, and such a row renders `station.status.printing` instead of a button. 11.1's `TicketAcknowledgePolicy` and 11.2's break-glass row both walk a `Printing` ticket through all six printer conditions. |
| B3 | The admin screen instructs the operator to change the data folder mid-evening, and changing it destroys the evening | Fixed | 10.1 gained a subsection on what a settings change costs. **The data folder cannot be changed while an event session is active**: the field is disabled with `desktop.settings.dataFolderLocked` saying why, and an allowed change takes effect on the next start with nothing moved (`desktop.settings.dataFolderRestart`). `admin.printers.mockFolderUnwritable` was rewritten and no longer mentions the data folder setting at all: it says to announce the orders in person, then to repair the setup or free disk space. No message in the product now instructs a mid-service folder change. |
| B4 | The circuit breaker was never rekeyed to the printer endpoint when the workers were | Fixed | The breaker is a property of the printer endpoint, like the worker, the socket and the `PrinterProcessId` counter. A trip sets `IsFaulty` on every active location resolving to that endpoint and fails every waiting ticket at all of them in one transaction with `StationFaulty`; each affected location produces its own `header.stationFaulty` banner, so every one is named. `reconnect` on any of those locations clears all of them and restarts the one worker, and the response names them. Stated in 7.6, cross-stated in 2.12, 7.3, 3.5 and the 5.5 endpoint row, with new integration and end to end rows. |
| B5 | A station page filtered to another location never receives that location's new orders | Fixed | 6.1 replaced `station:{locationId}` with one site-wide `stations` group, with the reasoning: the access key already grants site-wide reading, so the group scope matches the page scope, and per-location filtering stays a client-side view concern. `OrderAccepted`, `TicketStatusChanged` and `PrinterStatusChanged` all push there. There is no leave-and-join on a filter change, so there is no window. 11.2's SignalR row and a new end to end scenario cover the filter case. |
| B6 | First run needs an elevated process, and 10.1 forbids the program from starting one | Fixed | 10.1's "one process" rule is now "one process serves the phones", with the exception stated plainly: the program relaunches **its own executable with a setup argument, elevated**, which creates the firewall rule, grants the folder rights and exits, hosting nothing. **The setup process is exempt from the single-instance mutex because it never hosts the server**, so it neither takes the mutex nor is refused by it, and the running instance waits for it. The mutex is taken by the window, and the single-instance section says so. |
| B7 | The ProgramData permission repair cannot run for the second Windows user, who is the person it exists to protect | Fixed | The repair is decoupled from first run. 10.2 defines the test as **"is the required state present"**, evaluated on every start, and says why a marker inside the unwritable folder was the trap. A folder that exists and cannot be written to now produces `desktop.error.dataFolderRepair`, which offers the elevated repair and never a different folder. 10.3 turned the firewall button into **"Repair the setup"**, one button running the same two idempotent actions, runnable by anybody who can answer the UAC prompt, with instructions as the only fallback. |
| B8 | Acknowledge can be undone for ten seconds, and there is no endpoint, transition, or event that undoes it | Fixed | 8.10 specifies the safer of the review's two readings and says so explicitly: **the undo is a ten second client-side delay before sending, not a reversal afterwards.** The row greys, holds its place and renders `station.takenPending` with a countdown; undo cancels it and nothing reaches the server. No undo endpoint, no `HandledOnPaper --> Queued` transition and no retraction event exist, and 8.10 says why `HandledOnPaper` cannot be taken back. The accepted cost, two phones each holding an unsent acknowledgement, is stated, and the loser gets 409 with the new `station.alreadyTaken`. Covered end to end. |
| B9 | The 20 minute outer bound is specified to fire "whatever is holding it", including a job with bytes on the wire | Fixed | 3.2 states the exception in the same breath as the bound: **the outer bound never fires on a ticket in `Printing`**, because failing it would send a server to announce an order the printer puts on the pile seconds later. Nothing parks, because `JobTimeoutSeconds` bounds the job at 90 seconds and the bound is evaluated again the moment the job ends, so the ticket is at most 90 seconds late. 11.1's `GiveUpWindow` row now asserts this instead of the opposite. |

## Non-blocking findings

| # | The finding, in one line | Disposition | What the spec says now |
|---|---|---|---|
| N1 | `PrintedOnTestPrinter` and `Printing` are missing from the mock's failure paths and the station status strings | Fixed | 7.8 gained a paragraph distinguishing its two zero byte rows, which run the clock and fail at five minutes, from the mock's unwritable folder, which is a suspending cause and ends at the 20 minute bound. `station.status.printing` was added to 8.10's table. |
| N2 | The location's slip language is specified but exists nowhere in the model, the API or a screen | Fixed | `SlipLanguage` was added to `ProductionLocation` in the ERD and in 2.4's field table, to the body of both location endpoints in 5.5 with `de` as the default, and to the admin station form with `admin.locations.slipLanguage` and its help string. 7.7 now names the field instead of asserting a setting that did not exist. |
| N3 | Every QR-consuming surface needs a raster encoder, which makes open question 11's second fallback impossible | Fixed | 10.1 states that a QR encoder is a version 1 requirement unconditionally, because the window draws a code on every start and the printable station card carries one, neither with a printer in the loop. Open question 11 was narrowed to how the symbol reaches the slip, and its second fallback, the URL as wrapped text with no symbol, is withdrawn. |
| N4 | Changing the port or the bind address in the settings window has no stated consequence | Fixed | 10.1's new settings subsection states the consequence in full and reuses the existing warning: both fields carry the sentence `admin.overview.addressChanged` already carries, `desktop.settings.bindAddressHelp` was added and `desktop.settings.portHelp` was extended, and **both are refused while any phone is enrolled and the session has accepted an order**, with `desktop.settings.addressLocked`. |
| N5 | The station card's printed QR is dead after an address change, and the recovery text does not mention it | Fixed | Checklist step 9 in both languages now says the cards in the printer lids carry the old address too and to print and tape a fresh card at every station. |
| N6 | `ticket.failed` states a duration that the suspension rule makes false | Fixed | The duration is gone and the cause is named instead: "Der Drucker dort antwortet nicht." / "The printer there is not answering." The minutes stay only on `ticket.failedAfterWaiting`, where they are true. |
| N7 | The suspension arithmetic is left to the implementer | Fixed | 3.2 states the accumulation rule in one sentence, a stopwatch paused and resumed rather than reset or read off the wall clock, and gives the review's worked example including the conclusion that the outer bound dominates a heavily suspended ticket and that this is intended. 11.1's `GiveUpWindow` row tests the alternating case against that example. |
| N8 | `OrderAccepted` and `TicketStatusChanged` carry nothing a station row can be rendered from | Fixed | 6.2 states that the station page refetches `GET /api/station/{accessKey}/tickets` on both events rather than rendering from the payload, with the reason: widening the payload would put a second producer of `canAcknowledge` beside the one 5.6 requires to be the only one. |
| N9 | The 503 and 429 responses have no strings, and the string that will be reused states a falsehood | Fixed | `review.sendFailedDatabase` and `review.tooManyRequests` were added in both languages, and 5.1 names them on the 503 and the 429. 5.1 also says why the 503 does not reuse `review.sendFailed`: the laptop answered and its disk did not. |
| N10 | `TicketResolvedByHuman` and `Confirmed` have no message key, which 11.1 asserts is impossible | Fixed | 2.11 states that eight of the nine `FailureReason` values reach a client and that `TicketResolvedByHuman` is an admin-only diagnostic with no key, because its ticket is `HandledOnPaper` and carries that state's message. 11.1's `messageForTicket` row was scoped to the eight and asserts the ninth has none. |
| N11 | The plural-forms rule is stated inside 8.5 and the strings that need it are mostly in 8.9 | Fixed | The rule moved to 8.1, where it binds every table in section 8, and it names all ten `{count}` keys. A second rule was added: a string carries at most one number needing a plural form, and the two strings carrying a second number are shown to be safe by construction, `admin.overview.stationBlocked` because its `{minutes}` is never below five and `ticket.failedAfterWaiting` because its `{minutes}` is never below twenty. |
| N12 | Cause-first guidance, against 8.1's own rule | Fixed | `catalog.paperWarning` and `catalog.offlineWarning` now lead with "Nehmen Sie weiter Bestellungen auf." / "Keep taking orders." and carry the cause as the trailing clause. `admin.printers.mockFolderUnwritable` leads with the action, as part of B3. |
| N13 | Checklist step 14 tells the volunteer to expect the ordering page, which an unenrolled phone cannot show | Fixed | Step 14 in both languages now says the screen asking for a six digit code is what should appear, and that seeing any page from the program at all is the proof. |
| N14 | Nothing in checklist step 12 changes the transport from `Mock` to `Network` | Fixed | 6.2's `PrinterDiscovered` row states that tapping a discovered printer fills in the host and port **and sets `TransportKind` to `Network`**. `admin.printers.hostHelp` says so, and checklist step 12 in both languages says it, including that a hand-typed address needs the kind set as well. |
| N15 | At most one outstanding invitation is asserted but never enforced | Fixed | 2.8 specifies a partial unique index over the outstanding condition and states that creating an invitation consumes the previous one in the same transaction that inserts, so two admin tabs produce one outstanding row and one loser. 11.2's enrolment row tests the concurrent case. |
| N16 | A revoked device's live SignalR connection is not terminated | Fixed | 6.2 states that the transaction setting `RevokedAtUtc` removes the connection from every group and aborts it, and that the hub refuses it if it reconnects. `DeviceRevoked` is what tells the person, not what enforces the revocation. Tested in 11.2's SignalR row. |
| N17 | `Device.Language` has no stated origin at enrolment | Fixed | 2.8 states the default: German, unless the redeeming browser's `Accept-Language` asks for English first, with the note that the settings sheet is where it changes and that a stored language always beats the header afterwards. It is the same rule as 8.10's opening language. |
| N18 | `Blocked --> Failed` at the give-up window exists in the job machine and not in the ticket machine | Fixed | The arrow was added to 3.2's diagram, conditioned on the cause not being one of the four suspending ones, with prose naming the case it covers: `PrinterError` with zero bytes, which is a mechanical error and an unknown cause. 3.5's mechanical error row was updated to match. |
| N19 | 7.5's release rule names only paper end | Fixed | 7.5 now says that any blocking condition going from set to clear releases the `Blocked` jobs at that printer, names paper end, cover open and the error state as the blocking conditions, and says what naming only paper end would have broken. |
| N20 | An order whose only ticket is `HandledOnPaper` reports itself `Printed` to the server | Fixed | The bucket is kept and the chip is not: 3.1 states that when at least one ticket is `HandledOnPaper` the phone renders the new `orders.status.handledOnPaper` instead of `orders.status.printed`. It is a rendering rule over a status the calculator already produced, so the table stays total. |
| N21 | The revision log records two closures that the current spec has removed, without saying so | Fixed | Recorded in the supersession note below, which belongs with the first review's supersession list. |
| N22 | The desktop `CLAUDE.md` says the settings window holds four settings and nothing else | Owner decision needed | This pass was scoped to `docs/spec.md` and this log, and the fix the review names is one sentence in `desktop/CLAUDE.md`. It became **open question 12**, which quotes the file, gives the case for both buttons on the file's own reasoning, records that it is the only `CLAUDE.md` contradiction across the four files, and leaves the owner to choose between adding the sentence and removing the buttons. |

## Supersession note, which closes N21

This belongs with the first review's "What changed in the spec after these dispositions were written"
list above, and is recorded here so that the earlier dispositions stay a record of what was true when
they were written.

* **Device reassignment was removed by the enrolment rewrite**, which touches B4 above and ranked
  missing piece 6. B4's disposition cites "An admin can reassign a phone to a different person for a
  shift handover (5.5)", and the missing piece cites `PUT /api/admin/devices/{id}` with
  `admin.devices.reassign`. Neither exists. The current 5.5 states the opposite outright: "No endpoint
  creates a person, and no endpoint moves a phone to somebody else." What replaced it is the rule in
  2.8 that a person has at most one working phone and that issuing a new QR code revokes the old one in
  the same transaction, so a phone changes hands by its new carrier setting it up under their own name.
  B4's actual finding, that an `Unknown` question is unanswerable when the placing phone dies, is
  unaffected and remains fixed: orders and questions are scoped by `ServerPersonId`, the
  `person:{serverPersonId}` group still exists, and the admin resolve endpoint still exists. Only the
  reassignment sentence went.

## Two first-review closures were reopened, and both are closed again

The second review found that two of the closures recorded above had been undone by later passes. Both
are named here, because this log is what a future reader checks to see whether a closure was silently
undone.

* **B6 of the first review, the mock reporting every order as printed.** It was closed by the rule that
  a mock station's ticket reaches `PrintedOnTestPrinter` and never `Printed`. Finding B1 of the second
  review showed that 7.6's mapping table, which 11.1 instructs an implementer to build `RetryPolicy`
  from, still mapped every `Confirmed` dispatch to `Printed` with no mention of the transport, so an
  implementer following the document as written would have shipped exactly the defect B6 described.
  **It is closed again**, and this time in the place the implementer reads: the mapping depends on the
  transport kind, it is stated once in 7.6, 7.8 and 3.5 defer to it, and the test row names the mock
  case. A station left on the test printer reaches `PrintedOnTestPrinter`, its order reaches
  `NeedsAttention` by row 2 of 3.1 during a real event, and it shows as needing attention rather than
  as printed.
* **B2 of the first review, the break-glass page dropping a live ticket.** It was closed by restricting
  the listing, then re-closed by moving the restriction onto the button when the owner asked for the
  full listing. Finding B2 of the second review showed that the button's condition, written as a
  disjunction, permitted a `Printing` ticket whenever the printer reported paper end, an open cover or
  an error state, which are exactly the conditions that arise while a job is in flight. **It is closed
  again**, by the boolean expression in 5.6: a `Printing` ticket is excluded whatever the printer
  reports, the prose says so as its own sentence, the endpoint refuses it, 8.10 renders a status in
  place of a button, and both the unit test and the integration test now walk a `Printing` ticket
  through every printer condition in turn. The claim transaction, the 409 and the acknowledge condition
  now say the same thing in the same words.

## Printers became entities, and the test printer stopped being special

The owner asked for a printer feature that supports more devices later, where adding one is a new
driver class and a registration rather than a branch. Three things in the spec changed with it.

* **A printer is an entity now, not a row beside a location.** `PrinterConfiguration` is gone.
  `Printer` is a table per hierarchy holding every supported device, `ProductionLocation.PrinterId` is
  nullable, and several locations may point at one printer. The worker, the process id counter and the
  circuit breaker all key on the printer's id, which replaces the canonical `TransportKind|Host|Port|
  AgentIdentifier` string that existed only because a printer had no identity of its own.
* **`IPrinterTransport` became `IPrinterDriver`, and it describes the model as well as reaching it.**
  Characters per line, the code page, the timeouts and whether the `GS ( H` echo can be trusted are
  facts about a model, so they live in the driver instead of in columns a volunteer types.
  `PrinterDriverRegistry` is the only place a printer's type turns into behaviour.
* **`PrintedOnTestPrinter` is gone, and B6 of the first review is closed a third way.** That state
  existed because a location whose printer was never configured was created on the mock, so the mock
  reporting `Printed` would have hidden a whole evening of orders in a folder. A location is now
  created with no printer at all: nothing prints there, the overview names the location, and its own
  screen tells the people standing at it to work the orders off the phone. The trap is closed at the
  source rather than by a ticket state, the test printer is one supported printer among others, and
  nothing above `IPrinterDriver` knows which driver wrote a slip.
