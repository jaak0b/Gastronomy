# Second adversarial review of `docs/spec.md`

Reviewed against the 4056 line document, `docs/spec-revision-log.md`, and the four `CLAUDE.md` files.
The reviewer wrote none of the specification.

## Verdict

**Another pass is needed, on five areas, before implementation starts.** The document is in far better
shape than the review log's starting point: the numbering design, the idempotency design, the routing
invariant, the give-up suspension rationale and the desktop host are all specified to a level an
implementer can build from without inventing policy. But eight sequential passes have left a
recognisable pattern of damage: **prose that was rewritten and a table three sections away that was
not.** Every blocking finding below is of that shape, and two of them silently reopen findings the
revision log records as closed.

The five areas that need another pass are **7.6 (the outcome table, which now contradicts 7.8)**,
**5.6 and 8.10 (the acknowledge rule, which permits exactly the case its own prose says it forbids,
and an undo with no mechanism)**, **7.6 plus 2.12 and 7.3 (the circuit breaker was never rekeyed to
the endpoint when the workers were)**, **6.1 and 6.2 (the station group was never widened when the
break-glass page was)**, and **10.1 to 10.3 (the desktop host's first run contradicts its own
one-process rule, and its data folder repair cannot run for the user it exists to protect)**.

Sections 1, 2.1 to 2.11, 2.13, 3.0, 3.1, 3.4, 3.6, 4, 5.1 to 5.5, 5.7, 7.1 to 7.5, 7.7, 9, 11 and 12
are clean or carry only the non-blocking notes listed below, and do not need another pass.

Em-dash characters: **zero in the document.** Checked mechanically.

---

## Blocking findings

### B1. The 7.6 outcome table sends every mock print to `Printed`, reopening the trap 7.8 exists to close

**Section 7.6, line 2108, against section 7.8, line 2467.**

7.6 is the mapping table, and its first row is unconditional: `Confirmed`, all bytes, job `Confirmed`,
ticket **`Printed`**. 7.8's fault table maps the mock's `None` case to outcome `Confirmed` with all
bytes written and ticket **`PrintedOnTestPrinter`**. Both tables claim to be the mapping. Nothing in
7.6 mentions the transport kind, and 7.4 step 9 says only "map to the job and ticket states in section
3".

11.1 tests `RetryPolicy` against "the full table in section 7.6, one case per row", so the test suite
ratifies the wrong table.

**Failure scenario.** An implementer builds `RetryPolicy` from 7.6, as instructed. A volunteer sets up
three stations and configures printers for two, leaving the third on the mock. During the real event
every order routed to the third station reports `Printed` on the placing server's phone, the order
projection reaches `Printed` by row 3 of 3.1, the attention filter stays empty, and checklist step 23
passes. The slips accumulate as text files in a folder nobody opens. This is finding B6 of the first
review, verbatim, restored by a table that was never updated when `PrintedOnTestPrinter` was added.

**Fix.** Add the transport kind as a column or a row to the 7.6 table: `Confirmed` on a `Mock`
transport maps to `PrintedOnTestPrinter`, `Confirmed` on any other transport maps to `Printed`. State
in 7.6 that it is the single mapping and that 7.8's table is a restatement of it for the mock's
faults, not a competing one. Add a `RetryPolicy` test asserting the mock row.

### B2. The break-glass acknowledge rule permits a `Printing` ticket, which 5.6 asserts one line later that it forbids

**Section 5.6, lines 1811 to 1820.**

The rule as written is a disjunction:

> A ticket may be acknowledged when its own status is `Failed`, `Unknown` or `Blocked`, **or** when its
> production location cannot print right now.
>
> A location cannot print right now when any of these holds: `IsFaulty`, `IsOnline` is false,
> `IsPaperEnd`, `IsCoverOpen`, `IsInErrorState`, or `IsEnabled` is false.

Line 1820 then claims "A ticket in `Printing` is refused by the same rule". It is not. The second
clause says nothing about ticket status, and the conditions in it are exactly the ones that arise
**while** a job is in flight. 3.2's state diagram has no `Printing --> HandledOnPaper` arrow at all,
and 11.1's `TicketAcknowledgePolicy` test only covers "a `Queued` or `Printing` ticket at a **healthy**
station refused", so neither the diagram nor the test catches it.

**Failure scenario.** The kitchen printer is out of paper, the crew has opened the station page and is
working off the screen. Somebody loads a fresh roll. `IsPaperEnd` clears, the worker claims ticket 042
and sets it `Printing`, writes 900 of 1100 bytes, and the roll is misfeeding so `IsInErrorState` sets
while the job is in `AwaitingEcho`. The station page re-evaluates on `PrinterStatusChanged`,
`canAcknowledge` is true by the second clause, and the cook taps "Übernommen". The ticket becomes
`HandledOnPaper` and the placing phone is told no slip will be printed. The printer then finishes the
job and echoes the process id: the worker holds a `Confirmed` job whose ticket is `HandledOnPaper`,
with a slip physically on the pile and a cook making the same order from the screen. Table 12 is
served twice. 3.2's claim that "there is no window between the two" is false, because the claim
transaction only closes the race in the direction where the human arrives before the claim.

