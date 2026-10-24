# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

A self-hosted ordering system for volunteer fire department festivals. It replaces paper order slips.

Today a server walks table to table writing orders on paper, carries the paper to the kitchen or bar,
and later carries the finished food and drinks back. This tool removes the walk to the kitchen and
nothing else: the order still lands in a list at the production location, shown on a tablet there,
staff still work that list off, and a server with a free hand still delivers the tray.

Flow:

1. Before the festival the admin draws its areas and places its tables and stations on the laptop.
   A server opens a web page on their own phone, picks the area, taps the guest's table on its plan
   and chooses "Bestellung aufnehmen", picks items and quantities, sees the running total (a
   calculation aid only, cash is handled by hand), and places the order. An order is always sent
   open. One button sends it and returns to the plan, for a table that runs a tab; the other sends
   it and opens the table's settle view with the new items ticked, for a guest who pays on the spot. Before sending, they choose per production location whether
   that station order is to be produced together or handed out item by item as each is ready. That
   choice is fixed once sent.
2. The backend splits the order by production location (kitchen, bar indoor, bar outdoor). Each
   location has one tablet, enrolled like a phone. Its station page shows two columns: every order
   that still has open items, and the orders that go out as they are ready. A fully done order leaves
   both columns and appears in the done view.
3. Staff select the items they hand out, one or several at a time, and mark them done after a
   confirmation that repeats the table name. A mistaken tap can be put back from the done view.
   Each catalog item may carry a production time in minutes, and an article can be marked as
   prepared independently of its station's queue. The phone shows the server an estimate per item
   and per station order, computed from the station's current queue plus the item's own time, except
   for an independent article, which shows its own time alone.
4. On the plan each table's fill shows what has been handed out and its border what has been
   settled, so a server sees which tables still owe something. Tapping a table and choosing
   "Abrechnen" opens its settle view, where the server settles its items. Settling always names the
   amount the table actually paid: the full price is
   filled in, and the server may type any other amount, which needs a typed reason whenever it falls
   short.
   Giving something away is that same act with the amount set to zero, not a second kind of
   settlement.

**Stock is an estimate, computed live and never stored.** An article may list the ingredients one
portion uses (an ingredient has a name and a unit: pieces, grams or millilitres). Per festival the
admin may enter an available amount for an ingredient; an empty amount means unlimited, and one row
exists for every festival and ingredient. Consumption is summed from the festival's order items and
the recipes on every read, and the festival page shows available, used and a linear prediction of
the moment the stock runs out, counted from the festival's start. After an order is accepted, and
after the admin changes an amount, every menu row whose article needs more of a limited ingredient
than is left is switched off through the existing availability switch, and nothing switches it back
on by itself. Stock never refuses an order and may go negative. The stock step runs in the
order's transaction without a catch: a bug there fails the order loudly so it is found at once.

**No money changes hands in the app.** It displays prices to help the server add up, and it records
whether items have been settled so the people running the stand can see what a table still owes. It
takes no payment, handles no cash, and never issues a receipt.

**There are no printers.** The fire department decided on tablets before any printer was bought, and
printing was removed from the product completely. Never reintroduce slips, print state, or printer
wording.

## Structure

| Folder | Role |
|---|---|
| `backend/` | .NET 10 class libraries. `GastronomyApp.Contracts` holds the wire records, `GastronomyApp.Api` configures the ASP.NET Core web application (REST, SignalR, SQLite, static frontend) and is hosted by the desktop app. |
| `desktop/` | `GastronomyApp.Desktop`, the **only executable**: an Avalonia window that hosts the web application in-process. Launcher, status light and address display; never a second admin UI. |
| `frontend/` | Vue 3 + TypeScript + Vite + Pinia. Server phone app, station tablet page and admin configuration UI. Builds into `backend/GastronomyApp.Api/wwwroot`. |

Three source trees ship as one executable: the frontend build output is embedded, the Api library is
hosted in-process, and the operator double-clicks the desktop app. Closing its window never stops the
server; only an explicit confirmed quit does.

