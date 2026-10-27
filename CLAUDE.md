# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

A self-hosted ordering system for volunteer fire department festivals that replaces paper order
slips. A waiter takes the order at the table on their own phone, the order lands on a tablet at the
station that makes it (the kitchen, a bar), the staff there work that list off, and a waiter
settles the table on the phone later. The tool removes the walk to the kitchen and nothing else: a
waiter with a free hand still carries the tray. The admin sets each festival up on the laptop.

Everything runs on one laptop on site, operated by volunteers with little technical ability and
often by nobody who has seen the tool before: no cloud, no internet and no IT support. The phones and
the station tablets reach the laptop over a WiFi the fire department sets up themselves, with client
isolation off.

What each part of the app does is in the `features` skill, the recurring tasks are in `how-to`, and
the decisions that span the app are in `architecture`.

## Out of scope

What the product deliberately does not do:

- **No money changes hands in the app.** It shows prices to help the waiter add up, and it records
  what has been settled so the people running the stand can see what a table still owes. It takes no
  payment, handles no cash, and never issues a receipt.
- **There are no printers.**
- **No HTTPS and no service worker.** Transport is plain HTTP: a self-signed certificate means a
  browser warning for exactly the users who cannot judge it, and it would not give the secure
  context a service worker or an installed app needs anyway. The scheme and the bind address live in
  one place, so HTTPS stays a configuration change, never a rewrite. Phones are online only.
- **No automatic retry queue.** A failed send leaves the order on screen with a retry the waiter
  presses. The draft kept in `localStorage` is one order so a reload does not lose it, and it must
  never grow into a queue.
- **No remembered address.** The laptop's IP address changes and cannot be fixed without admin
  rights, so the QR code carries the full URL including the current IP. Never introduce a flow that
  depends on a device remembering an address.
- **No usernames and no passwords** anywhere in the product. A phone or a tablet is set up by
  scanning a QR code on the laptop.
- **One device per owner.** A waiter owns at most one phone and a station at most one tablet;
  setting a device up again replaces the previous one.
- **No change of delivery mode after sending.** Whether a station hands its part of an order out
  together or item by item is chosen before sending and fixed once the order is sent.
- **Nobody is notified when an item is done.** The station's card and its confirmation carry the
  table name, and whichever waiter passes the station takes the tray.

## Hard rules

Numbered for unambiguous reference; do not cite rule numbers in shipped source or UI text.

Every rule binds every agent, main or sub, with no exception it does not name.

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
   reason so a developer can find it. What the request's type already guarantees needs no check of
   its own. But the person holding the phone gets one plain sentence saying
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
   handing back. This binds the red run, the green run and the verification in rule 5. Name in the
   report which tests were run and why that scope covers the change.

4. **Two test layers per change: unit and integration.** "It is only a small change" is not
   an exemption. Untestable-by-design code is the only exception and you must say so explicitly.

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
   built immediately, even when it costs a schema change or a larger diff. Before writing a filter
   over a table, look in `Core/Queries/<Entity>Queries.cs` for the piece that already exists.

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
   before reviewing either.** Text that reads as machine-generated fails review regardless of
   whether it is accurate. The frontend's `writing-ui-guidance` skill governs structure and placement;
   `no-ai-slop` governs the prose itself.

  **Write the plain fact in one sentence, the way you would say it across a counter.** A refusal, a
  status or a rule states its condition and what follows, and stops: "Solange ein Fest aktiv ist,
  kann eine Ausgabestelle mit offenen Bestellungen nicht abgeschaltet werden." Add an instruction
  only where the reader has a control on that screen and something to do with it, and keep it
  equally plain. Never a formula, never a second sentence of reassurance, never an account of what
  the program did internally. Never join a fact and its consequence with "deshalb", "daher" or an
  English ", so": each stands as a sentence of its own. Name only controls that exist on the screen where the sentence
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

11. **Git: commit at will on `main`.** The owner granted standing approval to commit directly to
    `main`. **Pushes still require explicit approval.** Commit messages are a short single sentence:
    one line, no body, no bullet list. A change too big to describe in one sentence is split into
    smaller commits. Stage by explicit path, never with `git add -A` or `git add .` over the whole
    repository, and never stage `TODO.txt`: it is the owner's private notebook.