**Fix.** Make the rule a conjunction on the ticket status as well: never acknowledge a ticket whose
status is `Printing`, whatever the printer reports, and say so as its own sentence rather than as a
consequence somebody has to derive. Add the `Printing`-at-a-broken-station case to the
`TicketAcknowledgePolicy` test list in 11.1 and to the break-glass integration list in 11.2.

### B3. The admin screen instructs the operator to change the data folder mid-evening, and changing it destroys the evening

**Section 8.9, line 2931, against sections 9.3, 4.3, 10.1 and 10.2.**

`admin.printers.mockFolderUnwritable` reads, in both languages: "In the program's settings, choose a
data folder where you are allowed to create files." The data folder is where `gastronomy.db` lives
(10.2). Nothing in 10.1, 10.2 or 10.7 says what happens when it is changed while the server is
running, whether the existing database is moved or a new one is created, or whether a restart is
required.

**Failure scenario.** At 20:40 the volunteer who is not the machine's owner sees the unwritable-folder
message, follows the instruction it gives, and points the data folder at their own Desktop. The
program opens an empty database there. Every device token was in the old file, so every phone's next
request answers 401 and every server on the site is returned to the enrolment screen holding a
half-built order. The `NumberCounter` rows are gone, so the next order is `Bestellung 1` and `BON 001`
into a pile that already holds a 001, which is precisely the double-numbering catastrophe 2.3 spends a
table preventing. The unique index on `ClientOrderId` is empty, so any retry of an order already
accepted in the old database creates a second order. Two of the three guarantees this product exists
to provide fail in the same click, and the click was the one the screen asked for.

**Fix.** Two changes. First, 10.1 must state the data folder's change semantics: it takes effect on
the next start, the operator is told the program must be restarted, and the window refuses the change
while the current session has accepted any order (the same class of guard 2.3 already applies to
starting a session). Second, reword `admin.printers.mockFolderUnwritable` so it does not instruct a
mid-service data folder change: the honest instruction during a live event is that the station cannot
print and the order has to be announced in person, with the folder repair left to between events.

### B4. The circuit breaker was never rekeyed to the printer endpoint when the workers were

**Section 7.6, lines 2152 to 2163, against sections 2.12 (lines 741 to 771) and 7.3 (line 2004).**

7.3 and 2.13 were rewritten so that the worker, the socket and the `PrinterProcessId` counter are all
keyed by the printer endpoint. The breaker was not. It sets `PrinterStatus.IsFaulty`, which is a row
per **production location** (2.12), moves "every waiting ticket at that station" to `Failed`, and is
cleared by `POST /api/admin/printers/{locationId}/reconnect` (line 1686), which is also per location.
When two locations share one endpoint there is one worker, one socket and two of everything the
breaker touches, and the specification never says which.

**Failure scenario.** The marquee bar's printer dies at 20:15 and the admin correctly performs
checklist step 21, pointing the bar at the kitchen's printer. At 21:00 the kitchen printer jams and
two consecutive attempts end `Unknown`. The worker trips. Read the rule per location and the tickets
it was attempting (kitchen tickets) fail while the bar's tickets stay `Queued` behind a worker that
"attempts nothing further", with every bar server's phone showing "Wird gedruckt" until the 20 minute
outer bound reaches each one individually over the following twenty minutes. Then a volunteer clears
the jam and presses "Wieder verbinden" on the kitchen row: the kitchen's `IsFaulty` clears, the bar's
does not, and whether the one worker restarts is undefined. If the implementer instead reads the rule
per endpoint, the bar's `PrinterStatus` row is written by a breaker trip its own configuration screen
gives no way to clear.

**Fix.** State that the breaker is a property of the endpoint, exactly as the worker and the counter
are: tripping sets `IsFaulty` on every location resolving to that endpoint and fails every waiting
ticket at all of them in the one transaction, and `reconnect` on any of those locations clears all of
them and restarts the one worker. Add it to the shared-printer integration row in 11.2, which
currently tests the happy path only.

### B5. A station page filtered to another location never receives that location's new orders

**Section 6.1 line 1869 and 6.2 line 1875, against section 5.6 line 1794 and section 8.10.**

5.6 widened the access key so that "a valid access key opens the page for the whole site and the
filter chooses which location's tickets it shows". 6.1 was not widened: the group is
`station:{locationId}`, "any open break-glass page for that location", and `OrderAccepted` and
`TicketStatusChanged` go to `station:{locationId}` of the affected ticket only. Nothing says the page
leaves and joins groups when the filter changes, and nothing says it may join a group for a location
whose key it does not hold.