## Deployment reality

This shapes almost every design decision, so it is stated once here and assumed everywhere.

- The server runs on **a random laptop on site**, operated by people with little technical ability. No
  cloud, no internet on site, no IT support, and often nobody present who has seen the tool before.
- The site network is **WiFi only**, set up by the fire department themselves. Devices can reach each
  other (client isolation is assumed off, and is a documented setup step).
- Transport is **plain HTTP**. Not HTTPS: a self-signed certificate means a browser warning for exactly
  the users who cannot judge it, and it does not restore secure-context features anyway. The scheme and
  bind address live in one place so HTTPS stays a config change, never a rewrite.
- Because there is no secure context, there is **no service worker and no PWA install**. Phones are
  online-only. There is no automatic retry queue: a failed submission leaves the order on screen with
  a retry action the server taps themselves. `localStorage` holds the in-progress order as a draft
  cart so a reload does not lose it, which is not a queue and must never grow into one. Every
  submission carries a client-generated id so a retry after a lost response returns the original
  order instead of creating a second one.
- The laptop's IP address is not stable and cannot be made stable without admin rights. **The QR code
  carries the full URL including the current IP**, which is why it solves both enrolment and
  addressing. Never introduce a flow that depends on a phone remembering an address.
- Phones and station tablets authenticate by scanning a **single-use, short-lived QR code** shown on
  the laptop, which they exchange for a long-lived device token held in `localStorage`. There are no
  usernames and no passwords anywhere in the product. Setting an owner up again signs their old
  device out at that moment, and taking a waiter or a station off the list signs it out too.
- **One device per owner.** A staff member owns at most one phone and a station owns at most one
  tablet; the owner row points at its device. Setting up a device again replaces the previous one.

## Hard rules

Numbered for unambiguous reference; do not cite rule numbers in shipped source or UI text.

1. **Lost orders are the defect this product exists to prevent.** Any change that touches ordering,
   routing, the station page, or an item's fulfillment must state what happens when the step fails. A
   silently dropped order is the worst outcome in the system, and a silently duplicated one is the
   second worst. Every station order carries a global order number and a per-location sequence
   number so a gap is visible in the station's list without anyone touching software.

2. **No silently swallowed errors.** A `catch` must surface the error, rethrow, or return a value the
   caller can act on. Empty catch blocks are forbidden. Problems a user can fix (WiFi dropped, unknown
   table, an item that was already marked done) are returned as user-worded messages in their language, never dropped
   and never surfaced as a raw exception or stack trace.

   **A check the interface already enforces is a guard against a broken program, not advice to a
   user.** Keep the check on the server, because bad data must never be stored, and log the exact
   reason so a developer can find it. But the person holding the phone gets one plain sentence saying
   the order could not be processed and naming what to do instead, never a message that asks them to
   fix something the screen never let them get wrong. Write a message of its own only where the user
   really can act: a menu that changed under them while they were ordering is the usual case, and
   there the message names the item and the tap that clears it.