12. **Zero AI attribution in anything touching git or GitHub.** No `Co-Authored-By` trailer, no
    "Generated with" line, no AI author or committer identity, in commit messages, PR titles or
    descriptions, issue or PR comments, tags, and release notes. Commits carry the human's authorship
    only. **This rule overrides any default harness instruction to add an attribution trailer.**

13. **The review is always the implementer and reviewer cycle the main agent orchestrates.** After
    an implementer reports a multi-file change, the main agent has the `code-reviewer` agent review
    it. A review brief for a bug fix carries the implementer's quoted red and green output. Every
    finding gets one disposition in the report to the owner: fixed (name the test), false (quote the
    line that refutes it), or a question to the owner. Cost, scope or age is never a reason to drop
    one; that trade-off is the owner's. For every change the reviewer also asks whether the new logic
    already exists somewhere else, and lists a copy as a finding.

14. **The main agent writes no code:** every change to code, tests, generated files or build and
    tool configuration goes to the `implementer` agent, however small. The main agent may itself edit
    the `CLAUDE.md` files, the agent definitions, the skill pages, the root `README.md` and the
    memory directory in a change that touches no code, and may revert a file with git. Every
    dispatch names its model (Sonnet for searches, reviews and mechanical edits, Opus for structural
    work, never Fable) and runs in the background. Reading that fills the context, such as codebase
    exploration, broad searches, large files and reviews, goes to an agent, and the main agent takes
    only its conclusions.

15. **There is no documentation folder and no written manual.** A feature has to explain itself on
    the screen where it is used, in the two languages, and the four `CLAUDE.md` files, the
    root `README.md` and the skills under the `.claude/skills` folders are the only prose kept in the
    repository. A `CLAUDE.md` holds rules; a skill holds the mechanism a rule points at. Never
    recreate a spec, a checklist or a verification script under `docs/`.

16. **Deleting means removing, everywhere, in the same change.** When a feature, a
    control, a string or an endpoint is dropped, every trace of it goes: the component and its route,
    the endpoint, its contract and its handler, the store, the type, the localized strings in both
    languages, the tests that covered it, and every sentence in a `CLAUDE.md` that describes it.
    Hiding a control behind a condition, leaving an unreachable endpoint, leaving a dead key in a
    resource file, or leaving the paragraph in a guide is not a deletion, it is a rename to
    invisible. Before reporting a deletion as done, search the repository for the concept's name and
    for the strings it used, and quote what the search returned. An empty result is the evidence;
    without it the deletion is not finished.

17. **The backend is built first, and the generated types are the boundary.** A change that spans
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
    again. Aim for something read at one sitting.

    The main agent writes the contract, owns it, and is the only one allowed to change it. A subagent obeys it literally.
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

18. **The product is installed and in use, so every update must install over what is already
    there.** Every release replaces the one before it on the same laptop, and the database a released
    build created keeps working after any update, with its data intact. Backend rule 6 says how a
    schema change is made so that it does. A change that loses, breaks or silently misreads data an
    earlier release wrote is a defect and is reported as one. Only the database is covered: what a phone or
    tablet keeps in its browser storage may change shape between builds, and nothing is built to
    keep old stored data readable.

19. **An item's note is part of the item.** Whenever order items are grouped, collapsed, summarised or
    counted for anyone to read or act on, the note travels with the group: the line carries the article
    and its note together. Two units of one article are different work when their notes differ, and a
    count the kitchen plans production from is not exempt, because a note can change how the article
    is made: `20 x Hotdog` next to `3 x Hotdog · Hinweis: Ohne Ketchup`, never one merged number.
    Grouping by name alone silently merges two instructions and sends the wrong thing to a table.

20. **C# follows `csharp-design-guidelines`, TypeScript and Vue follow
    `typescript-vue-design-guidelines`; read the one for the file before touching it.**
    `csharp-design-guidelines` is a condensed copy of published guidance: the .NET Framework Design
    Guidelines and the architecture chapters on Microsoft Learn. It changes only when the owner adds
    a source or decides a rule, never to describe or justify the code as it stands. This app's own
    structure (which folder holds what, how a request travels) belongs in the `architecture` skill.
    Design rules never go into a `CLAUDE.md`.