**Failure scenario.** This is the exact scenario the filter was added for. The bar's printer is dead,
the admin has repointed the bar at the kitchen printer, and the kitchen printer then dies too. The
person at the kitchen scans the kitchen's card, switches the filter to the bar, and reads the bar's
open tickets. The page is subscribed to `station:{kitchen}`. Every bar order taken from that moment on
arrives over SignalR to a group this page is not in, and 6.3's refetch rule only fires "after any
reconnect", which never happens because the connection is healthy. The page sits showing a frozen
list. The bar's food is never made, and the page reports itself as up to date the whole time.

**Fix.** Specify the filter's effect on group membership: the page joins `station:{selectedLocationId}`
and leaves the previous one when the filter changes, and the hub authorises that join on the strength
of any valid `StationAccessKey`, which is the same widening 5.6 already took deliberately. Add a
SignalR integration case: switching the filter and then accepting an order at the newly selected
location puts a row on the page.

### B6. First run needs an elevated process, and 10.1 forbids the program from starting one

**Section 10.3 line 3535, against section 10.1 line 3304 and the single-instance rule at line 3371.**

10.1: "The desktop application does not launch a service, a second executable or a child process."
10.3: "the rule is created with an elevated command during first run". A process running as a standard
user cannot raise its own token; elevation on Windows is always a new process. The two statements
cannot both hold, and the specification never says which process performs the grant, what it is, or
how it relates to the mutex.

**Failure scenario.** An implementer honours 10.1 literally and attempts the firewall rule in-process.
It fails with an access denied that the specification has no message for, `desktop.firstRun.declined`
is shown for an elevation that was never requested, and the phones cannot reach the laptop on the
first evening. An implementer who instead re-launches the executable elevated hits the second
problem: 10.1 says the mutex is taken "at startup", so the elevated instance finds the mutex held,
signals the first over the named pipe, and exits without doing the work. The first-run marker may
already have been written. The rule is never created, and the settings button that is supposed to
repair it has the same defect.

**Fix.** State the exception explicitly in 10.1: the program launches exactly one short-lived elevated
helper process during first run and from the "Allow access from the network" button, it does nothing
but the firewall rule and the ACL grant, and it exits. State that the single-instance mutex is taken
by the window, not by that helper, and that the helper never takes it. Name what the helper is (an
elevated `netsh advfirewall` invocation plus the ACL grant, or a manifest-elevated second entry point)
so that "does not launch a second executable" is not read as forbidding it.

### B7. The ProgramData permission repair cannot run for the second Windows user, who is the person it exists to protect

**Section 10.2 lines 3483 to 3491, and section 10.3 lines 3513 to 3521.**

10.2 correctly identifies the trap and correctly says the grant needs no administrator rights "because
the owner of a folder may always change that folder's own access list". That is true only for the
folder's owner. The repair path is "The first run setup step in section 10.3 performs the same grant,
so a folder created before this rule existed is repaired rather than left broken", but 10.3's first
run is described as happening once per installation and nothing defines what makes a run "the first"
one. The only plausible marker, `settings.json`, lives in the very folder that is unwritable.