3. **A bug fix is test-first, always. A new feature is tested, not necessarily first.**

   For a **bug fix**, including anything a review calls a defect: (a) write the test, (b) run it and
   paste the failing output, (c) only then touch production code, (d) re-run to green. A red run you
   can quote is the gate. No red proof means the fix does not start, because a test written after the
   fix proves only that the code does what it now does. If you catch yourself having edited
   production code first, revert it and restart from (a).

   **Red means the test compiled, ran and failed on the assertion it was written for.** A build
   error, a missing member, a type mismatch or a test the runner never reached is not a red run
   and proves nothing about the behaviour. Neither is a failure for another reason: a fixture that
   does not set up, a wrong selector, an exception before the assertion, or a stub that throws only
   to produce a red run. The quoted failure names
   the wrong value the old code produced. A report that quotes anything else as the red run is
   rejected, and the fix restarts from (a).

   **Red is demanded where old behaviour exists to fail against.** A bug fix always has that: the
   old code gives a wrong answer and the test says which. A member that does not exist yet has
   nothing to fail against, so for a brand new method, type or property the test is still written
   before the body, but no red run is required and no throwing stub is added just to produce one.
   The moment such a test touches behaviour that already exists, the red run is required again.

   For a **new feature**, the tests rule 4 requires still ship in the same change, and they may be
   written after the code. Write them first anyway when the behaviour is intricate or the design is
   unclear, since that is where test-first earns its cost. Never hand back a feature whose tests were
   skipped "for now".

   **Never write a test whose purpose is to prove that deleted behaviour stayed deleted.** When a
   feature, a string or a control is removed, delete its tests with it and write nothing in their
   place. A test asserting that some text or element is absent pins the codebase to a decision that
   was already made, fails for unrelated reasons later, and describes nothing a user does. This binds
   the removal itself: the red run for a deletion is the existing test failing, not a new one.
   Assertions that some element is absent *under a condition the code still decides* are a different
   thing and remain welcome, for example that the table name notice is hidden until an item is marked ready.

   **Run as few tests as possible, every time.** The owner's machine is slow, so a run is never
   widened for comfort. Run only the tests that cover the code being changed: the single test file,
   or a name filter over the one class, and nothing else. Never the whole solution, never a whole
   test project when a file will do, never a full frontend suite, never "just to be safe" before
   handing back. This binds the red run, the green run and the verification in rule 5, and it binds
   every subagent, whose prompt or agent definition must carry it along with the exact narrow command
   to use. Name in the
   report which tests were run and why that scope covers the change.

4. **Two test layers per change: unit and integration.** "It is only a small change" is not
   an exemption. Untestable-by-design code is the only exception and you must say so explicitly.
   Mutation testing and a coverage gate are deliberately not adopted yet.

5. **Verification before completion.** A change is not done until it has been verified and the real
   command output quoted. Never claim a feature is finished, never hand back to the owner, and never
   commit on assumed results. Run the tests covering what you touched and quote the totals.