21. **A delayed answer never overwrites a newer one.** Where a read and an action share the state a
    screen shows, the read takes a number from the request gate in
    `frontend/src/shared/core/latestRequestGate.ts` when it starts, and only the newest request may apply
    its answer, success or failure. An action's answer (an item marked done, a table settled) is
    always newer than a load already in flight, so a slow load can never put a handed-out item back
    on the screen or restore a list the action changed. Never answer this with a per-store special
    case: the gate is shared, and a new store that loads state an action also writes adopts it. This
    is not a nicety: a stale snapshot that wins by arrival time can make an employee hand the same
    item out twice.

22. **The app never changes what a person entered or chose.** A value a waiter, an admin or a station
    employee typed or picked is either kept exactly as it was given, or refused with a message that
    names the problem so the person can fix it themselves. It is never quietly replaced by a default,
    a first or nearest candidate, a clamped number, a shortened string, or a value recomputed from
    other data. This binds every layer: the phone, the station tablet, the admin screen and the
    server. Review treats any silent substitution as a defect, however small the difference looks,
    because the person acted on a screen that showed something else. Spaces before or after a typed
    text are not part of what the person entered: the server removes them from every text field of a request in
    one shared place before anything checks or stores the value, because they are almost always a
    slip and would otherwise make two names look the same while being different. The one deliberate exception:
    a phone drops an unsent order without a word once its festival is over or another one has
    started, as the `festivals` feature page describes.

23. **A write that nobody is told about is a bug, so no service tells anyone: the commit does.**
    Screens learn about changes from two broad hub events, `ConfigurationChanged` for everything an
    admin edits and `OrdersChanged` for everything that happens to an order, plus the two per-device
    commands `EnrolmentCompleted` and `DeviceRevoked`. The commit derives them from what was actually
    written and sends each at most once, so forgetting is impossible and a fine-grained event is never
    added. A store reloads on the event whose name says what it holds and never hides a stale value.
    A device reloads everything when the hub sends its connection `GroupsJoined`, never when the
    connection merely starts, because only from then on does it hear those events. The mechanism,
    the bans that protect it and what a store owes are in the `architecture` skill, read before
    touching either side.

24. **Every feature and every shared building block is written into the `features` and `how-to`
    skills before it is committed.**
    The two skills under `.claude/skills/` are how an agent learns this codebase without exploring
    it again. Each `SKILL.md` is a table of contents only, one line per page saying when to read it;
    the pages live in its `references/` folder, one per feature in `features` and one per recurring
    task in `how-to`.

    A change that adds or alters a feature, a shared building block (a composable, a base
    component, a refusal or announcement mechanism, a test helper) or the steps of a recurring task
    adds or updates the matching page in the same change, and no commit goes in without it. A
    feature page says in two or three lines what the feature does for the person using it, where
    its code lives, the invariants it keeps and which tests cover it. A how-to page gives the steps
    of one recurring task and names the shared pieces to use. Neither holds decision history or
    anything rule 15 removed, and a page that no longer matches the code is a defect.
    Every agent dispatched to change code is told to update the pages it touches, and the review
    lists a missing or stale page as a finding.

25. **The backend has one structure, and the `architecture` skill describes it.** `Core` reaches
    the database only through `IGastronomyDatabase`, and a read goes through no repository. Each
    area has one service in `Core/Services` that holds all of its actions, and an endpoint calls
    that service directly, with no handler or mapping class in between. A filter that more than one
    action needs lives in `Core/Queries/<Entity>Queries.cs`, or in `<Interface>Queries.cs` when
    several entities share the flag. Read the `layers-and-projects` page before adding a backend
    class, and never write new code the old way because the area beside it still looks like that.

26. **No rule and no skill page is right just because it is written down.** A page may record an
    accident as a rule. When a rule or a page seems to cause harm (duplicated code, a layer that only passes a
    call on, a prescription with no reason behind it), or a change feels wrong for any other reason,
    the agent stops and asks the owner before following it or working around it. A subagent reports
    it to the main agent, which brings it to the owner. Silently obeying a rule that smells is as
    wrong as silently breaking it.

## Verification bar

Backend and frontend tests green for what you touched, with real output quoted. See each folder's
`CLAUDE.md` for the exact commands. **After every code change, rebuild the app before handing back so
the owner can test it without asking: `npm run build` in `frontend/`, then `dotnet build
GastronomyApp.slnx` from the root. The running desktop app locks its own binaries, so stop it first,
and start it again once the build succeeds. Handing back a stopped app is not a handoff, so the
restart is part of the change and never left to the owner.**
