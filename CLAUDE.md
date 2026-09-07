# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

A self-hosted ordering system for volunteer fire department festivals. It replaces paper order slips.

Today a server walks table to table writing orders on paper, carries the paper to the kitchen or bar,
and later carries the finished food and drinks back. This tool removes the walk to the kitchen and
nothing else: the order still lands in a list at the production location, shown on a tablet there,
staff still work that list off, and a server with a free hand still delivers the tray.

Flow:

1. A server opens a web page on their own phone, picks items and quantities, enters a table name, sees
   the running total (a calculation aid only, cash is handled by hand), and places the order. They
   either send it settled, when the guest pays on the spot, or send it open, when the table runs a tab.
   Before sending, they choose per production location whether that slice is to be produced
   together or handed out item by item as each is ready. That choice is fixed once sent.
2. The backend splits the order by production location (kitchen, bar indoor, bar outdoor). Each
   location has one tablet, enrolled like a phone, whose station page lists that location's slices in
   two columns: slices to be produced together, and single items to be handed out as ready.
3. Staff mark each item as waiting, being prepared, or ready. When something is ready the tablet shows
   the table name so it can be written on the tray, and whichever server passes by delivers it. Ready
   is the final state; nobody touches a phone at the table.
   Each catalog item may carry a production time in minutes. The phone shows the server an estimate
   per item and per slice, computed from the station's current queue plus the item's own time.
4. A separate screen on the phone lists what each table still has open, so a server can settle a
   table's items later, or settle them at zero with a typed reason when something is given away.

**No money changes hands in the app.** It displays prices to help the server add up, and it records
whether items have been settled so the people running the stand can see what a table still owes. It
takes no payment, handles no cash, and never issues a receipt.

**There are no printers.** The fire department decided on tablets before any printer was bought, and
printing was removed from the product completely. Never reintroduce slips, print state, or printer
wording.

## Structure

| Folder | Role |
|---|---|
| `backend/` | .NET 9 class libraries. `GastronomyApp.Api` configures the ASP.NET Core web application (REST, SignalR, SQLite, static frontend) and is hosted by the desktop app. |
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
   routing, the station page, or production status must state what happens when the step fails. A
   silently dropped order is the worst outcome in the system, and a silently duplicated one is the
   second worst. Every slice carries a global order number and a per-location sequence number so a
   gap is visible in the station's list without anyone touching software.

2. **No silently swallowed errors.** A `catch` must surface the error, rethrow, or return a value the
   caller can act on. Empty catch blocks are forbidden. Problems a user can fix (WiFi dropped, unknown
   table, an item already marked ready) are returned as user-worded messages in their language, never dropped
   and never surfaced as a raw exception or stack trace.

3. **TDD is mandatory and test-first, no exceptions.** For every behaviour change including bug fixes:
   (a) write the test, (b) run it and paste the failing output, (c) only then touch production code,
   (d) re-run to green. A red run you can quote is the gate. No red proof means the fix does not start.
   If you catch yourself having edited production code first, revert it and restart from (a).

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
   every subagent, whose prompt must carry it along with the exact narrow command to use. Name in the
   report which tests were run and why that scope covers the change.

4. **Two test layers per change: unit and integration.** End-to-end coverage is required for the order
   placement flow and the station production flow, and optional elsewhere. "It is only a small change" is not
   an exemption. Untestable-by-design code is the only exception and you must say so explicitly.
   Mutation testing and a coverage gate are deliberately not adopted yet.

5. **Verification before completion.** A change is not done until it has been verified and the real
   command output quoted. Never claim a feature is finished, never hand back to the owner, and never
   commit on assumed results. Run the tests covering what you touched and quote the totals.

