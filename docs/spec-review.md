# Adversarial review of `docs/spec.md`

This review was written against an earlier version of the specification, one built around thermal
printers and printed slips. The fire department has since chosen tablets, printing has been removed
from the product entirely, and `docs/spec.md` has been rewritten around the station tablet. Read this
file as a record of what was reviewed and decided at the time. Every finding about printers, print
jobs, slips or paper describes a mechanism that no longer exists, and nothing here should be treated
as a description of the product today.

Reviewer: an agent that did not write the spec. Scope: correctness, completeness, compliance with the
root, backend and frontend `CLAUDE.md` files and the `writing-ui-guidance` skill, plus a challenge to
each design decision the author made on their own authority.

## Verdict

This is a strong specification and it is worth the owner's time, but it should get one revision pass
before he acts on it. The domain model, the hardware reasoning, the localization discipline and the
prose quality are all above the bar. The defects are concentrated in exactly the places the document
is least likely to be reread: the joins between the three state machines, the paths that run after
something has already gone wrong, and the second evening rather than the first. Fifteen of the
findings below can produce the one outcome the product exists to prevent, a silently dropped order,
and one of them (the counter scope key) means no order can be accepted at all. None of them require
a redesign. All of them are cheaper to fix in the document than in code, which is the argument for
one more pass rather than starting implementation now.

Counts: 15 blocking, 24 non-blocking.

---

# Blocking findings

## B1. The per-location counter scope key does not fit its own column, so no order can ever be accepted

Section 2.14, line 620: `| Scope | string(80), primary key |`.
Section 4.1, line 800: `` `session:{eventSessionId}:location:{productionLocationId}` ``.

With real GUID values that key is `session:` (8) plus 36, plus `:location:` (10) plus 36, which is 90
characters. The column is declared as 80. Section 2.1 says `string(n)` "means a maximum length
enforced both in the database schema and in validation", so this is not a soft limit.

What breaks: the very first order of the very first event fails inside the acceptance transaction,
which per section 4.2 rolls back the whole thing. The phone retries with the same `clientOrderId` for
ten minutes and then tells the server to write the order on paper. Every phone does this. The system
is dead on arrival at the first festival and the failure looks like a WiFi problem to everyone
present.

Fix: `string(120)`, or drop the GUID text form and make the counter a composite key of
`(EventSessionId, ProductionLocationId?)`, which also removes the string parsing nobody wants. The
composite key is the better answer because it lets the database enforce the relationship instead of
a formatting convention.

## B2. The break-glass page can suppress a slip while the printer is working perfectly

Section 5.6, line 1219: the tickets endpoint returns "that location's tickets that are not yet
`Printed` or `HandledOnPaper`". That set includes every `Queued` ticket, which means every order,
from the instant it is accepted until it comes off the printer. Line 1220: acknowledge "Moves the
ticket to `HandledOnPaper`" with no stated precondition. Section 3.2, line 705: `HandledOnPaper`
means "The slip will not be chased any further."

This contradicts section 8.11 line 1953, whose empty state reads "Es liegen keine offenen
Bestellungen an. Alle Bons sind gedruckt." That string is only true if the endpoint returns failed
tickets, not queued ones.

What breaks, concretely: at 20:30 a kitchen helper is given the break-glass link for an unrelated
reason and leaves the page open. Rows appear for every order as it arrives, because a live printer
still leaves a ticket `Queued` for a second or two and a busy one for longer. The helper taps
"Übernommen" on the top row to tidy the screen. That ticket is now `HandledOnPaper`, no slip is ever
printed, sequence number 042 never appears in the pile, and the placing server's phone shows
`ticket.handledOnPaper`, which reads as a normal, successful outcome. The order is silently dropped
and the gap in the pile has no explanation. The same affordance also lets the break-glass page drift
into being the kitchen display system that section 1.5 explicitly refuses.

Fix, server side and not by wording: the tickets endpoint returns only tickets in `Failed`,
`Unknown` or `Blocked`, plus `Queued` tickets whose printer has been offline for longer than the
give-up window. The acknowledge endpoint refuses with a stated reason (409) unless the ticket is in
one of those states. Then section 8.11's "Es liegen keine offenen Bestellungen an" becomes true, and
the page is genuinely empty in normal operation, which is the only thing that keeps it out of the
normal workflow. This is `writing-ui-guidance` rule 1 applied to the product's hardest constraint:
right now that constraint is defended only by the prose in `station.warningBody`.

## B3. Tickets can park forever with no escalation, and the phone keeps saying "Wird gedruckt"

Two separate paths reach the same permanent state.

Section 3.5, line 778: "Location is disabled in configuration | 0 | Held | `Queued` | That the
station is switched off and the slip is waiting". "Held" means the give-up window does not run. There
is no timeout, no upper bound and no admin alert. If the admin disables a printer at 21:00 because it
is jammed and then forgets it (which is what happens at 21:00), every order routed there sits in
`Queued` for the rest of the evening. The order never reaches a final state, never appears in the
"orders that need checking" filter (its status is not `NeedsAttention`), and the placing server's
phone shows `orders.status.printing`, "Wird gedruckt", indefinitely. That is a silently dropped order
that the system actively reports as in progress.

Section 7.3, line 1383: on startup the worker "enqueues every ticket of the current session that is
`Queued` or `Blocked`". Tickets of any previous session are never enqueued. Combine with section
5.5 line 1204, starting a new event session, and section 6.2 line 1262, which tells phones to clear
their order lists on `EventSessionStarted`: a ticket that was parked behind a paper-out when the
admin starts the next evening's session is now invisible to the worker, invisible to the phone, and
still `Queued` in the database forever.