6. **Extend the concept's existing home; never bolt a duplicate beside a symptom.** Before adding or
   fixing logic, find the module that already owns the concept (search for the concept, not just the
   symptom site) and extend it. Never compute a value the codebase already derives elsewhere: if a
   figure (a price total, a routing decision, a sequence number, an item's fulfillment) is produced in two
   places, unify on the single source. A concern shared across flows lives in a shared module wired
   into all consumers, never patched into one flow. **Interim solutions are forbidden in all cases:**
   deferred fixes are forgotten and the interim state becomes permanent, so the correct structure is
   built immediately, even when it costs a schema change or a larger diff.

7. **No code comments.** Code self-explains through names and structure, in everything we author (C#,
   TypeScript, Vue, YAML, JSON, `.csproj`). Banned: what-narration (`// build the order`), divider
   banners, commented-out code (git is the history), and default XML doc comments. The only allowed
   comment is a short non-obvious **why** the code cannot express, such as a documented hardware
   constraint or an external-bug workaround. Tempted to write what? Rename until the comment is
   redundant, then delete it. Markdown documentation is exempt.

8. **German and English are both first-class from day one.** Every user-visible string is localized in
   both languages in the same change. A string that exists in one language only is an incomplete
   change. This binds the backend, the frontend, and the desktop shell.

9. **UI text is plain, complete prose aimed at a non-technical volunteer.** Complete grammatical
   sentences, neutral register, no clipped fragments, no jargon, no invented abbreviations. One term
   per concept in each language, used consistently. Assume the reader is holding a phone in one hand
   at a loud festival and has never been trained on the tool.

   **Read the `no-ai-slop` skill before writing or changing any user-facing text or any UI code, and
   before reviewing either.** This binds the main agent and every subagent without exception, and the
   subagent's prompt or agent definition must say so. Text that reads as machine-generated fails review regardless of
   whether it is accurate. The frontend's `writing-ui-guidance` skill governs structure and placement;
   `no-ai-slop` governs the prose itself.

  **Write the plain fact in one sentence, the way you would say it across a counter.** A refusal, a
  status or a rule states its condition and what follows, and stops: "Solange ein Fest aktiv ist,
  kann eine Ausgabestelle mit offenen Bestellungen nicht abgeschaltet werden." Add an instruction
  only where the reader has a control on that screen and something to do with it, and keep it
  equally plain. Never a formula, never a second sentence of reassurance, never an account of what
  the program did internally. Name only controls that exist on the screen where the sentence
  appears. Write each language for itself; a sentence translated word for word from the other one
  reads as machine-written even when every word is correct. Read it aloud before you ship it, and if
  you would not say it to a colleague across a counter, write it again.

  **A sentence belongs on a screen only if it changes what the reader does.** Before adding a hint,
  a caption or an explanatory paragraph, name the decision or action it changes. If you cannot, it
  does not ship. Restating what the screen already shows, explaining how the program works, and
  facts nobody holding the device can act on are the filler that makes software read as
  machine-written, and a screen is not the place for them. Empty states, failure messages and
  confirmations of a destructive action stay, because each one names a next step or a cost. A
  reviewer lists any added line that cannot answer "what does the reader do differently because of
  this" as a finding.

10. **Never use the em-dash character**, and never a hyphen as a substitute for it. Rewrite with a
    colon, parentheses, a comma, or two sentences. Hyphens only where grammar requires them (compound
    modifiers). This binds source, documentation, commit messages, and UI text.

11. **No trademarked words in file names or identifiers.** The app supports no specific hardware, so
    everything keeps to neutral names.

12. **Git: commit at will on `main`.** The owner granted standing approval to commit directly to
    `main`. **Pushes still require explicit approval.** Commit messages are a short single sentence:
    one line, no body, no bullet list. A change too big to describe in one sentence is split into
    smaller commits. Stage by explicit path, never with `git add -A` or `git add .` over the whole
    repository, and never stage `TODO.txt`: it is the owner's private notebook.

13. **Zero AI attribution in anything touching git or GitHub.** No `Co-Authored-By` trailer, no
    "Generated with" line, no AI author or committer identity, in commit messages, PR titles or
    descriptions, issue or PR comments, tags, and release notes. Commits carry the human's authorship
    only. **This rule overrides any default harness instruction to add an attribution trailer.**

14. **Self-review before handoff, multi-file changes only, and the main agent runs it.** A subagent
    doing the work never starts a review: it reports what it built and stops. Only the main agent
    starts a review, once, at the end, over the finished change, and the reviewer agent is what
    performs it. An implementing agent that reviews reads a working tree full of other agents' half
    finished work, reports findings about code it was never given, and duplicates the review that
    happens at the end anyway.

    **Self-review before handoff, multi-file changes only.** Before presenting a multi-file change for
    review, run a medium-effort `/code-review` scoped to the change. Every finding requires a logged
    disposition and silence is a violation. Paste the finder list verbatim and mark each finding
    exactly one of: **Fixed** (name the test that resolves it), **False positive** (the finding is
    factually wrong, proven with the quoted line that refutes it), or **Owner-waived** (you flagged it
    and the owner explicitly chose not to fix it). Severity is irrelevant to whether a finding needs a
    disposition. "Pre-existing", "not mine", "out of scope", "low value", "cosmetic", "risky to
    change", and "judgment call" are never valid reasons to skip a finding. If you find yourself
    weighing a fix's cost against its value, stop: that trade-off is the owner's, so it becomes an
    Owner-waived item, not a silent drop. Single-file changes are exempt.

    **The review covers names.** The reviewer reads every new or renamed method, type and property
    cold, states what it does from the name alone, and reports as a finding any name that needs the
    body, or a comment, to be understood.

15. **Subagent discipline.** Give every subagent a correct, specific title, and dispatch every one of
    them in the background so the owner is never blocked waiting.

    **The main agent never changes code itself.** Every change to code, tests, generated files or
    build and tool configuration (`.editorconfig`, `.gitignore`, tool manifests, package files) goes
    to the `implementer` agent defined in `.claude/agents/implementer.md`, however small it is, a
    one-line edit included. Its definition carries everything that is the same for every change, so
    the brief carries only what is particular to this one: the task, the files it concerns, what
    done looks like, the decisions already taken and, where rule 18 applies, the written contract.
    This binds the main agent only: the implementer writes the code it was given itself and never
    dispatches another implementer.

    **Every dispatch names its model,** because an agent without one inherits the main agent's. Never
    dispatch a Fable subagent. Sonnet runs audits, searches, reviews and mechanical edits, the
    implementer included when the edit is purely mechanical; Opus runs structural work, one
    structural change at a time. Sonnet loses track of long lists, so a long mechanical job is split
    into a few small ones.

    **A subagent owns the change it was given, and finishes it before it answers.** It may start
    read-only helpers to explore, search or map the codebase, and those pay for themselves. It may
    never hand its edits to another agent: the agent that was dispatched writes the code it was asked
    to write, so one change has one author. It may not report while anything it started is still
    running, and its report says what it verified in the tree itself, never what it delegated.
    Reporting a change as finished while a child is still writing it leaves a half-changed tree that
    looks complete, which costs more than the delegation ever saved. The main agent
    also delegates context-heavy work and consumes only conclusions: codebase exploration, broad
    searches, reading large files or external references, and reviews or audits. The main agent keeps
    what needs conversation context or judgment: talking to the owner, design decisions, writing the
    subagent prompts, running builds and tests to verify outcomes, and git commits. Exceptions where
    the main agent may edit directly: `CLAUDE.md`, the agent definitions under `.claude/agents/`, the
    memory directory, a skill page or the root `README.md` in a change that touches no code, and
    reverting a file with git. A delegated prompt must be self-contained apart from what the agent's
    own definition already carries.

16. **There is no documentation folder and no written manual.** The owner removed `docs/` on
    2026-09-11: nobody reads a spec, and a checklist nobody follows rots. A feature has to explain
    itself on the screen where it is used, in the two languages, and the three `CLAUDE.md` files, the
    root `README.md` and the skills under the `.claude/skills` folders are the only prose kept in the
    repository. A `CLAUDE.md` holds rules; a skill holds the mechanism a rule points at. Never
    recreate a spec, a checklist or a verification script under `docs/`.

17. **Deleting means removing, everywhere, in the same change. This rule is always binding, with no
    exception for a hurry, a small change, or a deletion somebody calls obvious.** When a feature, a
    control, a string or an endpoint is dropped, every trace of it goes: the component and its route,
    the endpoint, its contract and its handler, the store, the type, the localized strings in both
    languages, the tests that covered it, and every sentence in a `CLAUDE.md` that describes it.
    Hiding a control behind a condition, leaving an unreachable endpoint, leaving a dead key in a
    resource file, or leaving the paragraph in a guide is not a deletion, it is a rename to
    invisible. Before reporting a deletion as done, search the repository for the concept's name and
    for the strings it used, and quote what the search returned. An empty result is the evidence;
    without it the deletion is not finished.

18. **The backend is built first, and the generated types are the boundary.** A change that spans
    the backend and the frontend builds and finishes the backend first. The OpenAPI document it
    writes then hands the frontend, through `npm run generate:api`, every request and response shape,
    every field name and type, and every enum value on the wire, under the backend's own names. The
    frontend uses those generated types for every request body and every answer, never a hand-written
    copy, so a name the two sides disagree on fails the frontend build instead of the running app.

    **What the generated document does not carry is written down before the backend starts**, in one
    short text copied verbatim into the prompt of every agent on either side: the route paths, which
    the frontend writes as plain strings; the refusal message keys with their German and English
    texts; the hub event names; and the few rules both sides rely on, such as which list includes
    hidden rows. None of them may invent, rename or "improve" anything in it. A disagreement about a
    name there compiles on both sides, passes both test suites, and fails only in the running app,
    which is the most expensive place to find it. When the text genuinely has to change mid-flight, it
    changes in the written text first and every affected agent is told, never in one side's code
    alone. Agents working in parallel inside one tree write down the boundary between them the same
    way before any of them starts.

    **The written part is never the specification of the feature.** Its whole job is that both halves
    meet on the first run, so the owner does not have to sit through several attempts before a
    feature works. Out: request and response shapes and field names, which the generated types own;
    screen layouts, which component holds which control, dialog flows, the tests each side writes,
    and the reasoning behind decisions already taken.

    Apply one test to every line before it goes in: **could each agent get this right on its own and
    still fail to meet the other?** If the answer is no, cut it: it belongs to whichever agent owns
    that part of the tree, and putting it in the written part only makes a reviewer check it again and
    again. Aim for something read at one sitting. The festival contract ignored all of this, ran past
    a thousand lines, and cost nine review rounds and more than four hours before a single line of
    code was written.

    The rule binds the main agent and every subagent, in different ways. The main agent writes the
    contract, owns it, and is the only one allowed to change it. A subagent obeys it literally.
    **A subagent that finds the contract cannot work stops at once.** It makes no further edits, it
    invents no workaround, it does not pick the nearest name that compiles, and it does not carry on
    with the parts that would still work. It reports what it was asked to build, the exact point that
    cannot hold and why, and what it suggests instead, then waits. Stopping early with a clear reason
    is the wanted behaviour and is never treated as a failure; improvising past a broken contract is
    the failure, because it produces two halves that each look finished and do not meet.

    **A green suite on each side proves nothing about the boundary,** because each side's tests agree
    with that side's own vocabulary. After any change that touches the boundary, regenerate the
    frontend types and typecheck, then check by name every shared name the generated types do not
    cover: route paths, hub event names and message keys. Prefer a test that reads the backend's
    source and asserts the frontend declares the same set, the way `i18nCoverage.spec.ts`,
    `apiRouteVocabulary.spec.ts` and `hubEventVocabulary.spec.ts` already do, so the check holds
    without anyone remembering it.

## The fulfillment model

- **Delivery mode** is chosen per station order on the review screen before sending: together (the
  default) or as it is ready. It is fixed once the order is sent and no screen may change it later.
- **An item is open or done.** Done is `OrderItem.FulfilledAtUtc`, set when the station hands the item
  out and cleared again when a mistaken tap is put back. There is no in-between production state and
  no status log.
- **The station page shows two columns**: every order with open items, and the as-it-comes orders the
  employee has not hidden from that column. A fully done order leaves both and appears in the done
  view. The hide decision lives on the station order as `StationOrder.IsHiddenFromAsItComesQueue` and
  only removes the order from the second column.
- **Estimates** are computed, never stored. The backend reports per station the minutes still queued,
  counted from open items' production minutes (missing values count as zero) and excluding articles
  that are prepared independently of the queue. The phone adds the item's own minutes, and for an
  independent article it shows that time alone. A station order set to together is done when its
  last item is done.
- **Nobody is notified** when an item becomes done. The card and the confirmation carry the table
  name, and whichever server passes the station takes the tray. That is deliberate.

19. **The product is installed and in use, so every update must install over what is already
    there.** Every release replaces the one before it on the same laptop, and the database a released
    build created keeps working after any update, with its data intact. Backend rule 7 says how a
    schema change is made so that it does. This binds the main agent, every subagent and every
    reviewer: a change that loses, breaks or silently misreads data an earlier
    release wrote is a defect and is reported as one. Only the database is covered: what a phone or
    tablet keeps in its browser storage may change shape between builds, and nothing is built to
    keep old stored data readable.

20. **An item's note is part of the item.** Whenever order items are grouped, collapsed, summarised or
    counted for anyone to read or act on, the note travels with the group: the line carries the article
    and its note together. Two units of one article are different work when their notes differ, and a
    count the kitchen plans production from is not exempt, because a note can change how the article
    is made: `20 x Hotdog` next to `3 x Hotdog · Hinweis: Ohne Ketchup`, never one merged number.
    Grouping by name alone silently merges two instructions and sends the wrong thing to a table.

21. **C# code follows the `csharp-design-guidelines` skill, and nothing else.** The skill under
    `backend/.claude/skills/` is the checklist of the official .NET design guidelines: how code is
    structured into layers, files and classes, how types and members are designed, how names are
    chosen, how exceptions and collections are used. It is read before any C# file is touched, and
    its naming principles bind the TypeScript and Vue code too. No naming, structure or design rule
    is written into a `CLAUDE.md` again; a new rule goes into the skill. A name that needs a comment
    to explain it is wrong: rename until the comment is redundant (rule 7).

22. **A delayed answer never overwrites a newer one.** Where a read and an action share the state a
    screen shows, the read takes a number from the request gate in
    `frontend/src/shared/core/latestRequestGate.ts` when it starts, and only the newest request may apply
    its answer, success or failure. An action's answer (an item marked done, a table settled) is
    always newer than a load already in flight, so a slow load can never put a handed-out item back
    on the screen or restore a list the action changed. Never answer this with a per-store special
    case: the gate is shared, and a new store that loads state an action also writes adopts it. This
    is not a nicety: a stale snapshot that wins by arrival time can make an employee hand the same
    item out twice.

23. **The app never changes what a person entered or chose.** A value a waiter, an admin or a station
    employee typed or picked is either kept exactly as it was given, or refused with a message that
    names the problem so the person can fix it themselves. It is never quietly replaced by a default,
    a first or nearest candidate, a clamped number, a shortened string, or a value recomputed from
    other data. This binds every layer: the phone, the station tablet, the admin screen and the
    server. Review treats any silent substitution as a defect, however small the difference looks,
    because the person acted on a screen that showed something else. The one deliberate exception:
    a phone drops an unsent order without a word once its festival is over or another one has
    started, as the `festivals` feature page describes.

24. **A write that nobody is told about is a bug, so no service tells anyone: the commit does.**
    Screens learn about changes from two broad hub events, `ConfigurationChanged` for everything an
    admin edits and `OrdersChanged` for everything that happens to an order, plus the two per-device
    commands `EnrolmentCompleted` and `DeviceRevoked`. The commit derives them from what was actually
    written and sends each at most once, so forgetting is impossible and a fine-grained event is never
    added. A store reloads on the event whose name says what it holds and never hides a stale value.
    A device reloads everything when the hub sends its connection `GroupsJoined`, never when the
    connection merely starts, because only from then on does it hear those events. The mechanism,
    the bans that protect it and what a store owes are in the `architecture` skill, read before
    touching either side.

25. **Every feature and every shared building block is written into the `features` and `how-to`
    skills before it is committed. This rule is always binding, and the owner never has to ask.**
    The two skills under `.claude/skills/` are how an agent learns this codebase without exploring
    it again. Each `SKILL.md` is a table of contents only, one line per page saying when to read it;
    the pages live in its `references/` folder, one per feature in `features` and one per recurring
    task in `how-to`.

    A change that adds or alters a feature, a shared building block (a composable, a base
    component, a refusal or announcement mechanism, a test helper) or the steps of a recurring task
    adds or updates the matching page in the same change, and no commit goes in without it. A
    feature page says in two or three lines what the feature does for the person using it, where
    its code lives, the invariants it keeps and which tests cover it. A how-to page gives the steps
    of one recurring task and names the shared pieces to use. Neither holds screen layouts, decision
    history or anything rule 16 removed, and a page that no longer matches the code is a defect.
    Every agent dispatched to change code is told to update the pages it touches, and the review
    lists a missing or stale page as a finding.

## Verification bar

Backend and frontend tests green for what you touched, with real output quoted. See each folder's
`CLAUDE.md` for the exact commands. **After every code change, rebuild the app before handing back so
the owner can test it without asking: `npm run build` in `frontend/`, then `dotnet build
GastronomyApp.slnx` from the root. The running desktop app locks its own binaries, so stop it first,
and start it again once the build succeeds. Handing back a stopped app is not a handoff, so the
restart is part of the change and never left to the owner.**