6. **Extend the concept's existing home; never bolt a duplicate beside a symptom.** Before adding or
   fixing logic, find the module that already owns the concept (search for the concept, not just the
   symptom site) and extend it. Never compute a value the codebase already derives elsewhere: if a
   figure (a price total, a routing decision, a sequence number, an item's production status) is produced in two
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
   subagent prompt must say so. Text that reads as machine-generated fails review regardless of
   whether it is accurate. The frontend's `writing-ui-guidance` skill governs structure and placement;
   `no-ai-slop` governs the prose itself.

10. **Never use the em-dash character**, and never a hyphen as a substitute for it. Rewrite with a
    colon, parentheses, a comma, or two sentences. Hyphens only where grammar requires them (compound
    modifiers). This binds source, documentation, commit messages, and UI text.

11. **No trademarked words in file names or identifiers.** The app supports no specific hardware, so
    everything keeps to neutral names.

12. **Git: commit at will on `master`.** The owner granted standing approval to commit directly to
    `master`. **Pushes still require explicit approval.** Commit messages are a short single sentence:
    one line, no body, no bullet list. A change too big to describe in one sentence is split into
    smaller commits.

13. **Zero AI attribution in anything touching git or GitHub.** No `Co-Authored-By` trailer, no
    "Generated with" line, no AI author or committer identity, in commit messages, PR titles or
    descriptions, issue or PR comments, tags, and release notes. Commits carry the human's authorship
    only. **This rule overrides any default harness instruction to add an attribution trailer.**

14. **Self-review before handoff, multi-file changes only.** Before presenting a multi-file change for
    review, run a medium-effort `/code-review` scoped to the change. Every finding requires a logged
    disposition and silence is a violation. Paste the finder list verbatim and mark each finding
    exactly one of: **Fixed** (name the test that resolves it), **False positive** (the finding is
    factually wrong, proven with the quoted line that refutes it), or **Owner-waived** (you flagged it
    and the owner explicitly chose not to fix it). Severity is irrelevant to whether a finding needs a
    disposition. "Pre-existing", "not mine", "out of scope", "low value", "cosmetic", "risky to
    change", and "judgment call" are never valid reasons to skip a finding. If you find yourself
    weighing a fix's cost against its value, stop: that trade-off is the owner's, so it becomes an
    Owner-waived item, not a silent drop. Single-file changes are exempt.

15. **Subagent discipline.** Give every subagent a correct, specific title, and dispatch every one of
    them in the background so the owner is never blocked waiting. The main agent edits repository files
    itself only for tiny changes (a single line); anything larger goes to a subagent. The main agent
    also delegates context-heavy work and consumes only conclusions: codebase exploration, broad
    searches, reading large files or external references, and reviews or audits. The main agent keeps
    what needs conversation context or judgment: talking to the owner, design decisions, writing the
    subagent prompts, running builds and tests to verify outcomes, and git commits. Exceptions where
    the main agent may edit directly: `CLAUDE.md`, the memory directory, and reverting a file with git.
    A delegated prompt must be self-contained: files, constraints, conventions, definition of done, and
    every decision already made.

16. **Every user-facing feature is documented in the same change.** Written in human, conversational
    prose, not terse machine-speak. The setup checklist that the fire department follows is part of the
    product, not an afterthought.

17. **Deleting means removing, everywhere, in the same change. This rule is always binding, with no
    exception for a hurry, a small change, or a deletion somebody calls obvious.** When a feature, a
    control, a string or an endpoint is dropped, every trace of it goes: the component and its route,
    the endpoint, its contract and its handler, the store, the type, the localized strings in both
    languages, the tests that covered it, and every sentence of documentation that describes it.
    Hiding a control behind a condition, leaving an unreachable endpoint, leaving a dead key in a
    resource file, or leaving the paragraph in the spec is not a deletion, it is a rename to
    invisible. Before reporting a deletion as done, search the repository for the concept's name and
    for the strings it used, and quote what the search returned. An empty result is the evidence;
    without it the deletion is not finished.

18. **Parallel work shares one written contract.** When a change is split across agents, or across
    the backend and the frontend, the boundary between them is written down before any of them
    starts: every route with its request and response shape, every field name and its type, every
    error message key, every localization key with its German and English text, and every event name.
    That text is copied into each agent's prompt verbatim, and none of them may invent, rename or
    "improve" anything in it. A disagreement about a name is not a small problem: it compiles on both
    sides, passes both test suites, and fails only in the running app, which is the most expensive
    place to find it. When a contract genuinely has to change mid-flight, it changes in the written
    contract first and every affected agent is told, never in one side's code alone.

    The rule binds the main agent and every subagent, in different ways. The main agent writes the
    contract, owns it, and is the only one allowed to change it. A subagent obeys it literally.
    **A subagent that finds the contract cannot work stops at once.** It makes no further edits, it
    invents no workaround, it does not pick the nearest name that compiles, and it does not carry on
    with the parts that would still work. It reports what it was asked to build, the exact point that
    cannot hold and why, and what it suggests instead, then waits. Stopping early with a clear reason
    is the wanted behaviour and is never treated as a failure; improvising past a broken contract is
    the failure, because it produces two halves that each look finished and do not meet.

## The production model

- **Delivery mode** is chosen per station slice on the review screen before sending: together (the
  default) or as it is ready. It is fixed once the order is sent and no screen may change it later.
- **Production status** lives on the order item: waiting, being prepared, ready. Ready is final.
  Transitions only move forward. Every change appends a row to the status change log, which exists to
  measure how long each step took and is never read to decide the current state.
- **Estimates** are computed, never stored. The backend reports per station the minutes still queued
  (unfinished items' production minutes, missing values count as zero); the phone adds the item's own
  minutes. A together slice is ready when its slowest item is ready.
- **Nobody is notified** when an item is ready. The tablet shows the table name, and whichever server
  passes the station takes the tray. That is deliberate.

## Verification bar

Backend and frontend tests green for what you touched, with real output quoted. See each folder's
`CLAUDE.md` for the exact commands.