**Failure scenario.** Volunteer A prepares the laptop at home under their own Windows account and runs
the version that predates the ACL rule, or declines the elevation. `C:\ProgramData\GastronomyApp\`
exists, owned by A, with Users at read only. At the festival volunteer B signs in, starts the updated
program, and it finds the folder and `settings.json` present, so it treats the run as not-first and
prompts for nothing. Startup's writability check (5.1) then fails, the window shows
`desktop.error.dataFolder` naming a path, and its instruction is to choose a different data folder,
which is finding B3 above. B is not the owner and cannot repair the ACL, and the elevation that could
is never offered. The evening does not start.

**Fix.** Decouple the repair from "first run". On every start, if the data folder exists and the
current user cannot write to it, offer the elevated repair explicitly with its own string, rather than
offering a different folder. Define the first-run marker as something outside the data folder, or
define first run as "the required state is not present" rather than "we have run before", which is
also what makes the repair idempotent.

### B8. Acknowledge can be undone for ten seconds, and there is no endpoint, transition, or event that undoes it

**Section 8.10 line 3080, against section 5.6 (endpoint table, lines 1770 to 1776), section 3.2
(diagram, line 918) and section 6.2.**

8.10 says "Marking a row as taken can be undone for ten seconds" and gives it a string,
`station.undo`. 5.6 lists five station endpoints and none of them reverses an acknowledgement. 3.2
draws `HandledOnPaper` as terminal. 6.2 has no event that would tell the placing phone the
acknowledgement was retracted. 11.2's break-glass row does not test it.

**Failure scenario.** Two cooks are working the same screen. One taps "Übernommen" on slip 042 by
mistake, sees it, and taps "Rückgängig". If the POST already went, nothing can reverse it: the ticket
is terminal, the placing server has already been told over `TicketStatusChanged` that no slip will be
printed, and 3.2 guarantees no job will ever be sent for it. The undo control lies. If the implementer
instead builds the undo as a ten second client-side delay before sending, then the page shows an
acknowledged row that the server has not accepted, and a second cook on a second phone sees the row as
still open and takes it again, and neither the 409 nor the `canAcknowledge` flag protects against that
because the first request has not been sent.

**Fix.** Pick one and specify it. The safer choice is the client-side delay, in which case say so
explicitly, say what the row renders during those ten seconds, and accept that two phones can both be
holding an unsent acknowledgement. If a real undo is wanted, it needs an endpoint, a
`HandledOnPaper --> Queued` transition in 3.2 with the same claim-transaction protection step 2 of 7.4
gives, and a `TicketStatusChanged` push. Do not leave both readings available.

### B9. The 20 minute outer bound is specified to fire "whatever is holding it", including a job with bytes on the wire

**Section 3.2, line 956, against the diagram at lines 905 to 907 and section 7.4 steps 7 and 8.**

The prose says "A ticket that reaches 20 minutes goes to `Failed` whatever is holding it". The diagram
gives only `Queued --> Failed` and `Blocked --> Failed` for the bound, with no arrow from `Printing`.
11.1's `TicketStateMachine` test requires that "every transition not listed is refused", so prose and
diagram give an implementer opposite instructions for a state that is reachable at that moment.

**Failure scenario.** A busy kitchen printer is blocked on paper for nineteen minutes. The roll goes in
at minute 19:20, the worker claims the oldest ticket, and at minute 19:50 it is in `AwaitingEcho` with
all bytes written. At minute 20:00 the bound fires. Under the prose reading the ticket goes `Failed`,
the placing server gets `ticket.failedAfterWaiting` and walks over to announce the order, and eight
seconds later the printer echoes the process id and the slip is on the pile. The kitchen produces the
order from the slip and again from the server standing in front of them. Under the diagram reading the
implementer refuses the transition, which is correct, but nothing in the document tells them that is
the intended behaviour.

**Fix.** State the exception in 3.2 in the same breath as the bound: the outer bound never fires on a
ticket in `Printing`. A job is bounded by `JobTimeoutSeconds` in any case, so the bound is evaluated
again when the job ends and the ticket is at most 90 seconds late. Add it to the `GiveUpWindow` test
list in 11.1, which currently asserts the opposite by saying the bound "expires under every one of
those causes".

---

## Non-blocking findings

### N1. `PrintedOnTestPrinter` and `Printing` are missing from the mock's failure paths and the station status strings

7.8's fault table (lines 2465 to 2473) gives resulting ticket states for all seven faults, and
`ConnectTimeout` and `DropSocketEarly` both read "`Queued`, then `Failed`". That is correct only when
the give-up window runs, and the mock's unwritable-folder case is one of the four suspension causes
(3.2). The table does not distinguish them, so an implementer reading only 7.8 will fail a ticket at
five minutes that 3.2 says should be suspended. Separately, 5.6 defines an open ticket as including
`Printing`, and 8.10's `station.status.*` group has no string for it: `waiting`, `cannotPrint`,
`failed` and `unknown` are the four, so a `Printing` row renders nothing. Fix: add the state mapping
note to 7.8 and a `station.status.printing` key.

### N2. The location's slip language is specified but exists nowhere in the model, the API or a screen

7.7 line 2189: "The language is a per location setting, defaulting to German." `ProductionLocation` in
the ERD and in 2.4's field table (line 350 onward) has no such field. `POST` and `PUT
/api/admin/locations` take `{name, sortOrder}` only. No admin string sets it. Failure scenario: the
English slip rendered in full at lines 2295 to 2317 cannot be produced by any operator action, which
also makes rule 8 of the root `CLAUDE.md` unmeetable for slips. Fix: add `SlipLanguage` to the entity,
the ERD, both location endpoints and the admin location form, or delete the sentence and state that
slips are always rendered in German.

### N3. Every QR-consuming surface needs a raster encoder, which makes open question 11's second fallback impossible

10.1 line 3350 says the window's QR "is drawn from the same QR encoder the printing service uses for
the station card's **fallback path** (section 7.7 and open question 11)". That encoder exists only if
`GS ( k` fails. But the window needs a QR unconditionally, and so does `GET
/api/admin/locations/{id}/station-card`, which 5.5 describes as "a printable card with the station
name and a QR code". So the encoder is required in every branch, which kills open question 11's second
fallback ("the URL printed as wrapped text with no symbol at all") and forces the dependency backend
rule 5 is hostile to. Fix: say plainly that a QR encoder is a version 1 requirement for the window and
the printable card regardless of what the printer's firmware does, and let question 11 decide only
whether the slip uses `GS ( k` or a raster of that same encoder's output.

### N4. Changing the port or the bind address in the settings window has no stated consequence

10.1's settings table (lines 3385 to 3390) lists both, and `desktop.settings.bindAddress` has no help
string at all. 5.5 lines 1670 to 1677 states the consequence of an address change in full, for the
case where DHCP causes it, and nothing extends that to the case where the operator causes it from a
button the product provides. Failure scenario: at 20:00 the operator changes the port to 8080 because
they read `desktop.settings.portHelp` and misread it. Every phone's `localStorage` is scoped to
`http://192.168.1.23:5000`, so every token, and every draft cart, is stranded at an origin nothing
will visit again, and each phone must be enrolled from scratch. Fix: reuse the existing warning. Both
settings show the same consequence sentence `admin.overview.addressChanged` already carries, and both
refuse silently-applied changes while any device is enrolled and the session has accepted orders.

### N5. The station card's printed QR is dead after an address change, and the recovery text does not mention it

The break-glass URL encoded on the card taped inside every printer lid (7.7, 8.10) embeds the
laptop's IP. `admin.overview.addressChanged` tells the admin to set every phone up again and says
nothing about the cards. Failure scenario: the router reboots at 19:00 and hands out a new address.
Everything is repaired by re-enrolment except the cards, and at 21:00 when a printer dies the cook
scans the card in the lid and gets nothing, in the one situation the card exists for. Fix: add the
reprint of the station cards to `admin.overview.addressChanged` and to checklist step 9's consequence
sentence.

### N6. `ticket.failed` states a duration that the suspension rule makes false

`ticket.failed` (line 2801) reads "Der Drucker dort antwortet seit fünf Minuten nicht" / "The printer
there has not answered for five minutes". Under 3.2 the give-up window accumulates only unsuspended
time, so a ticket can reach `Failed` after a wall-clock nineteen minutes of which fourteen were
suspended. The message then states a false number to somebody who is about to walk. Fix: make it a
`{minutes}` parameter as `ticket.failedAfterWaiting` already does, or drop the duration and name the
cause.

### N7. The suspension arithmetic is left to the implementer

3.2 says the clock "resumes the moment the condition clears without the elapsed time being reset",
which implies accumulated running time, and 11.1's `GiveUpWindow` row says "resumes without
resetting". Neither states it as a rule for the alternating case. Worked example the document owes the
implementer: paper out at minute 0, cleared at minute 18, printer then unreachable. The give-up window
has accumulated zero, so it would fire at minute 23, three minutes after the outer bound has already
ended the ticket. The bound therefore silently dominates every suspended ticket, and no reader can
tell whether that was intended. Fix: state the accumulation rule in one sentence and add the worked
example, so nobody implements wall-clock-since-cause-cleared instead.

### N8. `OrderAccepted` and `TicketStatusChanged` carry nothing a station row can be rendered from

6.2 says the station page "gains a row" on `OrderAccepted`, whose payload is `{orderId,
globalOrderNumber, tableLabel, totalCents, tickets[]}`. 8.10 requires each row to carry the slip
number, the order number, the table, the time taken, every line with quantity and note, the order
note, and `canAcknowledge`. None of the lines, notes, times or the acknowledge decision is in either
payload. Fix: either state that the station page refetches `/api/station/{key}/tickets` on both
events, or widen the payload. The first is simpler and matches 6.3's "SignalR is a push channel, not a
source of truth".

### N9. The 503 and 429 responses have no strings, and the string that will be reused states a falsehood

5.1 specifies a 503 `DatabaseUnavailable` "with a message telling the server to try sending the order
again", and 5.1 specifies 429 "with a plain message". Neither has a key in section 8. The only
available string is `review.sendFailed`: "Der Laptop war nicht erreichbar", which is factually wrong
for a 503, where the laptop answered and its disk did not. 5.5 line 1745 states the governing rule:
"A failure that has no message key is a failure nobody wrote a sentence for, and the answer is to write
the sentence." Fix: add `review.sendFailedDatabase` and `review.tooManyRequests` in both languages.

### N10. `TicketResolvedByHuman` and `Confirmed` have no message key, which 11.1 asserts is impossible

11.1's `messageForTicket` row requires that "Each ticket state and failure reason maps to exactly one
message key in both languages". `FailureReason` has nine values (2.11 line 682) and
`TicketResolvedByHuman` has no key. It never reaches a phone, because the phone sees the ticket state
`HandledOnPaper` and `ticket.handledOnPaper`, but the test as written cannot pass. Fix: scope the test
statement to the reasons that reach a client, and say in 2.11 which `FailureReason` values are
admin-only diagnostics.

### N11. The plural-forms rule is stated inside 8.5 and the strings that need it are mostly in 8.9

The list of at-risk keys given in the review brief is incomplete. Ten web keys carry `{count}`:
`header.attention` (2644), `header.stationWaiting` (2648), `catalog.basketSummary` (2695),
`admin.overview.itemsWithoutLocation` (2849), `admin.overview.openTickets` (2851),
`admin.overview.stationBlocked` (2852), `admin.locations.openTickets` (2867), `admin.printers.waiting`
(2926), `admin.event.blockedOpenTickets` (3023), `admin.event.blockedQuestions` (3024). 8.5's rule
(line 2636) does cover them by wording ("Every string with a `{count}` placeholder"), so the gap is
closed in substance, but the same sentence then says "The tables in this section give the general
form", which scopes it to 8.5. Two further keys carry a second count that no plural mechanism selects
on: `admin.overview.stationBlocked` has `{minutes}` alongside `{count}`, and
`ticket.failedAfterWaiting` has `{minutes}` alone, so both read "seit 1 Minuten" at one minute. Fix:
move the rule to 8.1, where it binds every table, and state that a string carrying two counts is split
or worded so that neither needs a plural form.

### N12. Cause-first guidance, against 8.1's own rule

8.1 requires that "A message about a problem names the next step first and the cause second". Three
strings break it: `catalog.paperWarning` and `catalog.offlineWarning` (2697, 2698) both open with the
cause, and `admin.printers.mockFolderUnwritable` (2931) puts the action in its third sentence, at the
three-sentence ceiling. Fix: reorder. The catalog warnings' actual next step is "Sie können weiter
bestellen", which is already the second clause and should lead.

### N13. Checklist step 14 tells the volunteer to expect the ordering page, which an unenrolled phone cannot show

Step 14 in both languages (lines 3612 and 3693): "Take one phone and scan the QR code in the program
window. If the ordering page opens, the phones can reach the laptop." Per 8.2 the window's QR carries
the site root, and a phone with no token at `/` gets the six digit enrolment screen. Failure scenario:
the volunteer scans, sees a code entry field instead of the ordering page, concludes the network step
failed, and works through step 8 and the firewall button for a system that was already working. Fix:
say what actually opens, which is the setup screen, and that seeing it at all is the proof.

### N14. Nothing in checklist step 12 changes the transport from `Mock` to `Network`

Locations are created with `TransportKind: "Mock"` (5.5, line 1600). Step 12 says to search and tap the
found printer, and 6.2 describes `PrinterDiscovered` as giving "a row the admin can tap to fill in the
address". Filling in an address does not change the transport kind. Failure scenario: the volunteer
taps the discovered printer, prints a test slip at step 13, no paper comes out, and the only thing
that catches it is step 15's refusal, whose message ("Set up a printer at {names}") describes work
they believe they have done. Fix: state that tapping a discovered printer sets `TransportKind` to
`Network` along with the host and port, and say so in `admin.printers.hostHelp`.

### N15. At most one outstanding invitation is asserted but never enforced

2.8 line 560: "At most one invitation is outstanding at any moment", and the six digit verification
design depends on it: "Verification checks a single row rather than searching a set, which is why a
hashed short code needs no plaintext lookup index." No index, constraint or transaction is specified
that makes it true. Failure scenario: the admin has the server list open in two browser tabs and
clicks "Neue Bedienung" in each within a second. Both inserts succeed, two invitations are
outstanding, and the redeem path has no defined row to verify a six digit code against and no way to
find it without scanning every unconsumed invitation and running PBKDF2 against each. Fix: specify a
partial unique index on the outstanding condition, and specify that creating an invitation consumes
the previous one in the same transaction that inserts, which 2.8 already implies but does not state as
a transaction.

### N16. A revoked device's live SignalR connection is not terminated

6.1 defines `person:{serverPersonId}` as "Every unrevoked phone belonging to that person" and 2.8 says
revocation "pushes a SignalR message to that device, which clears its token". The clearing is
client-side. Nothing says the hub drops the connection or removes it from the person group. Failure
scenario: a phone is lost with the page open in a pocket. The admin revokes it. REST is correctly
401'd, but the connection stays and keeps receiving that person's order and ticket events, including
table labels and slip numbers, for the rest of the evening. Low harm, but it contradicts backend rule
6's "Revoking a device invalidates its token immediately". Fix: state that the hub aborts the
connection and removes the group memberships in the same transaction that sets `RevokedAtUtc`.

### N17. `Device.Language` has no stated origin at enrolment

The redeem response returns `"language": "de"` (5.2) and the redeem request has no language field.
2.8 says every backend message to that phone is rendered in it. Nothing says whether it defaults to
German, comes from `Accept-Language`, or is chosen on the enrolment screen, and 8.3's screen has "One
field for a name and one button. Nothing else." Fix: state the default, which by 8.10's precedent
should be German unless the browser asks for English first, and note that the settings sheet is where
it is changed.

### N18. `Blocked --> Failed` at the give-up window exists in the job machine and not in the ticket machine

3.3 line 1015 gives `Blocked --> Failed : give-up window expired or the outer bound was reached`. 3.2's
ticket diagram gives only `Blocked --> Failed : the outer bound was reached`. That is deliberate for
the four suspended causes, but 3.2's own prose says "a mechanical error the printer reports" runs the
clock, and 7.6 maps `PrinterError` with zero bytes to ticket `Blocked`. Failure scenario: the printer
reports a mechanical error before sending, the ticket is `Blocked`, the clock runs by 3.2's prose, and
at five minutes the `TicketStateMachine` refuses the transition because 11.1 requires every unlisted
transition to be refused. The ticket parks for a further fifteen minutes with `ticket.printerError` on
the phone. Fix: add the arrow to 3.2 conditioned on the cause not being one of the four suspending
ones, or move `PrinterError` with zero bytes to ticket state `Failed` directly.

### N19. 7.5's release rule names only paper end

7.5 line 2098: "paper end clearing releases `Blocked` jobs at that printer". 7.6 says `Blocked` retries
"when the condition clears" and lists cover open and printer error as blocking conditions. Failure
scenario: somebody closes the cover and the parked slips do not print, because the specification's one
release trigger did not fire, and `ticket.coverOpen`'s promise that "Der Bon wird danach von selbst
gedruckt" is broken. Fix: say that any transition of the blocking condition to clear releases the
blocked jobs, not paper end specifically.

### N20. An order whose only ticket is `HandledOnPaper` reports itself `Printed` to the server

Row 3 of 3.1 (line 859) includes `HandledOnPaper` in the `Printed` bucket, so the phone's chip reads
"Gedruckt". `ticket.handledOnPaper` on the detail screen says the opposite: no slip will be printed.
The order row and the ticket row inside it contradict each other at a glance, and 8.8 states that the
list must "answer one question at a glance". Low harm, because the food is being made either way. Fix:
either keep the bucket and reword the chip to something that covers both, or give `HandledOnPaper` its
own chip.

### N21. The revision log records two closures that the current spec has removed, without saying so

The log's "What changed after these dispositions" section is otherwise scrupulous, but two entries are
now stale and are not covered by it. B4's disposition cites "An admin can reassign a phone to a
different person for a shift handover (5.5)", and missing piece 6 cites `PUT /api/admin/devices/{id}`
with `admin.devices.reassign`. The current 5.5 states the opposite outright: "No endpoint creates a
person, and no endpoint moves a phone to somebody else." That is a defensible consequence of the
enrolment rewrite (the new carrier sets the phone up under their own name), but the log is the artifact
a future reader uses to check that a closure was not silently undone, and here it was. Fix: add a
bullet to the log's supersession list saying the enrolment rewrite removed device reassignment and
what replaced it.

### N22. The desktop `CLAUDE.md` says the settings window holds four settings and nothing else

`desktop/CLAUDE.md`: "The settings window holds only what cannot live in a web page served by the very
server being configured: port, bind address, database location, and which network to display. Nothing
else." 10.1 adds two buttons to that window and 10.3 makes the firewall repair a permanent resident of
it. Both are defensible (they are actions, not settings, and the firewall button genuinely cannot live
in a page the firewall is blocking), but the file reads as forbidding them. Fix: one sentence in the
`CLAUDE.md` allowing actions that cannot be performed from a served page. This is the only
`CLAUDE.md` contradiction found across the four files.

---

## Seam check results

1. **Cross-references.** Traced every reference into section 10, the 7.6 table, and 3.0/3.2/3.5. Two
   are broken: 5.6's claim about `Printing` (B2) and 10.1's claim about the QR encoder's provenance
   (N3). References to deleted machinery are clean: no rolling code, no zone, no void, no offline
   queue, no mock station screen and no console window survives anywhere in the document, and 8.9 and
   open question 9 both record the console's removal rather than leaving a dangling mention.
   Checklist step numbers cited from prose (step 21 for the repoint) resolve correctly.

2. **State machines against tables against prose.** Three defects: B1 (7.6 versus 7.8 on
   `PrintedOnTestPrinter`), B9 (3.2 prose versus its own diagram on the outer bound), N18 (3.2 versus
   3.3 on `Blocked --> Failed`). The claim transaction at 7.4 step 2, the nullable `ProcessId` chain
   (2.11, 7.2, 7.4 step 6), the `GS ( H` confirmation and the re-query order are internally consistent
   and are the strongest-specified part of the document.

3. **SignalR event list against client behaviour.** One blocking defect (B5, the station group was
   never widened) and one non-blocking (N8, payloads too thin for the behaviour described). No event
   pushes to anything deleted, and every described push has an event.

4. **String tables.** Every key referenced in prose exists in a table and no key from a deleted
   feature survives: `admin.devices.*`, `admin.overview.console`, `admin.overview.missingZone`,
   `zone.*`, `order.notSent`, `order.giveUp` and `enrol.error.nameMissing` are all absent, as their
   dispositions promise. German and English placeholder sets are identical everywhere. Sie is used
   without exception, including on the station page and the desktop window. Gaps: N9 (missing keys for
   503 and 429), N10, N11 (plural rule scoped to the wrong section, list is ten keys not six, and two
   keys carry an unpluralised `{minutes}`), N1 (`station.status.printing`), N12 (cause-first
   ordering).

5. **ER diagram against entity sections against payloads.** `EnrolmentInvitation`,
   `PrinterEndpointKey`, nullable `ProcessId`, `PrintAttempt.Phase`, `IsAvailable`,
   `ChosenProductionLocationId` and `ClientOrderId` are each declared in the ERD, in their entity
   section, and in every payload that carries them. One omission: the per-location slip language (N2).

6. **Checklist against the features.** The order is executable and no step assumes the console, the
   rolling QR, moving the executable or power settings. Two defects: N13 (step 14 promises the wrong
   screen) and N14 (step 12 never changes the transport kind). One gap: the "During the festival"
   block never mentions the sold-out toggle, which is the one thing an operator does at the laptop
   during service, and 8.9 spends three paragraphs on it.

7. **Claim transaction interleavings.** Walked worker claim, human acknowledge, human resolve, reprint
   and the bound against each other. Two holes: B2 (acknowledge of a `Printing` ticket via the station
   clause) and B9 (the bound firing on a job with bytes on the wire). The acknowledge-versus-claim race
   in the direction 3.2 describes is genuinely closed. One minor gap not raised as a finding: a
   reprint from `Printed` returns the ticket to `Queued`, which then makes it acknowledgeable at a
   broken printer even though its original slip is on the pile, and 3.2 does not extend its
   `Unknown --> HandledOnPaper` reasoning to that case.

8. **Suspension arithmetic.** Not clean, and not blocking: N7 gives the worked example the document
   owes, and N6 gives the message that goes false as a result.

9. **Shared printer.** Slip ordering survives interleaving correctly (4.2 and 7.3 agree, and the
   per-location run of sequence numbers is preserved because ordering is by `CreatedAtUtc`). The
   circuit breaker does not: B4.

10. **Enrolment races.** The invitation-versus-invalidation race, the six digit cap versus the QR, and
    the revoke-on-issue are all correctly specified. Two holes: N15 (the one-outstanding invariant is
    asserted, not enforced) and N16 (revoked device's live connection).

11. **Idempotency against the desktop world.** "Forever" holds while the database does. Changing the
    data folder mid-evening breaks it, and the product actively instructs that change: B3.

12. **Desktop host.** Three findings: B6 (elevation versus the no-child-process rule and the mutex),
    B7 (the ACL repair cannot run for the user it protects), N4 (port and bind address changes have no
    stated consequence).

**Compliance.** Zero em-dash characters. One `CLAUDE.md` contradiction, in the desktop file (N22); the
root, backend and frontend files are consistent with the spec throughout, including the frontend's
no-zones constraint that the revision log's closing section says was repaired. `writing-ui-guidance`:
the three sentence ceiling holds everywhere, "one rule one strength" holds, and the instruction-first
rule fails in three strings (N12). Nothing requires station staff to interact in the normal flow: the
break-glass page renders no buttons while a printer works, which is stated and tested. Nothing
requires a secure context, the camera API, a service worker, or a remembered address; 8.3 states the
`getUserMedia` reason explicitly and the station page's `localStorage` use is a display preference
only.

---

## What is good

The numbering design (section 4) is the best-specified part of the document and needs nothing. The
idempotency chain from 5.4 through 9.3, including the requirement that the 200 body is byte for byte
the 201 body and that the phone must not distinguish them, is complete, reasoned and testable. Section
3.0's known-cause versus unknown-cause principle is the kind of stated principle that stops a later
pass from making a locally sensible change that breaks something three sections away, and 7.6's
refusal to let queue depth trip the breaker is exactly right for the deployment. The two-flag split in
2.5 is specified down to who taps what and when, with a table that makes the distinction impossible to
collapse. The decision to key the worker and the process id counter on the printer endpoint is
correct and the reasoning given for it (a late echo confirming the wrong job) is the real risk. The
open questions are honest, and 12's practice of keeping closed questions with their numbers is a
discipline worth keeping.