Fix: give parked tickets a bounded life. A ticket held by a disabled printer escalates to
`NeedsAttention` after the give-up window with its own message ("Die Station ist ausgeschaltet, der
Bon wartet seit 12 Minuten"), and the admin overview counts held tickets as a readiness problem.
Starting a new session must refuse while any ticket is non-final, or must resolve those tickets
explicitly and say so on screen.

## B4. The `Unknown` question is unanswerable in several reachable cases

Section 3.4, line 755, promises two answering surfaces: "The question is asked on the phone of the
server who placed the order ... It is also shown in the admin list on the laptop." Section 7.6, line
1445, repeats it: "through `POST /api/orders/{id}/tickets/{ticketId}/resolve` or the equivalent
action in the admin list."

Section 5.5 contains no such admin endpoint. The only resolve endpoint (line 1079) is device auth.
Section 3.4 line 764 states the rule that makes this fatal: "`Unknown` never resolves itself by
timing out."

Three reachable scenarios, all of which end with a permanently unanswered question and an order stuck
in `NeedsAttention`:

1. The placing server's phone battery dies at 21:00. Nobody else can answer. The slip may or may not
   be on the pile and no human will ever be asked.
2. The admin revokes that device (a phone is lost, or the wrong row is tapped). Section 6.2 line 1260
   sends `DeviceRevoked` and the phone clears its token. The question is gone from the only surface
   that had it.
3. The phone is revoked and re-enrolled, which is the obvious volunteer response to any phone
   misbehaving. Re-enrolment creates a new `Device` row. Section 5.4 line 1076: `GET
   /api/orders/{orderId}` returns "404 if the order belongs to another device". `GET
   /api/orders/mine` returns that device's own orders. The re-enrolled phone therefore cannot see, let
   alone resolve, any order the same human placed ten minutes earlier on the same handset.

Fix: two changes. Add `POST /api/admin/orders/{id}/tickets/{ticketId}/resolve` with the strings for
the admin order list, so the laptop is a real second surface rather than a promise. And scope a
server's own orders by `ServerPersonId` rather than `DeviceId`, so re-enrolling a phone does not
orphan the evening's history. The break-glass page must not be the fallback here, because per B2 it
should not be reachable while the printer is alive.

## B5. Voiding an order does not cancel its print jobs, so a cancelled order can still be produced

Section 3.1, line 670: "**Voiding** is admin only ... and only while every ticket of the order is
still `Queued` or `Failed` with nothing printed." Section 5.5, line 1206: `POST
/api/admin/orders/{id}/void`.

Nothing anywhere says what happens to the `LocationTicket` rows or to their queued `PrintJob` rows.
The ticket state machine in section 3.2 has no `Voided` or `Cancelled` state at all, and the print job
machine in section 3.3 has no transition that produces `Cancelled`, even though `Cancelled` is a
declared `FailureReason` (line 563), a declared attempt `Outcome` (line 572), a declared
`PrintDispatchOutcome` (line 1353) and a row in the retry table (line 1437). It is a state with no
trigger.

What breaks: at 20:40 the kitchen printer runs out of paper. Order 137's ticket goes `Blocked` and
parks. The guest changes their mind and the admin voids order 137 on the laptop, which succeeds
because nothing has printed. At 20:47 someone loads a new paper roll. Section 7.5 line 1421: "paper
end clearing releases `Blocked` jobs at that printer." Slip 042 for the voided order 137 prints and
lands on the pile. The kitchen produces it. The system has now produced food for an order that the
admin was told, on screen, was cancelled.

Fix: define voiding as a transaction that moves every ticket of the order to a terminal `Voided`
state and every non-final print job to `Cancelled` with `FailureReason: Cancelled`, and have the
worker skip a job whose ticket is no longer live. Add `Voided` to the ticket state machine and to the
order status mapping. That also gives the `Cancelled` outcome its missing producer.

## B6. Every station is created pointing at a printer that silently swallows its orders

Section 2.5, line 360: "Every active location has exactly one `PrinterConfiguration` row". Section
2.13, line 590: `TransportKind` default `Mock`. Section 5.5, line 1133: creating a location also
creates a printer configuration "with `TransportKind: "Mock"`". Section 7.8, line 1564: the mock with
no fault returns `Confirmed`, which per the same table produces ticket state `Printed`.

So a station whose printer was never configured is not an error state. It is a station that reports
every order as successfully printed, to the phone, to the admin list, and to the order status
projection, while rendering the slips into a browser tab on the laptop that nobody has open.

What breaks: the checklist itself sets this trap. Step 7 (line 2121) says "Leave every station on the
test printer for now". Step 14 says to enter each address. If the volunteer configures Kitchen and
Bar indoor and misses Bar marquee, the marquee bar receives nothing all evening. Every server's phone
says "Gedruckt" for every drink. The admin's attention filter is empty. Nobody discovers it until a
guest complains, and even then there is no signal pointing at the cause. The admin overview does have
`admin.overview.missingPrinter` (line 1848), but it is worded as one readiness item among several and
it is not a gate.

Fix, in code rather than in prose: starting an event session refuses, or requires an explicit typed
confirmation, while any active location is on `Mock`. During an active session with a `Mock` station,
the ticket does not reach `Printed`: it reaches a distinct state that the admin overview and the
phones both surface. The mock is a first-class product feature (backend `CLAUDE.md` rule 10), which
is precisely why it must be impossible to mistake for a printer.

## B7. The spec contradicts itself on whether revoking a device destroys its unsent orders

Section 6.2, line 1260, `DeviceRevoked`: "The phone clears its token **and its queue** from
`localStorage`".
Section 9.4, line 2072: "The app clears the token, **keeps the pending queue** in `localStorage` so
nothing is thrown away".

These cannot both be implemented. The difference is whether unsent orders are silently destroyed.

What breaks: at 20:15 the admin means to revoke the phone on the row above and taps the wrong one.
That phone has two orders queued through a WiFi dead spot at the far end of the marquee. Under the
6.2 wording they are erased with no record, no message and nobody aware that two tables are waiting
for food that was never ordered. Under the 9.4 wording they survive and are resubmitted after
re-enrolment, which is the correct behaviour and matches the root rule that a silently dropped order
is the worst outcome in the system.

Fix: adopt 9.4 and delete the phrase from 6.2. Add the string the 9.4 path needs, which does not
exist yet: the enrolment screen must say that unsent orders are being held and will be sent once the
phone is set up again. Also state what happens if the phone is never re-enrolled, because those
orders then exist only in one browser's storage.

## B8. The offline queue is declared unrecoverable when it is not, and a given-up entry can stall every later order

Two defects in one mechanism.

First, section 9.1, line 2006: the queue "does not survive the tab being closed, the browser being
killed, or the phone rebooting." That is false for `localStorage`, which survives all three. What
does not survive is the retry loop, because there is no service worker. The spec conflates "cannot
retry in the background" with "the data is gone", and the whole product design contradicts it: the
device token lives in the same `localStorage` and is explicitly expected to survive a closed tab for
the entire evening (section 2.9). If the token survives, the queue survives.

This matters because the false claim is baked into the UI. `order.giveUp` (line 1830) tells the
server "Schreiben Sie diese Bestellung auf Papier". If the app simply reads `pendingOrders` on boot
and resumes the queue, a server whose tab was evicted by iOS under memory pressure reopens the page,
the order goes through, and no paper is needed. That is a recoverable order currently being declared
lost, for free.

Second, section 9.3, line 2039: after ten minutes the entry's retrying is "Stopped", and line 2041:
"Retries happen in order, oldest first, one at a time." If a stopped entry stays at the head of the
queue, every later order queues behind an entry that will never be retried.

What breaks: a server takes an order at the far picnic tables where there is no signal. It gives up
at minute ten. They walk back into coverage and take four more orders. Nothing sends, because the
head of the queue is stopped and the loop is strictly oldest first. The header shows "5 Bestellungen
warten auf die Verbindung", which is true and useless, and five tables get nothing.

Fix: resume the queue from `localStorage` on page load and correct section 9.1 to say what is
actually true (the queue survives, the automatic retrying does not resume until the page is open
again). Explicitly specify that a stopped entry is skipped, not blocking, and that the queue keeps
retrying every 60 seconds after minute ten instead of stopping, while still showing the paper
instruction at ten minutes. The instruction is what a human needs; stopping the machine adds nothing.

## B9. The order status projection contradicts the ticket machine, and its mapping is never given

Section 3, line 626: "The order state is a projection of its tickets." Section 3.1, line 659:
`Order.Status` "is computed by `OrderStatusCalculator` from the ticket statuses". Section 11.1, line
2234, requires a test proving "Every combination of ticket states maps to exactly one order state,
with an exhaustive test over the state matrix".

That matrix is never given anywhere in the document, and the arrows in the section 3.1 diagram are
demonstrably not it:

* `Printed --> [*]` (line 645) is terminal, but section 3.2 line 692 has `Printed --> Queued : a
  human asked for a reprint`, and section 5.4 line 1091 allows a reprint from `Printed`. So a server
  taps "Erneut drucken" on a printed order and the order projection has no legal transition. The
  stored `Order.Status` says `Printed` while a ticket says `Queued`, which is the exact disagreement
  section 3 promises cannot happen. If that reprint then goes `Unknown`, there is no
  `Printed --> NeedsAttention` arrow either, so the phone shows "Gedruckt" for an order with an
  unanswered question on it.
* `Accepted --> Voided` is the only path into `Voided`, but section 3.1 line 670 explicitly permits
  voiding when tickets are `Failed`, and a `Failed` ticket puts the order in `NeedsAttention`. There
  is no `NeedsAttention --> Voided` arrow.
* `NeedsAttention --> Printed` requires "the last open ticket was resolved as printed". A ticket that
  goes `Failed --> HandledOnPaper` (line 690) is resolved but not printed, so the order stays in
  `NeedsAttention` permanently. The break-glass acknowledgement leaves the placing server looking at
  "Bitte prüfen" for the rest of the evening on an order that was handled correctly.
* `Blocked` has no order-level representation at all. An order whose only ticket is `Blocked` shows
  "Wird gedruckt".
* `Accepted --> NeedsAttention : every printer for this order is unreachable` uses "every" where the
  `Printing` arrow uses "any", which are different rules for the same situation depending on whether
  an unrelated ticket happened to be claimed first.

Fix: delete the trigger prose from the arrows and specify the calculator as a table with one row per
ticket-state multiset, priority ordered, for example: any ticket `Unknown` or `Failed` gives
`NeedsAttention`; else any `Voided` and all `Voided` gives `Voided`; else all in
{`Printed`, `HandledOnPaper`} gives `Printed`; else any `Printing` gives `Printing`; else `Accepted`.
That table is the thing section 11.1 wants to test, and it cannot be written from the current
document.

## B10. A change of the laptop's IP strands every enrolled phone, including its unsent orders

Root `CLAUDE.md` lines 50 to 52: "The laptop's IP address is not stable and cannot be made stable
without admin rights. **The QR code carries the full URL including the current IP** ... Never
introduce a flow that depends on a phone remembering an address."

The spec inherits this and never addresses what an enrolled phone actually is: a browser tab sitting
on the origin `http://192.168.1.23:5000`. Section 5.5 line 1176 only handles building the QR URL at
enrolment time. Two consequences the document never states:

1. `localStorage` is scoped to that origin. If the router reboots and DHCP hands the laptop `.27`,
   every phone's saved token is unreachable from the new address, so the entire crew must re-enrol
   mid-service, and any queued unsent order is stranded in storage for an origin nobody will visit
   again. This is the "remembered address" failure the root rules forbid, arrived at from the other
   direction.
2. The break-glass URLs and the printed address in `admin.overview.address` also die.

What breaks: 21:10, the router is power cycled because the WiFi "feels slow". The laptop comes back
on a new address. Eight phones show `enrol.error.noConnection`, which tells them to check they are on
the festival WiFi, which they are. Nothing in the product diagnoses the real cause and the checklist
has no step for it.

Fix: make address stability a mandatory checklist step rather than an accepted risk, with the two
options a volunteer can actually perform (a DHCP reservation in the router, or a static address on
the laptop's WiFi adapter, both with a screenshot in the documentation). Add a recovery path: the
admin enrolment screen already exists, so the recovery is "rescan the QR", but the spec must say that
queued orders on the old origin are unreachable, and preferably show the previous address alongside
the current one on the overview so the person at the laptop can recognise what happened. Also
consider binding the phones to a hostname the laptop advertises, which survives an address change.

## B11. An item can become unroutable, and the acceptance path has no defined answer

Section 2.7, line 400, gives "the only routing rule in the system", and line 403: "If zone filtering
leaves nothing, fall back to the item's assignments in any zone, lowest `Priority` first."

The fallback does not repeat the `IsActive` filter that the primary rule applies. Two defects follow.

First, the fallback as written can select a deactivated location, which has a stopped worker (section
7.3) and, per 2.5, may not even be a legal ticket target.

Second, and worse, if every assigned location is inactive there is no candidate at all. Section 2.6's
invariant ("An active item must have at least one `ItemLocationAssignment`") is enforced when saving
the item, not when deactivating a location, and section 5.5 line 1135 only blocks deactivation when
"tickets are open". Meanwhile `OrderLine.LocationTicketId` is non-nullable (section 2.10) and an order
must have at least one line, so acceptance cannot complete. The status table for `POST /api/orders`
(line 1030) has no code for this, and section 3.1 line 665 insists "An order is never rejected because
of a printer."

What breaks: at 21:00 the marquee bar's printer dies and the admin deactivates the station to stop
the noise. Radler was assigned only to that station. The next Radler order produces an unhandled
failure. The server sees a generic error, the phone retries the same `clientOrderId` every ten
seconds for ten minutes, and then tells them to write it on paper. Every Radler order for the rest of
the evening does the same.

Fix: apply the `IsActive` filter in the fallback, and define the zero-candidate case explicitly. The
honest answer, consistent with "never reject an order", is to route the line to a designated fallback
station (the site's default, chosen by the admin) and mark the ticket so the slip says the item was
routed there because its own station is switched off. The alternative, blocking deactivation while
any active item routes only there, is simpler but will be fought at 21:00 by a volunteer who needs
the station gone.

## B12. The central retry rule contradicts its own table, one line below itself

Section 7.6, line 1425: "**a job is retried automatically if and only if zero bytes reached the
printer.**"
Section 7.6, line 1433: "| `SocketDropped` | any | **never** | `Unknown` | `Unknown` |".

"any" includes zero. The two statements are incompatible, and section 3.2 line 682 and section 3.5
line 772 both take the other side ("attempt failed before any byte was written" returns to `Queued`,
"Write failed before the first byte | 0 | Yes").

Which one is implemented decides real behaviour. A socket that dies during the connect handshake or
before the first write is a common event on flaky festival WiFi. Under the table row, that becomes an
`Unknown`, which per section 3.4 can only be cleared by a human walking to the kitchen and reading the
top of the pile. Half a dozen of those in an evening and the servers learn to answer "Der Bon liegt
da" without walking, which destroys the mechanism for the case where it matters.

Fix: the headline rule wins. Change the row to "`SocketDropped` | 0 | yes, with backoff" and
"`SocketDropped` | > 0 | never". While fixing it, define `BytesWritten` precisely: it is bytes handed
to the socket, and any value above zero is treated as possibly delivered. That definition is
conservative in the safe direction and should be stated, because a reader can reasonably assume it
means bytes acknowledged by the printer, which nothing can know.

## B13. Enrolment lockout is global, trivially triggered by a stranger, self-inflicted by a crew, and has no unlock

Section 2.9, line 482: "Ten failed redemption attempts within five minutes invalidate every
outstanding code and tell the admin to open the screen again." Rate limit (line 867): 20 per minute
per IP.

Three problems compound:

1. The lockout is global, not per source. Anyone on the open festival WiFi, including a guest's phone
   or a bored teenager, can POST ten wrong codes in thirty seconds and lock enrolment for everyone,
   then do it again the moment it is reopened. Plain HTTP on an open network is an accepted premise
   of this product, so this is not a theoretical attacker.
2. It is likely to be self-inflicted at the exact moment the feature exists for. Section 2.9 line 477
   describes the design goal: "a whole crew can enrol during one briefing". Eight people scan the
   same displayed QR within a few seconds. The first consumes the code atomically; the others get
   410. If a 410 counts toward the ten failures (the spec does not say), the crew locks itself out
   halfway through its own briefing.
3. `enrol.error.locked` (line 1644) promises "Die Person am Laptop kann sie wieder freigeben", and
   section 5.5 has no unlock endpoint. `invalidate-all` is the opposite operation.

Fix: count failures per source IP and lock only that source. Never invalidate codes that were already
issued to other people, because that is what turns one person's mistyping into everyone's problem.
State explicitly that a 410 on an already-consumed code is a race, not a failed attempt, and does not
count. Add the unlock endpoint the string promises, or change the string.

Related, same paragraph: `enrol.error.codeUsed` (line 1643) tells the reader "Bitten Sie die Person am
Laptop um einen neuen QR-Code." During a briefing that is the wrong next step, because the screen is
already showing a fresh code and rotating every thirty seconds. The right instruction is "Scannen Sie
den QR-Code noch einmal." Instruction first, and it must be the instruction that works.

## B14. The print queue is unbounded and there is no circuit breaker at a station that fails slowly

Section 7.3 describes a `Channel<Guid>` per printer with a single consumer. Nothing bounds its depth,
and nothing reacts to a run of bad outcomes. Section 7.4 step 7 waits up to 90 seconds per job for the
process id echo.

What breaks: a printer answers TCP but stops echoing (a jam, a firmware wedge, a cable half out).
Every job takes the full 90 seconds and then produces `Unknown`. Orders arrive at three per minute.
After ten minutes the channel holds roughly 27 jobs, the newest of which will not be attempted for
another 40 minutes, and the system has generated seven separate "walk to the kitchen and check the
pile for number NNN" questions on four different phones, each about a slip that was queued long
enough ago that the server has forgotten the table. Nothing has told anyone that the station itself
is the problem. The 5 minute give-up window does not help, because section 3.2 defines it as "5
minutes of continuous failure for a single ticket" and a ticket sitting in the channel is not
failing; the spec never says whether the window runs while a ticket waits its turn.

Fix: three additions. State that the give-up window starts at ticket creation, not at first attempt.
Add a station circuit breaker: after two consecutive `Unknown` or `TimedOut` outcomes, the worker
stops attempting, marks the station as faulty, pushes `PrinterStatusChanged`, and moves the remaining
queue to a state that surfaces on the phones and in the admin overview at once, rather than one
ticket at a time over the next hour. And state a queue depth beyond which the station is declared
faulty regardless of outcome, because a depth of ten at a station that normally clears in two seconds
is already a diagnosis.

## B15. The phone labels two different numbers "Nr.", and the `Unknown` question names the other one

Section 4.4 (lines 847 to 850) defines two numbers with two jobs and says the sequence number is the
loss detection mechanism. On the phone they are presented like this:

* `orders.row` (line 1799): "Nr. {number}, {table}", where `number` is the **global order number**.
* `orders.ticket` (line 1805): "{station}, Bon Nr. {sequence}", the **sequence number**.
* `orders.detailTitle` (line 1806): "Bestellung {number}", the global number again, now under a third
  label.
* Section 4.4 line 848: the phone renders the global number as `#137`, a fourth form.
* `ticket.unknown.action` (line 1815): "Schauen Sie am Stapel bei {station} nach Nummer {sequence}".

So the row the server tapped says "Nr. 137", and the question inside it says to go and look for
"Nummer 042", and the slip they are looking for says `NR. 042` while also carrying "Bestellung 137".
Root `CLAUDE.md` rule 9 and the spec's own rule at line 1605 ("One term per concept per language")
are both violated, and the cost lands on the one interaction the whole safety net depends on.

What breaks: a server at 22:00, in the dark, in a loud marquee, walks to the kitchen looking for
"137" in a pile whose slips are printed with 042 in double height, concludes the slip is missing, and
answers "Der Bon fehlt". A reprint is produced and the kitchen makes the order twice, which is the
second worst outcome in the system, produced by a label.

Fix: one word per number, everywhere, on screen and on paper. Global order number is always
"Bestellung 137" / "Order 137" with no `#` and no "Nr."; the per-location sequence is always "Bon 042"
/ "Slip 042", including on the slip header (`BON 042` instead of `NR. 042`, and `SLIP 042` instead of
`NO. 042`, which is not idiomatic English anyway). Then `ticket.unknown.action` reads "Schauen Sie am
Stapel bei {station} nach Bon 042", and it names exactly the thing printed in double height at the top
of the paper.

---

# Non-blocking findings

## N1. `SixDigitDisplay` is a persisted column and is described as never persisted

ERD line 210 lists `string SixDigitDisplay "shown on laptop only"` as an `EnrolmentCode` field.
Section 2.9 line 469 says it is "Kept in plaintext only in memory for the admin screen and never
persisted". If the ERD wins, the six digit code sits in plaintext in the database beside its own
PBKDF2 hash, which makes the hashing decorative. Fix: remove it from the ERD and hold it in the
rotation service's in-memory state, or accept the column and delete the hashing for it. The first is
right.

## N2. A test print has no representation in the model

Section 5.5 line 1187 offers `POST /api/admin/printers/{locationId}/test-print`, and section 7.3 line
1378 says "Nothing else in the process opens a socket to a printer. This is what keeps the 'one
connection at a time' rule true even while the admin runs a test print." So the test print must go
through the worker, which consumes `PrintJob` ids, and `PrintJob.LocationTicketId` is non-nullable
with `Kind` restricted to `Initial` or `Reprint`. There is no legal way to express a test print. Fix:
add `Kind: Test` and make `LocationTicketId` nullable, or introduce a separate work item type the
channel carries. This is a schema decision, so it needs settling before the first migration is
written.

## N3. The process id counter has no home and section 4.1 denies it exists

Section 4.1 line 813: "Nothing else in the system allocates a number. Print jobs do not". Section 7.4
step 5 (line 1400): "Allocate a process id. A per-printer counter cycling 1 to 9999, persisted on the
job." The value is persisted on the job; the counter is not stated to be persisted anywhere. If it is
in memory, it restarts at 1 after a crash, which matters because the process id is the only thing that
turns a job into `Confirmed`. A stale echo matching a reused id would produce a false `Confirmed`,
which converts a genuinely lost slip into a reported success, worse than an `Unknown`. Fix: state
where the counter lives (a `NumberCounter` row per printer is the obvious answer, which also removes
the contradiction), and state that echoes are only accepted on the socket that sent the job.

## N4. The admin UI is unreachable from the address the admin overview tells the volunteer to use

Section 5.1 line 863 restricts admin paths to loopback and returns 404 elsewhere.
`admin.overview.address` (line 1851) displays "Die Telefone erreichen den Laptop unter {url}", for
example `http://192.168.1.23:5000`. That is the URL the volunteer will read, remember and type,
including into the laptop's own browser, where it is not loopback. They get 404 and conclude the
program is broken. Fix: keep the API loopback-only, but serve the admin *page* from any address with
a plain explanation ("Die Verwaltung lässt sich nur direkt am Laptop öffnen. Rufen Sie dort
http://localhost:5000/admin auf."), and treat the machine's own non-loopback addresses as loopback if
that is easier. Also note that the admin SPA shell is currently served to phones (only `/api/admin`
is restricted), so a curious server sees a broken admin screen with no explanation.

## N5. Two localization mechanisms produce the same message

Section 5.1 line 870 renders the message server side, translated per `Accept-Language`. Section 6.2
line 1254 sends `messageKey` for the client to render. The same ticket failure therefore has a
server-rendered wording and a client-rendered wording, which will drift, and root `CLAUDE.md` rule 6
forbids a value being produced in two places. There is a second bug inside this: the device has a
stored language (`PUT /api/session/language`, line 949) which `Accept-Language` ignores, so a server
who chose German on an English phone gets English error messages. Fix: pick one direction. Sending a
key plus parameters and rendering on the client is the better one for the phone (it also honours the
device's stored choice for free); the slip stays server-rendered from resx per backend rule 2.

## N6. The German drifts between "einrichten", "anmelden" and "abmelden" for one concept

`enrol.title` "Dieses Telefon einrichten" / "Set up this phone", `admin.enrol.title` "Telefon
anmelden" / "Add a server phone", `admin.devices.title` "Angemeldete Telefone" / "Phones that are set
up", `admin.devices.revoke` "Telefon abmelden" / "Sign this phone out", `admin.overview.ready` "Sie
können Telefone anmelden" / "You can add server phones", `admin.enrol.enrolled` "Angemeldet: {names}"
/ "Set up so far: {names}". English drifts too: "set up" throughout, then "sign out" for the
opposite, which implies a sign-in that does not exist in a product with no accounts. Fix: German
"einrichten" and "Einrichtung entfernen" (or "sperren"); English "set up" and "remove". One pair each.

## N7. "Leave this page open" is stated three times at three strengths

`header.queuedOne` and `header.queuedMany` ("Lassen Sie diese Seite offen."), `order.notSent`
("Lassen Sie diese Seite offen, bis sie gesendet ist."), plus the `beforeunload` dialog, plus the
narrative in 9.1. `writing-ui-guidance` rule 2 forbids restating one rule at different strengths, and
rule 6 caps what a user retains at about four items. Fix: keep it in exactly one place, the header,
where it is visible at the moment it binds, and delete it from `order.notSent`, which then reads
"Diese Bestellung ist noch nicht beim Laptop angekommen." Note that if B8 is fixed and the queue
resumes on load, the rule becomes an optimisation rather than a hard constraint, and it should be
softened accordingly.

## N8. A price change between the phone's catalog fetch and acceptance is invisible to everyone

Section 5.3 line 981: the phone caches the catalog. Section 2.10 line 501: `TotalCents` is stored, and
line 522 asserts it "equals the recomputed sum". The recomputation happens on the backend from current
prices, and the request body (line 993) carries no price and no expected total, so that assertion can
never fail and cannot detect divergence. Meanwhile the server already read the old total out loud and
took the cash. Fix: send `expectedTotalCents` with the order, and if it differs, accept the order (it
must never be rejected) and return the difference so the phone can show one line: "Der Preis hat sich
geändert. Neue Summe: 11,00 €." One string, and it protects the only number a guest hears.

## N9. "Positionen" counts two different things

`catalog.basketSummary` (line 1740) is "{count} Positionen, {total}" while the slip footer (line 1500)
prints "Positionen gesamt: 6" for an order of three lines with quantities 2, 1 and 3, so the slip means
units. Section 8.7 says the basket bar shows "the number of items" without saying which. Fix: decide,
and use two words. "3 Positionen" for lines, "6 Artikel" for units. The slip footer wants units, so
"Artikel gesamt: 6".

## N10. `IsPaperNearEnd` is captured, pushed and never shown

It is a column (line 605), a field on the status snapshot (line 1332), and part of the
`/api/printers/status` payload (line 1107) and the `PrinterStatusChanged` event (line 1256). No string
anywhere uses it: `admin.printers.*` has `paperEnd` but no `nearEnd`, and the phone banners cover
paper out and offline only. Paper running out mid-job is the single most common way this system
produces an `Unknown`, and near-end is the warning that prevents it. Fix: one admin string and one
overview row ("Die Papierrolle bei {name} geht zu Ende. Legen Sie eine neue Rolle bereit."). Do not
put it on the phones; the person who can act is at the laptop or at the station.

## N11. A `Blocked` ticket can be overtaken, producing a transient gap in the pile

Section 7.5 line 1421: paper end clearing "releases `Blocked` jobs at that printer". Nothing states
that the release preserves ticket order, or that a later-numbered ticket cannot be printed before an
earlier blocked one. Section 4.2 rests the entire loss detection mechanism on gaps meaning exactly one
thing. A pile that reads 41, 43, 44, then 42 a minute later trains the station to wait and see, which
is the same behaviour as ignoring gaps. Fix: state that a printer's jobs are attempted strictly in
`LocationSequenceNumber` order and that a blocked job holds the station rather than being overtaken.
That is also the physically correct behaviour, since a paper-out condition blocks everything anyway.

## N12. The documented backup can be incomplete

Checklist step 21 (lines 2153 and 2209) says copying `gastronomy.db` is the whole backup. If EF Core
runs SQLite in WAL mode (which is the sensible default for this workload), the recent transactions
live in `gastronomy.db-wal` and copying only the main file loses them, silently. Fix: either state
journal mode explicitly and keep it non-WAL, or tell the checklist to copy all three files, or better,
add an admin button that performs `VACUUM INTO` a dated file and tells the volunteer to copy that one.
The button is the version a volunteer can actually get right, and it answers open question 6 at the
same time.

## N13. SQLite contention, disk full and file permissions are never mentioned

The acceptance path uses `BEGIN IMMEDIATE` (line 805). Nothing states a busy timeout, so the
integration test promised at line 2260 ("no gaps under 200 concurrent submissions") will be flaky, and
a real `SQLITE_BUSY` produces an undefined API response. Disk full and a read-only program directory
(a volunteer who copies the executable into `Program Files` will hit exactly this on Windows) are not
covered anywhere, and root rule 2 requires every failure to have a stated user-facing outcome. Fix:
state a busy timeout, state that a database write failure returns a 503 with a user-worded message,
and have startup verify the database path is writable and say so plainly if it is not.

## N14. `beforeunload` is not a reliable backstop on the phones this product targets

Section 8.6 line 1702 leans on `beforeunload` as the mechanical backup for the "leave this page open"
rule. iOS Safari does not reliably fire it on tab close, and mobile browsers evict background tabs
under memory pressure without any dialog at all. Fix: keep it (it costs nothing where it works), stop
describing it as the control that makes the rule safe, and implement the load-time queue recovery from
B8, which is the control that actually works.

## N15. Nothing forbids the camera API, and a future developer will reach for it

The QR flow works because the phone's own camera app opens the URL. An in-page scanner would need
`getUserMedia`, which requires a secure context and cannot exist here. Frontend `CLAUDE.md` line 59
states the general rule, but the spec never names the camera specifically, and "scan a QR code" reads
like an invitation. Fix: one line in section 8.3 saying the scan happens in the phone's camera app and
that the web app never accesses the camera.

## N16. A device language endpoint exists with no screen and no strings

`PUT /api/session/language` (line 949) is defined; section 8 has no language screen, no key, and no
mention of how a server chooses English. Root rule 8 requires both languages for anything user visible.
Fix: either add the control (a two-item toggle in the zone screen is enough) with its strings, or drop
the endpoint and take the language from `Accept-Language` alone.

## N17. `ItemLocationAssignment.Priority` has no UI

The routing rule's tie-break input (line 395) is settable through `PUT
/api/admin/items/{id}/assignments` but section 8.10's assignment screen only describes ticking boxes
("Kreuzen Sie an, wo ein Artikel zubereitet werden kann") and has no string for priority. Two stations
of the same zone that can both produce an item then tie, and the outcome falls through to `SortOrder`,
which is not what the admin thinks they configured. Fix: either surface priority (a drag order per
item) or delete the field and document `SortOrder` as the tie-break, which is simpler and probably
right for fewer than ten stations.

## N18. The CSV import and export formats are undefined

`POST /api/admin/catalog/import` (line 1148) promises "422 with row numbers and reasons" and
`GET /api/admin/export/orders.csv` (line 1208) is "for the treasurer". Neither has a column list, a
delimiter, an encoding or a decimal convention. German Excel defaults to semicolons and comma
decimals, and a UTF-8 file without a BOM opens with broken umlauts, which is precisely the audience
here. Fix: specify both formats, including semicolon delimiter, UTF-8 with BOM, and prices as plain
integers in cents in the import (a volunteer typing 3,50 into a spreadsheet is the normal case, so
accept both and say so).

## N19. `POST .../resolve` is not scoped to the placing device

Section 5.4 line 1079 says only "Device auth". Any enrolled phone with a ticket id can therefore answer
another server's `Unknown` question. Given B4's fix (visibility by `ServerPersonId`), this needs an
explicit rule: who may answer, and what the 409 means. The current 409 message ("Jemand anderes hat die
Frage bereits beantwortet") implies more than one person is expected to be able to.

## N20. German naturalness, three strings

* `enrol.success` (line 1646): "Sie sind eingerichtet." reads as though the person was configured. The
  English says "Your phone is ready", which is the right sentence. Use "Das Telefon ist eingerichtet."
* `review.tableHelp` (line 1772): "damit die Bedienung das Tablett wiederfindet" speaks about the
  reader in the third person, since the reader is the Bedienung. Use "damit das Tablett zum richtigen
  Tisch kommt."
* `zone.intro` (line 1677): "Ihre Bestellungen gehen an die Küche und die Theke in diesem Bereich"
  hard-codes a site with exactly one kitchen and one bar, and will be factually wrong at a site with
  two bars in a zone. Use "Ihre Bestellungen gehen an die Stationen in diesem Bereich."

Otherwise the German is genuinely good: the Sie form is consistent across all 205 strings, both the
checklists, and the slips, with no du anywhere.

## N21. Price formatting in English is not idiomatic

Section 8.8 line 1783 specifies `10.50 €` for English. English convention places the symbol first
(`€10.50`). The euro decision itself is correct. Trivial, but it is on the screen a server reads aloud.

## N22. The slip's timestamp is undefined for reprints

Section 7.7 prints "26.08.2026, 19:42 Uhr" under the order header without saying whether it is the
order time or the print time. On a reprint 40 minutes later the two differ, and print time on a slip
that reuses the original sequence number would be actively misleading. Fix: it is the order time, and
a reprint additionally prints when it was reprinted, under the `NACHDRUCK` banner.

## N23. The two-column slip line has no defined overflow

`Bedienung: Anna              Bereich: Innen` (line 1492) is a hand-aligned 48 column line. The
truncation rules (line 1547) cover item names and table labels but not this line. A server called
"Marie-Christine" and a zone called "Zelt hinten links" collide. Fix: state that the line wraps to two
lines when it does not fit.

## N24. Markdown: five horizontal rules have no blank line before them

Lines 622, 851, 1276, 1580 and 1999 place `---` directly after a table row or paragraph. Some
renderers will read those as a setext heading or swallow them. Trivial, but this document is meant to
be read as rendered Markdown.

---

# Challenges to the author's judgment calls

## Table label as free text with a suggestion list

**Holds up. Do not change it.** The justification at line 418 is the correct one and it is argued from
the right principle: a foreign key into a table list means the first unlisted table blocks an order.
Nothing in the system aggregates by table, so the structure buys nothing. The one thing worth adding
is that `TableSuggestion` should be seeded from the labels servers actually type: after the first
evening the department has a real list for free. That is a nice-to-have, not a correction.

One small consequence to state rather than fix: "Tisch 12", "tisch 12" and "T12" are three labels for
one table. Since nothing aggregates, it does not matter, and normalising would be work for no gain.
Line 433 already trims and collapses spaces, which is the right amount of effort.

## Routing falling back to any assigned location when zone filtering leaves no candidate

**The principle holds, the implementation as written does not.** Never rejecting an order is right, and
the reasoning at line 405 ("A slip printed at the wrong bar is recoverable; a refused order at a busy
table is not") is correct.

Three problems. The fallback omits the `IsActive` filter that the primary rule applies, which is B11.
The zero-candidate case is undefined, also B11. And the fallback is entirely silent: the server sees
`review.goesTo` naming a station, but nothing tells them it is in another zone, and there is no string
for it. The slip does print "Bereich: Innen" (line 1492), which means the receiving station can see
the order came from elsewhere, and that is a genuinely good half of the mitigation.

What I would do: keep the fallback, add the missing `IsActive` filter, define the zero-candidate case
as routing to an admin-chosen default station with a marked slip, and add one review-screen string:
"{item} geht an {station} im Bereich {zone}." Instruction is not needed here, only the fact, because
the server can act on it (walk over, or change the item) and cannot act on what they are not told.

## Admin API restricted to loopback, returning 404 to non-loopback callers

**Mostly holds, with one correction and one consequence to accept openly.**

Loopback-only is the right call for version 1. It replaces a credential nobody would manage with a
physical constraint everybody understands: the admin is the person standing at the laptop. On an open
WiFi with plain HTTP, any token-based admin auth would be sniffable, so the physical constraint is
genuinely stronger than the alternative, not merely simpler.

404 rather than 403 is defensible for the API and I would keep it. It is wrong for the admin *page*,
because the person most likely to hit it is the admin themselves via the LAN address the overview
screen advertises (N4). Fix that one case with an explanatory page.

The consequence to accept openly: this makes open question 2 inevitable, and not for the reason stated
there. The task that needs mobility is not revoking a phone, it is knowing that a printer has stopped.
Section 10.2 step 19 asks a volunteer to "Watch the printer screen now and then", which is a person
standing at a laptop watching for something that happens twice an evening. That is the weakest part of
the operational design. My recommendation is not an admin device: it is that a station going offline
or running out of paper already reaches every phone via `PrinterStatusChanged`, so the person running
the evening should simply carry an enrolled phone. That gets 90% of the value with zero new attack
surface, and it is already built.

## A 32 character StationAccessKey in the break-glass URL

**The length is fine. The distribution is the real defect and the spec does not cover it.**

128 bits of entropy in a URL path is correct for something that must be openable with no login, and on
a network where everything is plaintext anyway the marginal risk is negligible. It should be stored
hashed for consistency with backend rule 6, but I would not block on that: unlike a device token it
must be reconstructible into a URL to be shown again, so plaintext storage is a defensible trade.
Worth stating explicitly rather than leaving as an omission, and worth keeping out of request logs.

The gap is that nothing says how the kitchen gets the link at the moment the printer dies.
`station.unknownKey` (line 1959) says "Fragen Sie die Person am Laptop nach dem aktuellen Link", and
there is no admin screen for showing it, no QR for it, and no human way to convey 32 hex characters
across a loud marquee at 21:00. What I would do: give each station a printed card, produced by the
app, showing a QR to its break-glass URL, and print that QR on the test slip during setup (step 14 of
the checklist already fetches every test slip) so it can be taped inside the printer lid. The evening
the printer dies, the card is on the printer.

## No total printed on the slip

**Holds up. Do not change it.** Both arguments at line 1479 are correct: the kitchen does not need it,
and a total printed beside a list of goods is the point at which a well-meaning volunteer starts
handing slips to guests, which is where KassenSichV becomes somebody's problem. The non-goal at line 86
and this decision are the same decision, correctly applied twice.

The only cost is that if a server's phone dies between ordering and collecting cash, the total exists
only in the admin list. That is recoverable in ten seconds at the laptop and does not justify the
risk.

## The 5 minute ticket give-up window and the 10 minute phone submission give-up window

**Five minutes holds. Ten minutes is the wrong shape, though roughly the right number.**

Five minutes for a ticket is well reasoned at line 709 (about the time it takes to walk to the bar and
back) and the effect is right: a station that recovers on its own recovers before anyone is asked to
act. Keep it. But define when the clock starts, because B14 shows it is currently ambiguous for a
ticket waiting behind others.

Ten minutes for the phone is the wrong shape because of what happens at minute ten: retrying stops
entirely (line 2039). Stopping the machine is not what produces the value; showing the instruction is.
If the WiFi comes back at minute eleven while the page is still open, the order should go, and under
the current design it does not until a human notices and taps `order.retryNow`. Combined with strict
oldest-first ordering, a stopped entry can also stall everything behind it (B8).

What I would do: at ten minutes, show `order.giveUp` exactly as written (the wording is good and
telling a volunteer to fall back to paper is the honest answer), but drop the retry interval to 60
seconds rather than stopping, and skip stopped entries in the queue order. If the order later succeeds
on its own, the row updates and the server sees it before they have finished writing the paper slip.

## An EventSession entity scoping the numbering counters

**Holds up, and it is the best structural decision in the document.** It solves the reset problem
without deleting anything, it keeps yesterday's numbers meaningful, and it makes the backup a single
file that contains real history. Anything else (a nightly reset job, truncating counters, a date
prefix on the number) is worse.

Three risks the spec leaves open, all cheap to close:

1. Starting a session mid-evening is guarded only by prose (`admin.event.startWarning`, "Starten Sie
   eine neue Veranstaltung nur, wenn keine Gäste bedient werden"). `writing-ui-guidance` rule 1 says
   the app should check what it can check, and it can: it knows whether an order was accepted in the
   last hour and whether any ticket is non-final. Refuse in that case, or require the admin to type
   the event name to confirm.
2. If it does happen, numbering restarts at 1 in a pile that already contains a 001, so the same
   physical stack now holds two different slips numbered 042, neither marked `NACHDRUCK`. That defeats
   both the gap mechanism and the reprint mechanism at once. The spec covers the gap direction of this
   risk carefully (section 4.2) and never covers the duplicate direction.
3. Tickets from the previous session are orphaned, which is half of B3.

Close those three and the decision is unambiguously right.

---

# Missing pieces

Ranked by what will hurt first.

1. **Correcting or cancelling an order after it has printed.** Section 1.5 rules it out of scope and
   section 3.1 gives the reasoning, which I accept. But the product then owes the server one sentence
   at the table, and it does not have one. `admin.orders.voidBlocked` tells the *admin* to "Sagen Sie
   der Station Bescheid", while the person standing in front of the guest who just changed their mind
   has no guidance at all. One string in the order detail ("Diese Bestellung ist schon gedruckt. Sagen
   Sie der Station Bescheid und nehmen Sie die Änderung als neue Bestellung auf.") closes it.
2. **A second printer as a station's fallback.** When a printer dies, the current answer is the
   break-glass page, which requires the station to look at a screen, which the product otherwise
   refuses. A far better answer exists and costs one configuration field: let a station name a
   fallback station whose printer takes its slips, with the intended station printed in large type at
   the top. The marquee bar's drinks then print at the indoor bar and somebody carries them thirty
   metres, which is exactly the pre-software process. I would build this before I would build the
   break-glass page.
3. **Printer discovery.** Checklist steps 13 and 14 ask a volunteer to hold a feed button while
   powering on a printer, read an IP off a self test, and type it in, once per printer, at every
   event, because DHCP will move them. Scanning the local /24 for open port 9100 and offering the
   results in a list is an afternoon of work and removes the two steps most likely to go wrong on
   site.
4. **An end-of-evening step.** The checklist ends with the backup. It never says to open the "Nur
   Bestellungen, die geprüft werden müssen" filter and confirm it is empty, which is the one check
   that catches an order nobody produced. Add it as step 21, before the backup.
5. **What happens to unanswered `Unknown` tickets at the end of the night.** They are permanent by
   design (line 764) and the phones clear their lists on `EventSessionStarted`. So the last open
   question of the evening quietly disappears from every surface a human looks at. Starting a session
   should list them and force an answer.
6. **Shift handover of a phone.** `Device` is bound to a `ServerPerson` and there is no way to change
   it. The day crew handing a phone to the evening crew currently means every slip prints the wrong
   name, or a revoke and re-enrol that orphans the earlier orders (B4). One admin action, "assign this
   phone to a different person", covers it.
7. **Logging and diagnosis.** Section 10.4 mentions a log level and nothing else. When the marquee bar
   reports it received nothing all evening, the log is the only artifact that can answer why, and
   `/api/admin/diagnostics` does not include it. Specify a rolling file next to the database and a
   diagnostics screen link to it.
8. **Break-glass link distribution.** See the judgment call above.
9. **Near-end paper warning.** See N10. It is the cheapest prevention available for the most common
   failure.
10. **Recovery when the laptop's address changes.** See B10.
11. **A "practice mode" separate from the event.** The checklist has the volunteer place practice
    orders at home (step 7), which consume numbers in whatever session is active and land in the
    export the treasurer reads. Since sessions already exist, say plainly that practice happens before
    step 16 and that starting the event is what makes numbering real. The current step order does this
    by accident rather than by design.
12. **What the black console window means.** Step 1 says leave it open; closing it kills the evening,
    and nothing else in the product defends that. A tray icon, or a console that refuses to close
    without a confirmation, is worth more than several of the features that are specified.

---

# What is good

Brief, so the owner knows what not to re-examine.

* **The hardware reasoning is correct and checkable.** `ESC t 19` for PC858, 576 dots over 12 wide
  giving 48 columns, `DLE EOT n=2` bit 2 for cover open, `DLE EOT n=4` mask 0x60 for paper end,
  `GS ! 0x11` for double size, `GS V 66 3` for a partial cut: all verified against the ESC/POS command
  set. The one uncertain command, `GS ( H`, is correctly identified as an open question rather than
  assumed.
* **The bytes-written rule is the right axis for retry safety**, and putting `BytesWritten` on the
  result record rather than inferring it is exactly right. Fix the contradiction in B12 and this
  section is sound.
* **The refusal to auto-resolve `Unknown`** (line 764) is the single most important decision in the
  document and it is argued correctly.
* **Localization coverage is complete and mechanically clean.** All 205 keys carry both languages, no
  key is duplicated, no placeholder set differs between the two languages, no string exceeds three
  sentences, and no string exceeds 28 words. The Sie form is consistent throughout, including both
  checklists and the slips.
* **No em-dash characters and no hyphen used as one**, anywhere in the file. Checked mechanically.
* **The section 8.1 writing rules** are a correct reading of `writing-ui-guidance` and `no-ai-slop`,
  and the strings mostly live up to them. `ticket.paperEnd`, `ticket.failed` and
  `ticket.unknown.action` are model examples: verb first, one action, the reason second or absent.
* **The setup checklist**, particularly step 9 on client isolation, which is the failure that would
  otherwise consume an entire evening, stated in bold at the point of action with the consequence
  attached.
* **The mock transport as a product feature** with a fault table that maps one to one onto the real
  failure modes, and end-to-end tests defined per row.
* **The non-goals table** is doing real work. It will prevent arguments rather than merely record
  decisions.
