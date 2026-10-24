---
name: architecture
description: Read before changing how the projects are layered, where a request is checked or how it is committed, how the wire records reach the frontend, or how data moves between the laptop, the phones and the station tablets, so that a change fits the architecture decisions already taken.
---

# Architecture

One page per architecture decision that spans the app. Each page holds the decision, its mechanism,
what each side owes in return, and the reason, so a change in one place cannot quietly break
another. A rule in a `CLAUDE.md` names the principle; the page here carries the mechanism. This skill
is never a feature or code documentation page: features are in `features`, recurring tasks in
`how-to`.

| Decision | Page |
|---|---|
| How the projects are layered and what each one may depend on | [references/layers-and-projects.md](references/layers-and-projects.md) |
| One request, one transaction | [references/one-transaction-per-request.md](references/one-transaction-per-request.md) |
| Where a request's shape is checked and where the domain rules live | [references/request-validation.md](references/request-validation.md) |
| The generated types as the boundary between the backend and the frontend | [references/the-wire-boundary.md](references/the-wire-boundary.md) |
| How screens learn that something changed | [references/announcements.md](references/announcements.md) |

## How to use

Open the page for every decision the change touches before editing, and read it again while
reviewing. A change that needs a mechanism this skill does not describe yet adds a page for it in the
same change, with the backend part, the frontend part and the reason, and a row in the table above.
