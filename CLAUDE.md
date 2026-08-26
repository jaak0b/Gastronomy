# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

A self-hosted ordering system for volunteer fire department festivals. It replaces paper order slips.

Today a server walks table to table writing orders on paper, carries the paper to the kitchen or bar,
and later carries the finished food and drinks back. This tool removes the walk to the kitchen and
nothing else: the slip still lands on a pile at the production location, staff still work that pile
off, and a server with a free hand still delivers the tray.

Flow:

1. A server opens a web page on their own phone, picks items and quantities, enters a table name, sees
   the running total (a calculation aid only, cash is handled by hand), and places the order.
2. The backend splits the order by production location (kitchen, bar indoor, bar outdoor) and prints
   each location's slice on that location's thermal receipt printer.
3. Staff produce the items, put them on a tray with the printed slip, and a server delivers it.

**No money is handled.** The app displays prices to help the server add up. It takes no payment, stores
no payment data, and issues no receipts to guests.

## Structure

| Folder | Role |
|---|---|
| `backend/` | ASP.NET Core (.NET 9). REST API, SignalR push, SQLite, printing service. Serves the built frontend. |
| `frontend/` | Vue 3 + TypeScript + Vite + Pinia. Server phone app and admin configuration UI. Builds into the backend's `wwwroot`. |
| `pi-agent/` | Python agent for USB-attached printers. **Deferred**, not yet started. Until it exists, `MockPrinterTransport` stands in. |

Two source trees ship as one executable: the frontend build output is embedded in the backend's
single-file self-contained binary. The operator double-clicks one file.

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
  online-only. An order queues in `localStorage` and retries through short WiFi dropouts, but only
  while the page stays open.
- The laptop's IP address is not stable and cannot be made stable without admin rights. **The QR code
  carries the full URL including the current IP**, which is why it solves both enrolment and
  addressing. Never introduce a flow that depends on a phone remembering an address.
- Phones authenticate by scanning a **single-use, short-lived QR code** shown on the laptop, which they
  exchange for a long-lived device token held in `localStorage`. There are no usernames and no
  passwords anywhere in the product. Every device is revocable from the admin UI.

## Hard rules

Numbered for unambiguous reference; do not cite rule numbers in shipped source or UI text.

1. **Lost orders are the defect this product exists to prevent.** Any change that touches ordering,
   routing, printing, or printer status must state what happens when the step fails. A silently
   dropped order is the worst outcome in the system, and a silently duplicated one is the second
   worst. Every slip carries a global order number and a per-location sequence number so a gap is
   visible on the pile without anyone touching software.

2. **No silently swallowed errors.** A `catch` must surface the error, rethrow, or return a value the
   caller can act on. Empty catch blocks are forbidden. Problems a user can fix (printer out of paper,
   WiFi dropped, unknown table) are returned as user-worded messages in their language, never dropped
   and never surfaced as a raw exception or stack trace.

3. **TDD is mandatory and test-first, no exceptions.** For every behaviour change including bug fixes:
   (a) write the test, (b) run it and paste the failing output, (c) only then touch production code,
   (d) re-run to green. A red run you can quote is the gate. No red proof means the fix does not start.
   If you catch yourself having edited production code first, revert it and restart from (a).

4. **Two test layers per change: unit and integration.** End-to-end coverage is required for the order
   placement flow and the printing pipeline, and optional elsewhere. "It is only a small change" is not
   an exemption. Untestable-by-design code is the only exception and you must say so explicitly.
   Mutation testing and a coverage gate are deliberately not adopted yet.

5. **Verification before completion.** A change is not done until it has been verified and the real
   command output quoted. Never claim a feature is finished, never hand back to the owner, and never
   commit on assumed results. Run the tests covering what you touched and quote the totals.

6. **Extend the concept's existing home; never bolt a duplicate beside a symptom.** Before adding or
   fixing logic, find the module that already owns the concept (search for the concept, not just the
   symptom site) and extend it. Never compute a value the codebase already derives elsewhere: if a
   figure (a price total, a routing decision, a sequence number, a printer's status) is produced in two
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
   change. This binds the backend, the frontend, and the text printed on slips.

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

11. **No trademarked words in file names or identifiers.**

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

## Hardware constraints that bind the code

These come from the Epson TM-T20IV Technical Reference Guide and are not negotiable by preference.

- **One printing connection per printer at a time**, held until released, with a 90 second timeout.
  Print jobs must be serialized per printer. A crashed job blocks that station for 90 seconds.
- **A dropped socket means the job's status is unknown.** Re-query before re-sending, or the station
  prints the order twice. Duplicate orders are a real failure mode, not a harmless retry.
- **Paper-out detection uses `GS a` ASB as the primary push channel** (the printer tells us), with
  `DLE EOT n=4` polled as a heartbeat. Do not rely on polling alone.
- Status is read over the same connection: port 9100 is documented bidirectional for network printers,
  and USB printer class exposes a bulk IN endpoint for the Pi-attached case.

## Verification bar

Backend and frontend tests green for what you touched, with real output quoted. See each folder's
`CLAUDE.md` for the exact commands.
