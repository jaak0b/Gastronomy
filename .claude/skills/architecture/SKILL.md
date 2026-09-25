---
name: architecture
description: Read before changing how data moves between the laptop, the phones and the station tablets, so that a change fits the decisions already taken about announcements and the other mechanisms that span the backend and the frontend.
---

# Architecture

One page per decision that spans both trees. Each page holds the mechanism on the laptop, what the
phone or tablet owes in return, and the reason the decision was taken, so a change on one side
cannot quietly break the other. A rule in a `CLAUDE.md` names the principle; the page here carries
the mechanism.

| Decision | Page |
|---|---|
| How screens learn that something changed | [references/announcements.md](references/announcements.md) |

## How to use

Open the page for every decision the change touches before editing, and read it again while
reviewing. A change that needs a mechanism this skill does not describe yet adds a page for it in the
same change, with the backend part, the frontend part and the reason, and a row in the table above.
