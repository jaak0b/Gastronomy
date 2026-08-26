# System specification

Ordering system for volunteer fire department festivals.

Status: draft for owner review. Version 1 scope. Written to be read end to end by a human, not by a
build tool.

Conventions used in this document:

* "Server" always means the person carrying orders and trays, never the machine. The machine is called
  "the backend" or "the laptop".
* Money is stored and calculated in integer cents. No decimal type appears anywhere.
* Times are stored in UTC and rendered in the laptop's local time zone.
* Every user-facing string in this document is given in German and English. Names the admin types in
  (item names, station names, staff names) are data, not chrome, and are stored once in whatever
  language the fire department uses. The app does not translate them.
* Two numbers appear on paper and on screen, and each has exactly one word in each language. The
  global order number is always "Bestellung 137" or "Order 137". The per-station sequence number is
  always "Bon 042" or "Slip 042". No other form of either number exists anywhere in the product.

Contents:

1. Purpose and scope
2. Domain model
3. State machines
4. Numbering
5. REST API
6. SignalR
7. Printing service
8. Frontend screens
9. Offline and failure behaviour on the phone
10. Configuration and setup
11. Testing strategy
12. Open questions

---

## 1. Purpose and scope

### 1.1 What the product does

A server walks table to table with their own phone, picks items and quantities, types the table name,
reads the running total aloud so the guest can pay cash, and places the order. The backend splits the
order by production location, gives each slice its own numbered slip, and prints it on that location's
thermal printer. Kitchen and bar staff work the pile of slips off exactly as they work a pile of paper
today. A server with a free hand carries the tray out.

The product removes one step from the current paper process: the walk from the table to the kitchen.
Nothing else about the process changes.

### 1.2 Why it exists

Orders get lost. A paper slip falls behind a fridge, a server forgets which table a tray belongs to, or
a slip is written twice and the kitchen produces the same order twice. Every design decision in this
document is answerable to that one problem. Two mechanisms carry the weight:

1. Every slip carries a global order number and a per-station sequence number. A missing number in the
   pile is visible to anyone holding the pile. No screen, no login, no software.
2. The server who placed an order can see, on their own phone, whether the slip actually printed. A
   failure is never allowed to look like a success.

### 1.3 Who uses it

| Role | Device | Technical ability assumed | Trained? |
|---|---|---|---|
| Server | Their own phone, any browser | None | No. Possibly never saw the tool before tonight. |
| Admin | The laptop on site | Can type an IP address if told exactly what to type | Read the setup checklist once |
| Kitchen and bar staff | Nothing | None | Not applicable. They touch no software. |

Production location staff click nothing, wait for nothing, and log into nothing. This is a hard
constraint on every design decision below, and the break-glass page in section 8.10 is the single
narrow exception to it.

### 1.4 Scale

Fewer than 10 production locations. Fewer than 10 servers. A catalog of roughly 20 to 60 items. A few
dozen tables. Peak load is a handful of orders per minute. The system is not designed for scale, and
adding scale is not a goal. It is designed for reliability and for operation by people with little
technical ability.

### 1.5 Non-goals

The following are explicitly out of scope. They are listed so that a future change request can be
answered with "that was decided against", not with a redesign.

| Not in scope | Reason |
|---|---|
| Payments of any kind | Cash is handled by hand at the table. The app takes no payment, stores no payment data, and has no card reader. |
| Guest receipts, invoices, tax documents | Any of these would drag German fiscal law (KassenSichV, TSE, GoBD) into a hobby project. The printed slip is a production instruction for staff, not a receipt for a guest. |
| Kitchen display system | Staff work off a pile of paper. Putting a screen in the kitchen would replace the process instead of removing one walk from it. The break-glass page (section 8.10) does list a station's open orders, and it is not this: it has no printer to compete with when it is being used, nothing on it advances an order through a workflow, and the station reaches for it only when the paper has stopped arriving. |
| Stock, inventory, portion counts | An item is marked sold out by hand, in one tap, by whoever hears that the kitchen has run out. Counting portions is not attempted, because nobody will keep the count correct while serving. |
| Table reservations or floor plans | Tables are moved during the evening. Managing them as objects is more work than the problem is worth. |
| Zones or areas grouping stations | Almost every festival has one kitchen and one bar. Grouping stations into areas made every server perform a shift-start ritual for a case that hardly ever happens. Routing now comes from the item itself, and section 2.6 describes the one remaining choice a server makes. |
| Cloud, remote access, multi-site | There is no internet on site. |
| Accounts, usernames, passwords | Nobody will manage credentials at a festival. Section 2.8 describes what replaces them. |
| Guest self-ordering | The server at the table is the product. |
| Reporting and analytics beyond a list of the evening's orders | Nobody will read it. |
| Native apps, app store distribution, PWA install | There is no secure context over plain HTTP, so no service worker and no install prompt exist. |
| Tray tracking, delivery confirmation | A server carries the tray. The app is not told when it arrives. |
| Cancelling or correcting an order after it is placed | The moment an order is placed, its slip is printing or already lying on the pile at the station. Cancelling in software does nothing to paper that already exists, and a cancel button would tell the server the order is withdrawn while the kitchen carries on cooking it. That illusion is the exact defect this product exists to remove. The honest correction is the one the paper process already uses: walk over and tell the station. Section 3.6 says what the phone tells the server. |
| Deleting or cleaning up a mistaken order | It stays in the database. No money moves through the app and nothing aggregates orders, so a wrong order costs a line in a list the treasurer skims once. A cleanup mechanism would be a second way to make a slip disappear from a screen while it is still on a pile. |

---

## 2. Domain model

### 2.1 Notation

Types are given as they will exist in C#. `Guid` primary keys are generated on the backend, except
`Order.ClientOrderId`, which is generated on the phone. `string(n)` means a maximum length enforced
both in the database schema and in validation. All money is `int` cents.

### 2.2 Entity relationship diagram

```mermaid
erDiagram
    EventSession ||--o{ Order : contains
    EventSession ||--o{ NumberCounter : scopes

    ProductionLocation ||--|| PrinterConfiguration : "prints through"
    ProductionLocation ||--|| PrinterStatus : "last reported"
    ProductionLocation ||--o{ ItemLocationAssignment : "can produce"
    ProductionLocation ||--o{ LocationTicket : receives
    ProductionLocation ||--o{ NumberCounter : scopes

    CatalogItem ||--|{ ItemLocationAssignment : "is produced at"
    CatalogItem ||--o{ OrderLine : "appears as"

    ServerPerson ||--o{ Device : uses
    ServerPerson ||--o{ Order : placed

    EnrolmentCode |o--o| Device : "was redeemed by"
    Device ||--o{ Order : submitted

    Order ||--|{ OrderLine : "consists of"
    Order ||--|{ LocationTicket : "splits into"
    LocationTicket ||--|{ OrderLine : groups
    LocationTicket ||--o{ PrintJob : "is printed by"
    PrintJob ||--|{ PrintAttempt : records

    EventSession {
        Guid Id PK
        string Name
        bool IsPractice
        DateTime StartedAtUtc
        DateTime EndedAtUtc "nullable"
        bool IsActive
    }
    ProductionLocation {
        Guid Id PK
        string Name
        string StationAccessKey "32 hex chars, unique"
        int SortOrder
        bool IsActive
    }
    CatalogItem {
        Guid Id PK
        string Name
        string CategoryName
        int PriceCents
        int SortOrder
        bool IsActive
        bool IsAvailable
    }
    ItemLocationAssignment {
        Guid Id PK
        Guid CatalogItemId FK
        Guid ProductionLocationId FK
    }
    TableSuggestion {
        Guid Id PK
        string Label
        int SortOrder
    }
    ServerPerson {
        Guid Id PK
        string Name
        bool IsActive
        DateTime CreatedAtUtc
    }
    Device {
        Guid Id PK
        Guid ServerPersonId FK
        string DisplayName
        string Language
        byte_array TokenHash
        byte_array TokenSalt
        int TokenIterations
        string TokenAlgorithm
        string TokenLookupId "unique, non-secret"
        DateTime CreatedAtUtc
        DateTime LastSeenAtUtc
        DateTime RevokedAtUtc "nullable"
        string UserAgentSnapshot
    }
    EnrolmentCode {
        Guid Id PK
        byte_array CodeHash
        byte_array CodeSalt
        int CodeIterations
        string CodeAlgorithm
        DateTime CreatedAtUtc
        DateTime ExpiresAtUtc
        DateTime ConsumedAtUtc "nullable"
        Guid ConsumedByDeviceId FK "nullable"
    }
    Order {
        Guid Id PK
        Guid EventSessionId FK
        Guid ClientOrderId "unique, from the phone"
        int GlobalOrderNumber
        Guid ServerPersonId FK
        Guid DeviceId FK
        string TableLabel
        string Note "nullable"
        int TotalCents
        string Status
        DateTime CreatedAtUtc
    }
    OrderLine {
        Guid Id PK
        Guid OrderId FK
        Guid LocationTicketId FK
        Guid CatalogItemId FK
        Guid ChosenProductionLocationId FK "nullable"
        string ItemNameSnapshot
        int UnitPriceCentsSnapshot
        int Quantity
        string Note "nullable"
    }
    LocationTicket {
        Guid Id PK
        Guid OrderId FK
        Guid ProductionLocationId FK
        int LocationSequenceNumber
        string Status
        int ReprintCount
        DateTime CreatedAtUtc
        DateTime ResolvedAtUtc "nullable"
        string ResolutionNote "nullable"
    }
    PrintJob {
        Guid Id PK
        Guid LocationTicketId FK "nullable"
        Guid ProductionLocationId FK
        string Kind
        string Status
        int ProcessId "nullable"
        string FailureReason "nullable"
        Guid RequestedByDeviceId FK "nullable"
        DateTime CreatedAtUtc
        DateTime CompletedAtUtc "nullable"
    }
    PrintAttempt {
        Guid Id PK
        Guid PrintJobId FK
        int AttemptNumber
        string Outcome
        string Phase
        int BytesWritten
        string TransportDetail
        string PrinterStatusSnapshotJson
        DateTime StartedAtUtc
        DateTime EndedAtUtc
    }
    PrinterConfiguration {
        Guid Id PK
        Guid ProductionLocationId FK
        string TransportKind
        string Host "nullable"
        int Port
        string AgentIdentifier "nullable"
        int CharactersPerLine
        string CodePageName
        int ConnectTimeoutSeconds
        int JobTimeoutSeconds
        int HeartbeatSeconds
        bool IsEnabled
    }
    PrinterStatus {
        Guid ProductionLocationId PK
        bool IsOnline
        bool IsPaperEnd
        bool IsPaperNearEnd
        bool IsCoverOpen
        bool IsInErrorState
        bool IsFaulty
        string LastDetail
        DateTime LastChangedAtUtc
        DateTime LastHeardFromAtUtc
    }
    NumberCounter {
        string CounterKind PK
        Guid EventSessionId PK "nullable"
        Guid ProductionLocationId PK "nullable"
        string PrinterEndpointKey PK "nullable"
        int NextValue
    }
```

`TableSuggestion` has no relationship to anything. It is a list of words the phone offers as buttons,
and section 2.7 explains why that is deliberate.

### 2.3 EventSession

An operating period, normally one festival evening. It exists for one reason: it scopes the numbering
counters, so a fresh evening starts at slip number 1 without making yesterday's numbers ambiguous.

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| Name | string(60) | The admin types it, for example "Samstagabend". Defaults to the current date. |
| IsPractice | bool | A practice run. Its orders are excluded from the treasurer's export and a station on the test printer is expected rather than an error. |
| StartedAtUtc | DateTime | |
| EndedAtUtc | DateTime? | Set when the next session starts |
| IsActive | bool | Exactly one row may be active |

Invariants:

* Exactly one `EventSession` has `IsActive = true` at any time. Starting a new session ends the
  previous one in the same transaction.
* An order always belongs to the session that was active when it was accepted. Sessions are never
  reassigned.
* Ending a session deletes nothing. The SQLite file is the whole history and section 10.5 describes the
  backup.

**Starting a session is guarded, because starting one by accident during service is the one action that
breaks both safety mechanisms at once.** Numbering would restart at 1 into a pile that already holds a
001, so the same stack would carry two different slips numbered 042 and neither would be marked as a
reprint. The gap mechanism and the reprint mechanism would both be dead in the same second. The backend
therefore refuses to start a session when any of these is true, and each refusal names what to do:

| Condition | What the admin is told |
|---|---|
| A ticket of the current session is not in a final state | How many, with a link to the order list, and that they must be settled first |
| An `Unknown` question of the current session is unanswered | The same, because those questions vanish from every phone when the session changes |
| An order was accepted within the last hour | The start is allowed only after the admin types the new event's name into a confirmation field |
| An active production location is still on the test printer | Which stations, and the offer to start a practice run instead |

A practice run is exempt from the last condition, because the test printer is the point of it.

### 2.4 ProductionLocation

A kitchen or a bar. Exactly one printer per location. There is no grouping above this: a location is
the whole of the site structure the system models.

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| Name | string(40) | Printed in large type at the top of every slip |
| StationAccessKey | string(32) | Random hex, unique. The only thing protecting the break-glass page. |
| SortOrder | int | The order stations appear in, on the phone and in the admin |
| IsActive | bool | Soft delete only |

Invariants:

* Every active location has exactly one `PrinterConfiguration` row and exactly one `PrinterStatus` row.
  Both are created with the location and never exist without it.
* A location cannot be deactivated while it has tickets that are not in a final state.
* **A location cannot be deactivated while it is the last active location of any active item.** The
  refusal names those items. This is what keeps the candidate set in section 2.6 non-empty, and it is
  the reason acceptance never has to invent a station.
* `StationAccessKey` is generated on creation and can be regenerated by the admin, which immediately
  invalidates the old break-glass link. It is stored in plaintext because it has to be rendered back
  into a URL and a QR code whenever the admin asks for the station card. It is never written to the
  request log, and the log's request line for `/station/...` paths is truncated before the key.

### 2.5 CatalogItem

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| Name | string(60) | Printed on the slip |
| CategoryName | string(40) | Free text, used only to group buttons on the phone |
| PriceCents | int | Zero is allowed, for example for tap water |
| SortOrder | int | |
| IsActive | bool | Not on this festival's menu at all. Soft delete, because orders reference items. |
| IsAvailable | bool | On the menu, but sold out tonight. Flipping it pushes to every phone at once. |

**These two flags are two different things, set by two different people at two different times, and the
document never uses one word for both.**

| | `IsActive` false, "deaktiviert" / "deactivated" | `IsAvailable` false, "ausverkauft" / "sold out" |
|---|---|---|
| What it means | The item is not on this festival's menu | The item is on the menu and has run out tonight |
| Who sets it | The admin, at the laptop, setting up the event | Whoever hears that the kitchen has run out |
| When | Before the event, between events | During service, and very often reversed twenty minutes later when somebody finds another crate |
| How | The item editor, refused during a live event | One toggle in the item list, one tap each way, no form and no dialog |
| On the phone | The item is not in the catalog at all | The item stays in the list, greyed, not selectable, labelled `catalog.soldOut` |

Sold out is reversed constantly by somebody who is busy, so it is one tap and nothing else. Deactivation
is a considered edit made once, so it lives in the editor where a considered edit belongs.

**A sold-out item stays on the phone rather than disappearing from it.** An item that vanishes silently
sends a server hunting through categories for something that was there a minute ago, wondering whether
they are on the wrong screen. Greyed out with "Ausverkauft" underneath answers that question at the
moment it is asked, which is the moment the guest asks for it.

Invariants:

* **An item cannot be saved with zero production locations, and an item with zero production locations
  never appears in the catalog.** The admin form refuses to save and says why, and the API answers 422.
  This is the invariant the whole routing design rests on.
* `PriceCents >= 0`.
* Editing a name or price never changes an existing order. Every `OrderLine` carries a snapshot.
* **Deactivating an item is refused while a non-practice session is active**, and the refusal names the
  sold-out toggle as the tool for tonight. Deactivation is a between-events operation. Without this rule
  a phone holding a cached catalog could offer an item that has left the menu entirely, and the catalog
  the phone holds would be describing a different festival from the one it is standing in.
* **Marking an item sold out is allowed at any time and never refused.** It changes nothing about
  orders that already exist, and an order carrying a line for it is still accepted (section 5.4).

### 2.6 ItemLocationAssignment and routing

Which locations are capable of producing an item. Bratwurst is assigned to the kitchen. Beer at a site
with two bars is assigned to both.

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| CatalogItemId | Guid | |
| ProductionLocationId | Guid | |

Invariants:

* `(CatalogItemId, ProductionLocationId)` is unique.
* An item's **candidate set** is its assigned locations that are active. By the invariants in sections
  2.4 and 2.5 the candidate set of an orderable item is never empty, so routing never has a nothing
  case and there is no fallback rule anywhere in the system.

**The routing rule, and the only routing rule in the system:**

1. **One candidate.** The line routes there. The server is never asked, and no station control is
   rendered for that item. This is the normal case at a site with one kitchen and one bar, and it is
   completely invisible.
2. **More than one candidate.** The server chooses, on the phone, at the moment the line is added.
   The choice is stored on `OrderLine.ChosenProductionLocationId`, is shown on the line in the review
   screen, and is changeable until the order is sent.

The choice belongs to the order line. It is not a session setting, not a device setting, and not a
shift setting, and nothing about it is remembered for the next line or the next order.

The one edge the backend has to answer: the chosen location may have been deactivated between the
catalog fetch and the submission. In that case the line routes to the lowest `SortOrder` candidate that
is still active, `ChosenProductionLocationId` keeps what the server actually chose, and the slip prints
the intended station under the lines so the receiving station can see where it was meant to go. The
order is never rejected for a routing reason.

This rule lives in one class, `OrderRoutingResolver`, and is used by the order submission path, by the
admin preview in the assignment screen, and by tests. It is not reimplemented anywhere.

### 2.7 Table naming: free text, with suggestions

**Decision: a table is a free text label on the order, not an entity that orders point to.** A
`TableSuggestion` table exists purely to prefill a list of buttons on the phone.

Justification. At a festival the tables are beer benches. They get moved, added, and joined together
during the evening, and guests sit at whatever is standing. If the order required a foreign key into a
table list, then the first table that is not in the list blocks an order, and blocking an order is the
exact failure this product exists to prevent. Free text can never block. The suggestion list gives the
common case one tap and keeps spellings consistent, which is all the structure that is actually needed:
nothing in the system aggregates by table.

| TableSuggestion field | Type | Notes |
|---|---|---|
| Id | Guid | |
| Label | string(40) | For example "Tisch 12" |
| SortOrder | int | |

Invariants:

* `Order.TableLabel` is required, trimmed, collapsed to single spaces, between 1 and 40 characters.
* `Order.TableLabel` need not match any suggestion.

"Tisch 12", "tisch 12" and "T12" are three labels for one table, and that is accepted rather than
normalised, because nothing aggregates by table and normalising would be work for no gain.

After the first evening the department has a real list for free: the admin screen offers to add every
distinct label that servers actually typed during the last session, so the second festival starts with
suggestions that match how this crew names its tables.

### 2.8 ServerPerson, Device, EnrolmentCode

There are no usernames and no passwords anywhere in the product.

**ServerPerson** is a name to print on the slip, the owner of the evening's orders, and the entry in
the admin list.

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| Name | string(40) | |
| IsActive | bool | |
| CreatedAtUtc | DateTime | |

An order belongs to a `ServerPerson`, not to a phone. That is what makes a flat battery, a revoked
phone, or a re-enrolment survivable: the evening's history follows the human, and the questions in
section 3.4 stay answerable by the person who can walk to the station.

**Device** is one enrolled phone.

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| ServerPersonId | Guid | Who is carrying it. The admin can reassign it when a phone changes hands between shifts. |
| DisplayName | string(40) | Defaults to the person's name, editable in admin |
| Language | string(2) | `de` or `en`. Every message the backend sends to this phone is rendered by the phone in this language. |
| TokenLookupId | string(32) | Non-secret random id, sent with every request so the backend can find the one row to verify against |
| TokenHash, TokenSalt, TokenIterations, TokenAlgorithm | | PBKDF2-HMAC-SHA512, per-token random salt, iteration count and algorithm name stored alongside the hash so both can be raised later without invalidating existing devices |
| CreatedAtUtc, LastSeenAtUtc | DateTime | |
| RevokedAtUtc | DateTime? | Non-null means every request from this device is rejected |
| UserAgentSnapshot | string(200) | So the admin can tell two phones apart in the list |

**EnrolmentCode** is a single-use, short-lived credential shown on the laptop.

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| CodeHash, CodeSalt, CodeIterations, CodeAlgorithm | | Same hashing scheme as device tokens |
| CreatedAtUtc, ExpiresAtUtc | DateTime | Lifetime 5 minutes |
| ConsumedAtUtc, ConsumedByDeviceId | | Set atomically by the first successful redemption |

The six digits and the QR URL of the code currently on screen live in the rotation service's in-memory
state and are never written to the database. Persisting them beside their own hash would make the
hashing decorative.

Invariants:

* Redemption is a single atomic update: a code moves from unconsumed to consumed in the same
  transaction that creates the device, so a photographed QR code cannot enrol a second phone.
* Codes rotate about every 30 seconds while the "Set up a server phone" screen is open, so a whole crew
  can enrol during one briefing. Rotation does not invalidate codes that are still inside their 5
  minute window, because a phone may be slow to open the page.
* Verification is a linear scan over the unconsumed, unexpired codes, of which there are at most a
  handful. This is why a hashed short code needs no plaintext lookup index.
* **A code that is already consumed or expired answers 410 and is not counted as a failed attempt.**
  Eight people scanning the same displayed QR within two seconds is the design goal, not an attack, and
  seven of them get a 410 as a matter of course.
* **A wrong code is counted per source address, and only that address is locked.** Ten wrong codes from
  one address within five minutes lock that address for five minutes. Codes already issued to other
  people are never invalidated by somebody else's mistyping, because that is what turns one person's
  bad thumb into everyone's problem on an open network.
* The admin can lift every lock from the enrolment screen, and the screen lists the locked addresses so
  the person at the laptop can see that it happened at all.
* Tokens are never logged, never returned after enrolment, and never recoverable. A lost phone is
  handled by revoking and enrolling again.
* Revoking a device sets `RevokedAtUtc` and pushes a SignalR message to that device, which clears its
  token and returns to the enrolment screen. **The half-built order on that phone is kept**, for the
  reasons in section 9.4.

### 2.9 Order and OrderLine

| Order field | Type | Notes |
|---|---|---|
| Id | Guid | |
| EventSessionId | Guid | The active session at acceptance |
| ClientOrderId | Guid | Generated on the phone, unique index. This is the idempotency key. |
| GlobalOrderNumber | int | Allocated at acceptance, unique within the session |
| ServerPersonId | Guid | Who placed it. This is what the order list is scoped by. |
| DeviceId | Guid | Which phone submitted it, for the admin's diagnosis only |
| TableLabel | string(40) | |
| Note | string(200)? | An order level note, printed on every station's slip |
| TotalCents | int | Sum of `Quantity * UnitPriceCentsSnapshot`, stored so the slip and the phone can never disagree |
| Status | string | See section 3.1 |
| CreatedAtUtc | DateTime | |

| OrderLine field | Type | Notes |
|---|---|---|
| Id | Guid | |
| OrderId | Guid | |
| LocationTicketId | Guid | The slice this line was routed into |
| CatalogItemId | Guid | |
| ChosenProductionLocationId | Guid? | Null when the item had one candidate. Set when the server chose. |
| ItemNameSnapshot | string(60) | |
| UnitPriceCentsSnapshot | int | |
| Quantity | int | 1 to 99 |
| Note | string(100)? | For example "ohne Zwiebeln", printed under the line |

Invariants:

* An order has at least one line.
* `Quantity >= 1`.
* `line.LocationTicket.OrderId == line.OrderId` for every line.
* `TotalCents` equals the recomputed sum. This is asserted in the acceptance transaction and covered by
  a test, because the total is the only number a guest hears out loud.
* An accepted order is immutable except for its status. Lines are never added, removed, or edited, and
  an order is never cancelled or deleted.
* `ClientOrderId` carries a unique index and is the whole duplicate protection for a resubmission.
  Section 9.3 describes it from the phone's side.

### 2.10 LocationTicket

The per-location slice of an order. This is the thing that gets printed, and the thing that carries a
sequence number.

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| OrderId | Guid | |
| ProductionLocationId | Guid | |
| LocationSequenceNumber | int | Allocated at acceptance, unique per location per session |
| Status | string | See section 3.2 |
| ReprintCount | int | Zero on the first print. Every reprint slip says so in its header. |
| CreatedAtUtc | DateTime | Both windows in section 3.2 are measured from here: the give-up window, which does not run while the cause is known, and the outer bound, which always runs |
| ResolvedAtUtc | DateTime? | When a human answered an unknown outcome or acknowledged a manual handover |
| ResolutionNote | string(200)? | Who answered and what they answered |

Invariants:

* `(OrderId, ProductionLocationId)` is unique. An order produces at most one ticket per location, and
  every line for that location goes on it.
* A ticket has at least one line.
* `LocationSequenceNumber` is allocated exactly once and never changes, including on reprints.

### 2.11 PrintJob and PrintAttempt

A `PrintJob` is one intent to put a sheet of paper in a station's tray. A `PrintAttempt` is one
connection level try inside that intent. Splitting them is what makes "we sent bytes and do not know
what happened" a recordable fact rather than a guess.

| PrintJob field | Type | Notes |
|---|---|---|
| Id | Guid | |
| LocationTicketId | Guid? | Null only for `Test`, which belongs to a printer rather than an order |
| ProductionLocationId | Guid | Which printer's worker owns it. Always set, including for `Test`. |
| Kind | string | `Initial`, `Reprint`, or `Test` |
| Status | string | See section 3.3 |
| ProcessId | int? | 1 to 9999 from that printer's persisted counter. **Null until step 6 of section 7.4**, which is the first moment a value is legitimate. Sent with `GS ( H` and echoed back by the printer when it has finished processing the job. |
| FailureReason | string? | `PaperEnd`, `CoverOpen`, `Unreachable`, `Timeout`, `SocketDropped`, `PrinterError`, `StationDisabled`, `StationFaulty`, `TicketResolvedByHuman` |
| RequestedByDeviceId | Guid? | Null for the initial job, set when a human asked for a reprint |
| CreatedAtUtc, CompletedAtUtc | | |

| PrintAttempt field | Type | Notes |
|---|---|---|
| Id | Guid | |
| PrintJobId | Guid | |
| AttemptNumber | int | 1 based |
| Outcome | string | `Confirmed`, `Blocked`, `Unreachable`, `SocketDropped`, `Timeout`, `PrinterError` |
| Phase | string | Where in the job sequence the attempt ended: `Connecting`, `PreflightCheck`, `Sending`, `AwaitingEcho`. Diagnostics only. Section 7.6 says why it never changes an outcome. |
| BytesWritten | int | **Bytes handed to the socket for this job's payload.** Not bytes the printer acknowledged, which nothing can know. Any value above zero is treated as possibly delivered, which is the conservative direction. |
| TransportDetail | string(400) | The socket error text or the transport's own message. **Admin-only, and the boundary is a rule rather than a habit:** it is serialized in exactly one place, the print-history response in section 5.5, which is served only to the laptop. No response to a phone and no SignalR payload carries it, in raw or summarized form. A device-facing failure is described by a message key and its parameters, never by transport text. |
| PrinterStatusSnapshotJson | string | The ASB or `DLE EOT` state at the end of the attempt |
| StartedAtUtc, EndedAtUtc | | |

Invariants:

* At most one `PrintJob` per ticket is in a non-final state at any moment.
* **A job is only ever sent while its ticket is still waiting for it.** A job whose ticket has left
  `Queued` and `Blocked` ends `Failed` with `FailureReason: TicketResolvedByHuman` and zero bytes
  written. Section 7.4 step 2 gives the check and section 3.2 gives the reason: this is what makes it
  impossible for one ticket to be both handled by a human and printed by a machine.
* `ProcessId` is null until step 6 of section 7.4 and never changes afterwards. A job that ended before
  step 6 keeps it null, which is the honest record that nothing was sent. A non-nullable column would
  have forced a placeholder value onto such a job, and a placeholder that a returning echo could match
  is exactly the stale match the counter exists to prevent.
* An attempt with `BytesWritten > 0` never leads to an automatic retry. Only a human can ask for the
  reprint.
* Attempts are append only. Nothing in the print history is ever updated after it ends.
* A process id echo is accepted only on the socket that sent that job. An echo arriving on a new
  connection is discarded, because the counter cycles and a stale match would turn a lost slip into a
  reported success.

### 2.12 PrinterConfiguration and PrinterStatus

| PrinterConfiguration field | Type | Default | Notes |
|---|---|---|---|
| ProductionLocationId | Guid | | One to one |
| TransportKind | string | `Mock` | `Network`, `Agent`, or `Mock`. This is the only place the choice exists. |
| Host | string(64)? | | IP address of the printer or its WiFi bridge. **Two locations may name the same address, and that is a supported configuration rather than a mistake.** See below. |
| Port | int | 9100 | |
| AgentIdentifier | string(64)? | | Reserved for the deferred Pi agent |
| CharactersPerLine | int | 48 | Font A on 80 mm paper |
| CodePageName | string | `PC858` | ESC/POS page 19, which contains the German umlauts and the Eszett |
| ConnectTimeoutSeconds | int | 5 | |
| JobTimeoutSeconds | int | 90 | Matches the printer's own connection timeout |
| HeartbeatSeconds | int | 10 | `DLE EOT n=4` poll interval |
| IsEnabled | bool | true | A disabled printer holds its tickets rather than failing them straight away, and section 3.2 bounds how long that can last |

| PrinterStatus field | Type | Notes |
|---|---|---|
| ProductionLocationId | Guid | |
| IsOnline | bool | A connection is open and the last heartbeat answered |
| IsPaperEnd, IsPaperNearEnd, IsCoverOpen, IsInErrorState | bool | Decoded from ASB and `DLE EOT` |
| IsFaulty | bool | Set by the circuit breaker in section 7.6. The worker has stopped attempting until a human acts. |
| LastDetail | string(200) | |
| LastChangedAtUtc, LastHeardFromAtUtc | DateTime | |

**Two locations may point at one printer, and that is the answer to a dead printer.**

There is no fallback printer field, no secondary printer, and no automatic rerouting anywhere in this
product. What exists instead is that a location's printer configuration is an address the admin can
change at any time, including to an address another location is already using. When the marquee bar's
printer dies, the admin puts the kitchen printer's address into the marquee bar's configuration, and
from that moment both stations' slips come off the kitchen machine, interleaved. Somebody carries the
bar's slips thirty metres, which is exactly the process the department ran before there was software.

What that configuration means concretely:

* **The two locations keep separate sequence numbering.** `LocationSequenceNumber` is allocated per
  location (section 4.1) and nothing about it looks at printers. The kitchen's slips still run 41, 42,
  43 and the bar's still run 17, 18, 19 on the same roll, so each station's gap detection keeps working
  on its own numbering exactly as before. Section 4.2 states this as a rule rather than leaving it as a
  consequence somebody has to derive.
* **One machine still means one connection.** The worker in section 7.3 is started per distinct printer
  endpoint rather than per location, so two locations sharing an address share one worker and one
  socket. This is the whole reason that rule is written the way it is.
* **Which station a slip is for is unmistakable on the paper.** The station name is the first thing on
  the slip, centred, bold, and at double width and double height (section 7.7), so it is legible from
  across a marquee before anybody reads a single item. That was already true of every slip and no change
  to the layout is needed for this case.
* **The admin screen says so on both locations**, with `admin.printers.shared`, so a shared address
  reads as a decision somebody made rather than as two rows that happen to look alike.

Invariants:

* Status is written only by that printer's worker, so there is one writer per row and no contention.
  When two locations share a printer, one worker writes both `PrinterStatus` rows from the one socket
  it owns, so the two rows agree and there is still exactly one writer each.
* Every status change pushes a SignalR event. Clients never poll for printer state.

### 2.13 NumberCounter

One row per counter, with a composite key rather than a formatted string, so the database enforces the
relationship instead of a naming convention. See section 4.

| Field | Type | Notes |
|---|---|---|
| CounterKind | string(20) | `GlobalOrder`, `LocationSequence`, or `PrinterProcessId`. Part of the primary key. |
| EventSessionId | Guid? | Set for `GlobalOrder` and `LocationSequence`, null for `PrinterProcessId`. Part of the primary key. |
| ProductionLocationId | Guid? | Set for `LocationSequence`, null for `GlobalOrder` and `PrinterProcessId`. Part of the primary key. |
| PrinterEndpointKey | string(96)? | Set for `PrinterProcessId`, null for the other two. Part of the primary key. |
| NextValue | int | |

`PrinterProcessId` lives here rather than in memory because a counter that restarts at 1 after a crash
can match a stale echo, and a stale match reports a lost slip as printed.

**The process id counter belongs to the printer, not to the location**, which is why its key is the
endpoint and not a location id. Two locations sharing one machine (section 2.12) share one counter, and
they have to. Two independent counters feeding one socket can hand out the same value twice: a job that
timed out at 90 seconds can have its echo arrive late while the next job is already waiting for an
echo, and if that next job came from the other location's counter with the same number, the worker would
accept the dead job's echo as the live job's confirmation and report a lost slip as printed. One counter
per socket makes that arithmetically impossible.

`PrinterEndpointKey` is the canonical form of the endpoint, `TransportKind|Host|Port|AgentIdentifier`,
with the empty string for the parts a transport does not use. It is a string because an endpoint has no
id of its own, it is built in one place alongside the worker that uses it, and it is never parsed back
apart.

---

## 3. State machines

Two state machines exist, plus one projection. The ticket state is a machine driven by print jobs and
human answers. The print job state is the machine that touches hardware. The order status is not a
machine at all: it is recomputed from the ticket states, and section 3.1 gives the table that computes
it.

### 3.0 The principle every rule in this section follows

**If we know why a slip is not printing, the system waits and says why. It gives up only when nobody
knows what is happening.**

Paper out, an open cover, a station switched off at the laptop, and a test printer that cannot write
its folder are known causes. Each one names a thing a person can do, each one is already being walked
towards by the time the message lands, and the slip prints by itself the moment somebody acts. A
printer that has stopped answering, or one that answers and has stopped making sense, is a different
kind of event: nobody knows what is wrong, nobody is walking anywhere, and the order has to be carried
to the station by a human before the food is late.

Earlier drafts of this document treated those two the same. Both ran the same five minute clock, and
both ended in the same "the order failed" message on the same phones. That is wrong in both directions
at once. It cries wolf during an ordinary paper change, which is roughly a minute of work at a station
where somebody is already holding the new roll, and after two of those an evening the crew learns that
the failure message means nothing. Where the concrete rules below differ from each other, this is the
difference they are expressing:

| | Cause known and fixable | Cause unknown |
|---|---|---|
| Examples | Paper end, cover open, station disabled, the mock's folder unwritable | Unreachable, timing out, two unknown outcomes in a row, a mechanical error |
| The give-up window | Suspended, because giving up would be wrong | Running |
| What the phone says | The cause and the one action that fixes it, plus that the slip prints by itself afterwards | To carry the order to the station in person |
| What escalates | Visibility: the count of waiting slips on every phone's station banner, and a named row in the admin overview | The ticket itself, to `Failed` |
| What stops it parking forever | The outer bound in section 3.2, which no cause suspends | The give-up window |

### 3.1 Order status, a projection of its tickets

An order has no draft state on the backend. Until it is accepted it exists only on the phone, as the
draft cart described in section 9.2.

`Order.Status` is computed by `OrderStatusCalculator` from the current ticket statuses and written in
the same transaction as the ticket change that caused it. It is stored rather than derived on read so
that a query for "orders that need checking" is one indexed lookup, and it is computed nowhere else.

Earlier drafts of this document drew the order status as a diagram with transitions. That was wrong,
and it hid real contradictions: a reprint takes a ticket from `Printed` back to `Queued`, and no
diagram of the order state had an arrow for that. Recomputation has no such problem, because there are
no arrows to be missing.

**The calculator. The first row that matches wins.**

| # | Condition over the order's tickets | Order status |
|---|---|---|
| 1 | Any ticket is `Unknown`, `Failed`, or `Blocked` | `NeedsAttention` |
| 2 | Any ticket is `PrintedOnTestPrinter` and the session is not a practice run | `NeedsAttention` |
| 3 | Every ticket is `Printed`, `HandledOnPaper`, or `PrintedOnTestPrinter` | `Printed` |
| 4 | Any ticket is `Printing` | `Printing` |
| 5 | Otherwise, which means at least one ticket is `Queued` | `Accepted` |

Row 1 puts `Blocked` on the attention list on purpose. A station with no paper needs a human, and the
message that reaches the phone says the slip prints by itself once the roll is in, so the row clears
itself the moment somebody acts.

Row 2 is the trap in section 3.5: a station left on the test printer reports every order as printed
while nothing reaches any pile. During a practice run that is the point; during a real evening it is a
silently dropped order and it is reported as one.

The table is total: every combination of ticket states falls into exactly one row, and a new ticket
state is a new row rather than a new arrow somebody forgets to draw.

States:

| State | Meaning | What the server sees on the phone |
|---|---|---|
| `Accepted` | Stored, numbered, split into tickets. Nothing has printed yet. | "Wird gedruckt" / "Printing" |
| `Printing` | At least one printer is working on it. | "Wird gedruckt" / "Printing" |
| `Printed` | Every ticket is on paper, or a human confirmed it is on paper. | "Gedruckt" / "Printed" |
| `NeedsAttention` | At least one ticket failed, is blocked, or is unresolved. | The specific message from section 8.8, with a cause and a next step |

Rules and failure behaviour:

* **An order is never rejected because of a printer.** Acceptance persists the order and its numbers.
  Printing happens afterwards. A dead printer produces `NeedsAttention`, never a lost order.
* **An accepted order is never edited, cancelled, or deleted.** There is no `Voided` state and no
  cancel action anywhere in the product. Section 3.6 says why, and what the server does instead.
* **The same order submitted twice is one order.** A resubmission carrying a `ClientOrderId` that was
  already accepted returns the original order with its original numbers and creates nothing. Section
  9.3 describes the mechanism and the reason it is load-bearing.

### 3.2 LocationTicket

```mermaid
stateDiagram-v2
    [*] --> Queued : order accepted
    Queued --> Printing : worker claimed the job
    Printing --> Printed : printer echoed the process id
    Printing --> PrintedOnTestPrinter : the test printer rendered it
    Printing --> Unknown : socket dropped or the echo timed out after bytes were written
    Printing --> Queued : attempt failed before any byte was written
    Queued --> Blocked : pre-flight status says paper end or cover open
    Blocked --> Queued : printer reports paper loaded and cover closed
    Queued --> Failed : the give-up window expired
    Queued --> Failed : the outer bound was reached
    Blocked --> Failed : the outer bound was reached
    Unknown --> Printed : a human answered that the slip is on the pile
    Unknown --> Queued : a human answered that the slip is missing, reprint queued
    Failed --> Queued : a human asked for a reprint
    Failed --> HandledOnPaper : station staff acknowledged it on the break-glass page
    Unknown --> HandledOnPaper : station staff acknowledged it on the break-glass page
    Blocked --> HandledOnPaper : acknowledged at a station that cannot print
    Queued --> HandledOnPaper : acknowledged at a station that cannot print
    Printed --> Queued : a human asked for a reprint
    PrintedOnTestPrinter --> Queued : a human asked for a reprint
    Printed --> [*]
    HandledOnPaper --> [*]
```

| State | Meaning |
|---|---|
| `Queued` | Waiting for its printer's worker. Safe to retry, because no bytes have reached the printer. |
| `Blocked` | The printer answered, and it has no paper or an open cover. Nothing was sent. |
| `Printing` | Bytes are being written, or the process id echo is being waited for. |
| `Printed` | The printer echoed the process id, or a human confirmed the slip is on the pile. |
| `PrintedOnTestPrinter` | The station is on the test printer. The slip exists as a file on the laptop and nowhere else. |
| `Unknown` | Bytes were written and the outcome is genuinely not knowable from here. |
| `Failed` | Nothing was sent, and either the give-up window expired or the ticket hit the outer bound. The reason says whether the printer was unreachable, switched off, out of paper, or declared faulty. |
| `HandledOnPaper` | The station saw the order on the break-glass page and is producing it. The slip will not be chased any further, and no job for it will ever be sent. |

**The give-up window is 5 minutes, and it runs from `LocationTicket.CreatedAtUtc`, not from the first
attempt.** That distinction matters: a ticket queued behind twenty others at a station that answers
slowly is failing from the guest's point of view whether or not the worker has reached it yet. Five
minutes was chosen because it is roughly how long a volunteer takes to walk to the bar and back, so a
ticket that recovers on its own recovers before anyone acts on the message.

**The window does not run while the cause is known.** This is section 3.0 as a concrete rule. The clock
is suspended for a ticket whose station is held by one of these four conditions, and it resumes the
moment the condition clears without the elapsed time being reset:

| Condition | How it is read |
|---|---|
| Paper end | `PrinterStatus.IsPaperEnd` |
| Cover open | `PrinterStatus.IsCoverOpen` |
| Station switched off at the laptop | `PrinterConfiguration.IsEnabled` is false |
| The test printer's folder cannot be written | `PrinterStatus.IsInErrorState` on a `Mock` transport |

Every other reason a slip has not printed runs the clock: unreachable, timing out, a station declared
faulty, and a mechanical error the printer reports. The concrete failure the suspension prevents is
ordinary: the paper runs out, somebody walks off to find a roll, and at a busy bar the next four
minutes bring another ten orders. Under the old rule all of them reached `Failed` and ten servers were
told their order had failed, sixty seconds before the roll went in and all ten printed correctly.

**The outer bound is 20 minutes from `LocationTicket.CreatedAtUtc`, and no cause suspends it.** A
ticket that reaches 20 minutes goes to `Failed` whatever is holding it, with `FailureReason` naming the
cause, and the phone gets `ticket.failedAfterWaiting`, which tells the server to announce the order at
the station in person. Twenty minutes is longer than a paper change takes even when the roll has to be
fetched from a car, and it is shorter than the time a guest waits before asking where the food is, so a
ticket that fails at the bound still fails early enough for the walk to be worth making.

**Escalation between five minutes and the bound is visibility, not failure.** A ticket whose cause is
known and unfixed does not stay quiet while it waits:

1. **From the moment it blocks.** The ticket is `Blocked`, its order is `NeedsAttention` by row 1 of
   section 3.1, and the phone shows the cause with the one action that fixes it and the sentence that
   the slip prints by itself afterwards. Nothing about this is new.
2. **Past five minutes.** Every phone's station banner gains the count of slips waiting at that station
   (`header.stationWaiting`), and the admin overview gains a row naming the station, the count and how
   long they have waited (`admin.overview.stationBlocked`). The station's own break-glass page has been
   listing them since they were accepted (section 5.6).
3. **At twenty minutes.** `Failed`, on every affected phone, with the walk-over message.

**Nothing parks forever, and the outer bound is what guarantees it.** A ticket at a switched off
station, a ticket behind a jam, and a ticket at a station nobody has looked at since 21:00 all reach
`Failed`. A ticket that never leaves `Queued` while a phone says "Wird gedruckt" all evening is the
exact shape of a silently dropped order, and suspending the give-up window would have reintroduced it
had the bound not been specified in the same breath.

**A ticket is never handled by a human and printed by a machine as well.** The moment a ticket leaves
`Queued` or `Blocked`, any job still queued for it is dead: section 7.4 step 2 re-reads the ticket
inside the transaction that claims the job, and a job whose ticket has moved on ends `Failed` with
`FailureReason: TicketResolvedByHuman` and zero bytes written. The race in the other direction is
closed by the claim itself, which sets the ticket to `Printing` in that same transaction. So either the
human wins and the worker finds the ticket already resolved, or the worker wins and the human's
acknowledge gets a 409 saying the slip is printing. There is no window between the two, which is what
keeps a recovering printer from putting a second copy of an order onto a pile that a station is already
cooking from.

`HandledOnPaper` reached from `Unknown` is the one case where a slip may physically exist, and that is
the station's own judgment rather than a gap in this rule: the station looked at its pile, found
nothing, and is making the order from the screen. It is the same answer as "the slip is missing" with
the reprint declined, and no reprint is queued. Reprint is not offered from `HandledOnPaper` at all
(section 5.4).

### 3.3 PrintJob

This is the state machine that touches hardware, and the one that decides whether a retry is safe.

```mermaid
stateDiagram-v2
    [*] --> Queued : job created
    Queued --> PreflightCheck : worker picked it up and the connection is open
    PreflightCheck --> Blocked : ASB says paper end or cover open
    Blocked --> Queued : ASB clears
    PreflightCheck --> Queued : connection lost before any byte was written
    PreflightCheck --> Sending : status is clean
    Sending --> AwaitingEcho : all bytes written, GS ( H marker sent
    Sending --> Queued : socket dropped before the first byte
    Sending --> Unknown : socket dropped part way through the write
    AwaitingEcho --> Confirmed : printer returned the process id
    AwaitingEcho --> Unknown : socket dropped
    AwaitingEcho --> Unknown : 90 second job timeout expired
    Queued --> Failed : give-up window expired without reaching the printer
    Blocked --> Failed : give-up window expired or the outer bound was reached
    Queued --> Failed : the station was declared faulty
    Queued --> Failed : its ticket is no longer waiting for it
    Blocked --> Failed : its ticket is no longer waiting for it
    Unknown --> ResolvedPrinted : human answered that the slip is there
    Unknown --> ResolvedMissing : human answered that the slip is missing
    Confirmed --> [*]
    Failed --> [*]
    ResolvedPrinted --> [*]
    ResolvedMissing --> [*]
```

Every state in this machine has at least one producer and at least one way out. An earlier draft
declared a `Cancelled` outcome in four separate places without a single transition that could reach it,
which is how a state that nothing can enter survives review.

**The two `SocketDropped` arrows are drawn separately and end in the same place on purpose.** A drop
during `PreflightCheck` and a drop during `Sending` before the first byte are different events on the
wire and are recorded as such: `PrintAttempt.Phase` carries which one it was, so the log answers "where
was it when the marquee's WiFi went" without guessing. Neither one changes what the system does,
because the only fact that decides whether a retry is safe is `BytesWritten`, and it is zero in both.
Section 7.6 states that as a rule, and its table maps by outcome and byte count rather than by phase
for exactly that reason.

### 3.4 The unknown outcome, stated directly

The printer holds one connection at a time and drops it after 90 seconds of inactivity. If the socket
dies after the first byte of a job has been written, then from the backend's position the job is in
exactly one of two states and there is no command that distinguishes them: the slip is lying on the
pile, or it is not.

There is no ESC/POS query that answers "did job 42 print". `DLE EOT` reports paper, cover, and error
state; it does not report job history. So:

* **A job that wrote bytes and lost its confirmation enters `Unknown`. It is never retried
  automatically.** An automatic retry here is exactly how a station prints one order twice.
* **`Unknown` is resolved by a human, and only by a human.** The question is asked on the phone of the
  server who placed the order, because that server can walk to the station and look at the pile. It is
  also answerable in the admin order list on the laptop, through an endpoint that exists (section 5.5)
  rather than being promised. Two real surfaces matter: a phone with a flat battery at 21:00 must not
  take an unanswered question out of the world with it.
* **The question names the number that is printed in double height on the paper.** It says "Bon 042",
  the same words in the same order as the slip header, so the answer takes one glance at the top slips.
  Naming the order number here instead would send a server looking for 137 in a pile of slips numbered
  042, and the answer would be a wrong "the slip is missing" followed by a duplicate order.
* **The backend adds evidence, not a decision.** When the connection comes back, the worker reads the
  printer status. If paper end or cover open is set, the phone additionally says that the printer has
  no paper, which makes "the slip is missing" the likely answer. The app still asks. It never guesses.
* **Answering "the slip is there"** moves the ticket to `Printed` and records who answered.
* **Answering "the slip is missing"** queues a reprint. The reprint carries the same global order number
  and the same per-location sequence number, and prints `NACHDRUCK` / `REPRINT` in its header, so if
  both slips somehow exist the station sees two identical numbers and knows to produce one order.
* **`Unknown` never resolves itself by timing out.** An unanswered question stays on the screen and in
  the admin list until someone answers it. A quiet expiry would be a silently dropped order. Because
  such a question would otherwise disappear when the next evening's session starts, starting a session
  refuses while one is open, per section 2.3.

### 3.5 Failure behaviour summary

| What went wrong | Bytes written | Automatic retry | Ticket state | What the server is told |
|---|---|---|---|---|
| Printer not reachable on the network | 0 | Yes, with backoff, until the give-up window | `Queued`, then `Failed` | The station is not answering, with what to do next |
| Paper end or cover open found before sending | 0 | Yes, as soon as the printer reports it is ready | `Blocked`, and `Failed` only at the 20 minute outer bound | Which station, what is wrong, and that it prints by itself afterwards. The give-up window does not run, because the cause is known. |
| Socket dropped before the first byte | 0 | Yes, with backoff | `Queued` | Nothing, unless the give-up window expires |
| Socket dropped part way through the write | > 0 | Never | `Unknown` | The question in section 8.8 |
| 90 second timeout with no echo | > 0 | Never | `Unknown` | The question in section 8.8 |
| Paper ran out part way through a job | > 0 | Never | `Unknown` | The question, plus the note that the printer has no paper |
| Printer reports a mechanical error | Either | Never | `Unknown` if bytes were written, otherwise `Blocked` | The station name and to fetch someone who can look at the printer |
| Two jobs in a row end `Unknown` at one station | 0 for the queued ones | No, the station is declared faulty | `Failed` for every waiting ticket, at once | To announce the order at the station in person, on every affected phone at the same time |
| Many slips waiting at one station | 0 | Yes, nothing about a queue changes behaviour | Unchanged | The count, in the station banner and the admin overview. A queue is information, never a trigger. |
| Backend restarted mid-job | Unknown | Never | `Unknown` on recovery | The question, on the next connect of that phone |
| Station is disabled in configuration | 0 | Held until it is switched on again | `Queued`, and `Failed` only at the 20 minute outer bound | That the station is switched off. The give-up window does not run, because the cause is known. |
| A human took the ticket on the break-glass page while a job was queued for it | 0 | No, the job ends `Failed` with `TicketResolvedByHuman` | `HandledOnPaper` | That the station has taken the order from the screen and no slip will be printed |
| Station is on the test printer during a real event | all | No | `PrintedOnTestPrinter` | That the order went to the test printer and no slip is on the pile |

### 3.6 A guest changes their mind

This happens several times an evening, and the product's answer is deliberately not a button.

**Before the order is placed** there is nothing to specify. The order is a cart on the phone. Removing
a line, changing a quantity, and starting over are ordinary editing, not state transitions, and the
backend has never heard of the order.

**After the order is placed there is no cancel action, anywhere, for anybody.** The moment the order is
accepted its slip is either coming out of a printer or already lying on a pile at the station. Software
cannot take paper back. A cancel button would clear the row on the server's phone, which reads as "that
order is withdrawn", while the kitchen goes on cooking from a slip nobody removed. That is a system
telling a human something untrue about the physical world, which is the one defect this entire document
is written against. It is worse than the problem it solves.

**What happens instead** is what happened before there was any software: the server walks over and tells
the station. That walk is short, it is certain, and it is the same walk that any workable design would
have required anyway. The order detail on the phone carries one sentence saying so, at the moment the
server is looking at the order the guest just changed. The extra order stays in the database, which
costs nothing: no money moves through the app, and nothing aggregates orders except a list the
treasurer skims once.

**This is not the dead printer case.** A station whose printer failed is covered by the `Failed` ticket
and the break-glass page in section 8.10, and that path already ends in a human at the station taking
the order onto paper. There is exactly one escape hatch and it is that one.

---

## 4. Numbering

Two numbers appear on every slip. Both matter, and they do different jobs.

* The **global order number** is how a person says one order out loud across the whole site.
  "Bestellung 137, wo ist das Bier dazu." It is unique per event session and shared by every slip of
  that order.
* The **per-location sequence number** is how a person sees at a glance that something is missing. The
  slips at the kitchen run 1, 2, 3, 4. If the pile jumps from 41 to 43, then 42 is lost and the station
  knows it without touching software. This is the loss detection mechanism, and it is why the number is
  printed large.

### 4.1 Allocation

Counters live in the `NumberCounter` table, keyed by kind and by the ids the counter belongs to:

| CounterKind | EventSessionId | ProductionLocationId | PrinterEndpointKey | Counts |
|---|---|---|---|---|
| `GlobalOrder` | set | null | null | Global order numbers |
| `LocationSequence` | set | set | null | That location's sequence numbers |
| `PrinterProcessId` | null | null | set | ESC/POS process ids for one printer, cycling 1 to 9999. Two locations sharing a machine share this row, and section 2.13 says why they have to. |

The key is composite rather than a formatted string. An earlier draft built a string like
`session:{guid}:location:{guid}`, which is 90 characters and did not fit its own 80 character column,
so the first order of the first evening would have failed inside the acceptance transaction and every
phone would have told its server to write orders on paper. The composite key removes both the length
problem and the string parsing.

Order and sequence counters start at 1. Allocation happens inside the single database transaction that
accepts an order:

1. Open a SQLite write transaction (`BEGIN IMMEDIATE`, which SQLite serializes, so there is exactly one
   writer). The busy timeout is 5 seconds, so a concurrent writer waits rather than returning
   `SQLITE_BUSY` to a server standing at a table.
2. Insert the `Order` row and take the next global order number.
3. For each production location the order routes to, insert one `LocationTicket` and take that
   location's next sequence number.
4. Insert the lines.
5. Commit.

Nothing else in the system allocates an order or sequence number. Print attempts do not and reprints do
not. The one other counter in the table, `PrinterProcessId`, belongs to the printing service and is
described in section 7.4.

### 4.2 Why gaps mean what they mean

A gap in the pile has to mean "a slip is missing", otherwise the station learns to ignore gaps and the
mechanism is dead. Four rules keep that true:

* **A number is allocated only by a transaction that commits.** If anything in the acceptance path
  fails, the whole transaction rolls back and the counter rolls back with it. There is no separate
  "get a number" call that can succeed while the order fails.
* **Counters are in the database, never in memory.** A restart, a crash, or a laptop that lost power
  mid-evening resumes at the exact next value. There is no in-process cache and no batch reservation,
  because both hand out numbers that may never be used.
* **A reprint reuses the number.** Reprinting does not consume a new one, so a reprinted slip never
  makes the pile look like it gained an order.
* **Slips are printed in sequence number order at each station.** A blocked job holds its station
  rather than being overtaken by a later one, which is also what the hardware does anyway, since a
  paper-out condition stops everything. Without this rule a pile could read 41, 43, 44 and then 42 a
  minute later, which teaches a station to wait and see, and waiting and see is the same behaviour as
  ignoring gaps.
* **Sequence numbering is per location and knows nothing about printers.** When two locations share one
  machine (section 2.12), the roll carries `BON 041`, `BON 017`, `BON 042` interleaved, and each
  station's gap detection still works on its own run of numbers because each station only ever sorts
  its own slips. The station name at the top of each slip is what tells the person at the printer which
  pile it belongs on, and the numbers do the rest once it is on the right pile.

Because there is no cancellation and no deletion, a committed number always has a slip behind it. Every
gap in a pile means one thing and only one thing: that slip did not come out, and the admin order list
can name which order it belonged to in five seconds.

### 4.3 Across restarts and sessions

* On startup the backend reads nothing into memory. The first order after a restart takes the next
  value straight from the table.
* Starting a new event session creates new counter rows starting at 1. Old orders keep their old
  numbers and stay in the database. Because tickets carry `EventSessionId` through their order, a
  sequence number is only ever ambiguous across sessions, never within one, and the slips from
  yesterday are in yesterday's bin.
* Starting a session is guarded by the conditions in section 2.3, so the ambiguous case cannot be
  created during service. The admin screen also says plainly that numbering starts again at 1.

### 4.4 Display format

* Global order number: printed as given, no padding, always preceded by the word `Bestellung` or
  `Order`. It is written the same way on the phone, in the admin, and on paper. There is no `#` form
  and no "Nr." form anywhere.
* Per-location sequence number: printed at double width and double height, zero padded to three digits,
  always preceded by the word `Bon` or `Slip`, so `BON 042` on the slip header and "Bon 042" on the
  phone. The padding keeps the slips the same visual size all evening, so a jump is obvious in a stack.

One word per number, on screen and on paper, is a safety requirement rather than a style preference.
The whole `Unknown` mechanism depends on a server reading a question on a phone and finding the same
words on a piece of paper in the dark.

---

## 5. REST API

One process, one port. The default is port 5000 bound to `0.0.0.0`, and the scheme, port, and bind
address live in one configuration object so a later move to HTTPS is a setting rather than a rewrite.

### 5.1 Audiences and how each is authenticated

| Audience | Path prefix | Authentication |
|---|---|---|
| Server phones | `/api/...` | `Authorization: Bearer <TokenLookupId>.<secret>`. The backend splits on the dot, loads the one device row by `TokenLookupId`, and verifies the secret with PBKDF2 using that row's stored salt, iteration count, and algorithm. A revoked device is rejected. |
| Admin | `/api/admin/...` | The request must arrive from the laptop itself: the loopback interface, or one of the addresses this process is bound to. No password exists because nobody would manage one. Requests to admin paths from any other address get 404, not 403, so a phone browsing the site learns nothing. |
| Station break-glass | `/api/station/{accessKey}/...` | The 32 character access key in the path is the whole credential. It grants two things and nothing else: **reading the open tickets of any production location**, and acknowledging a ticket whose station cannot print. Section 5.6 gives both rules exactly, including why reading is not scoped to the key's own location and why acknowledging is scoped much more tightly than reading. |
| Enrolment and health | `/api/enrolment/redeem`, `/api/health` | Anonymous, rate limited |

Loopback only for the admin API is the right trade for version 1. It replaces a credential nobody would
manage with a physical constraint everybody understands: the admin is the person standing at the
laptop. On an open WiFi with plain HTTP any token-based admin login would be readable off the air, so
the physical constraint is genuinely stronger than the alternative, not merely simpler. Accepting the
laptop's own bound addresses as well is safe, because a request from a phone carries the phone's
address, and it removes the one case that otherwise looks like a broken program: the volunteer who
reads `http://192.168.1.23:5000` off the overview screen and types it into the laptop's own browser.

**The admin page is served from every address; the admin API is not.** A phone that opens `/admin` gets
a page with one sentence telling the reader to open the admin on the laptop, and the laptop's address
in it. Serving a broken admin screen with no explanation to a curious server is worse than either
extreme.

Rate limits: 20 requests per minute per IP on `/api/enrolment/redeem`, 600 per minute per device
elsewhere. Exceeding a limit returns 429 with a plain message.

Every error response uses the same shape. The message is not rendered here: the response carries the
key and the parameters, and the caller renders it in its own language from its own resource file. This
is deliberate. A phone has a stored language (section 2.8) that an `Accept-Language` header does not
know about, and a message that exists both as a backend string and as a frontend string will drift.

```json
{
  "code": "PrinterOutOfPaper",
  "messageKey": "ticket.paperEnd",
  "parameters": { "station": "Küche" },
  "details": null
}
```

`details` is present only for admin callers and carries the technical text. A server's phone never
receives a stack trace or a socket error string. Slips are the one exception to client side rendering:
they are rendered on the backend from resx, because the printer has no resource file.

**Database failures have a stated outcome, like every other failure.** A write that cannot complete
returns 503 with `code: DatabaseUnavailable` and a message telling the server to try sending the order
again. On startup the backend verifies that the database file's directory
is writable, and refuses to start with a plain sentence on the console when it is not. A volunteer who
copies the program into `Program Files` on Windows hits exactly that, and a program that starts and
then silently fails every order is the worst possible response to it.

### 5.2 Enrolment and session (server phones)

#### POST /api/enrolment/redeem

Anonymous. This is the only call a phone can make before it has a token.

Request:

```json
{
  "code": "8f2a1c...",
  "sixDigitCode": null,
  "serverPersonId": "c2f1...",
  "newServerName": null,
  "userAgent": "Mozilla/5.0 ..."
}
```

Exactly one of `code` (from the QR URL) and `sixDigitCode` is supplied. Exactly one of
`serverPersonId` (picked from the admin's list) and `newServerName` is supplied.

Response 200:

```json
{
  "deviceId": "9a71...",
  "deviceToken": "K7f3...secret",
  "serverPerson": { "id": "c2f1...", "name": "Anna" },
  "language": "de"
}
```

`deviceToken` is returned exactly once and never again.

| Status | When |
|---|---|
| 200 | Redeemed. The code is now consumed. |
| 400 | Neither code form supplied, or both, or the name is empty |
| 410 | The code was already used or has expired. The message tells the phone to scan again, because the laptop is already showing a fresher code. This does not count as a failed attempt. |
| 423 | This address is locked for five minutes after ten wrong codes. Only this address is locked. |
| 429 | Rate limited |

#### GET /api/session

Device auth. Returns who this device is.

```json
{
  "deviceId": "9a71...",
  "displayName": "Anna",
  "serverPerson": { "id": "c2f1...", "name": "Anna" },
  "eventSession": { "id": "...", "name": "Samstagabend", "isPractice": false },
  "language": "de"
}
```

401 when the token is unknown or the device is revoked.

#### PUT /api/session/language

Device auth. Body `{ "language": "de" | "en" }`. Returns 204. Stored on the device row so a reopened
page keeps the choice, and so every message the backend keys reaches this phone in the right language.

### 5.3 Catalog (server phones)

#### GET /api/catalog

Device auth. One call, everything the ordering screen needs.

```json
{
  "version": "2026-08-26T17:04:11Z",
  "categories": [ { "name": "Essen", "sortOrder": 1 } ],
  "items": [
    {
      "id": "...",
      "name": "Bratwurst mit Brot",
      "categoryName": "Essen",
      "priceCents": 350,
      "sortOrder": 1,
      "isAvailable": true,
      "locationIds": ["kitchen-id"]
    }
  ],
  "locations": [ { "id": "kitchen-id", "name": "Küche", "sortOrder": 1 } ],
  "tableSuggestions": [ { "label": "Tisch 12", "sortOrder": 1 } ]
}
```

`locationIds` holds the item's active candidate locations, always at least one. An item with exactly
one is routed silently. An item with more than one makes the phone ask, once, as the line is added.

**What the payload contains, and the two item states it distinguishes** (section 2.5):

* **Deactivated items (`IsActive` false) are not in the payload at all.** They are not on this
  festival's menu, so there is nothing for the phone to draw.
* **Sold-out items (`IsAvailable` false) are in the payload, with `isAvailable: false`.** The phone
  draws them greyed and not selectable with the reason underneath (section 8.6). Leaving them out would
  make a server hunt for an item that was on the screen a minute ago.

**Prices come from here and from nowhere else.** `priceCents` is the backend's current price, and it is
what the phone displays and what the phone adds up. The phone never sends a price, never proposes one,
and has no field in which it could: the request body in section 5.4 carries item ids and quantities.
The total stored on an order is computed by the backend from its own prices at acceptance, so a phone
holding a stale catalog produces a stale number on a screen and never a stale number in the database.

The phone caches this in memory and refetches when the `CatalogChanged` SignalR event arrives, which is
what keeps an open basket current when a price is edited or an item sells out mid-order. The candidate
list is used on the phone for display and for the question, and the routing is recomputed on the
backend at acceptance, which is authoritative.

### 5.4 Orders (server phones)

#### POST /api/orders

Device auth. The single most important endpoint in the system.

Request:

```json
{
  "clientOrderId": "3f7c9d2e-...",
  "tableLabel": "Tisch 12",
  "note": null,
  "expectedTotalCents": 1050,
  "lines": [
    { "catalogItemId": "...", "quantity": 2, "note": null, "productionLocationId": null },
    { "catalogItemId": "...", "quantity": 1, "note": "ohne Ketchup", "productionLocationId": "bar-marquee-id" }
  ]
}
```

`clientOrderId` is the submission id. **The phone generates it once, when the server first taps send,
and reuses the same value for every retry of that same order.** It is never regenerated, not by a
retry, not by a reload, and not by a re-enrolment. Section 9.3 covers it from the phone's side, and it
is the whole reason a manual retry cannot produce a second order.

`productionLocationId` is the server's choice for that line. It is required when the item has more than
one active candidate location and is omitted otherwise. When supplied it must name one of that item's
assigned locations.

`expectedTotalCents` is the total the phone showed and the server read aloud. It is not used to reject
anything. It exists so that a price the admin edited after the phone last fetched the catalog cannot
pass unnoticed by everybody, which is what happens when the backend simply recomputes and stores its
own answer.

**`expectedTotalCents` is a comparison value and nothing else, and an implementer must not be able to
read it any other way.** It is compared against the recomputed total, echoed back in the response so
the phone can show the difference, and then discarded. It is not stored on the order, not stored on any
line, and it never contributes a cent to `Order.TotalCents`. **The phone never sends a price and never
influences one.** There is no price field anywhere in this request: a line carries an item id, a
quantity, an optional note and an optional station, and the money comes from `CatalogItem.PriceCents`
as the backend reads it inside the acceptance transaction. A request whose `expectedTotalCents` is
absent, zero, or wildly wrong is accepted exactly like any other, at the backend's own total.

Response 201:

```json
{
  "orderId": "...",
  "globalOrderNumber": 137,
  "status": "Accepted",
  "totalCents": 1050,
  "expectedTotalCents": 1050,
  "createdAtUtc": "2026-08-26T17:42:03Z",
  "tickets": [
    {
      "ticketId": "...",
      "locationId": "kitchen-id",
      "locationName": "Küche",
      "sequenceNumber": 42,
      "status": "Queued",
      "lineIds": ["...", "..."]
    }
  ]
}
```

When `totalCents` differs from `expectedTotalCents`, the order is still accepted and the phone shows
one line naming the new total. The cash was taken against the old number and the difference belongs in
front of a human, not in a log.

| Status | When |
|---|---|
| 201 | Accepted and numbered |
| 200 | The same `clientOrderId` was already accepted. The original order is returned unchanged, with its original numbers and its original tickets. No second order is created and no second print job is enqueued. |
| 400 | Empty lines, quantity out of range, table label missing or too long |
| 401 | Unknown or revoked token |
| 409 | The same `clientOrderId` was used with different content. The message tells the server to check their order list before ordering again. |
| 422 | An item id is unknown, or a line names a location the item is not assigned to, or a line omits the station for an item that has more than one candidate |
| 503 | The database could not be written. The order was not accepted and the phone offers the retry again. |

**The 200 answer is the one that keeps a manual retry safe, and it is worth being exact about.** When
the first submission reached the backend but its response was lost on the way back, the server sees a
failure and taps retry. Without the id, the backend would create a second order with a second set of
numbers, both stations would print, and every table would get everything twice. With it, the second
submission finds the row by the unique index on `ClientOrderId` inside the same `BEGIN IMMEDIATE`
transaction that would otherwise insert, and returns what already exists.

The body of a 200 is byte for byte the body of the 201 it repeats, so the phone shows the same
confirmation with the same order number. To the server the two cases are the same event, and the screen
says the same thing, because a screen that distinguishes them would be describing the network rather
than the order.

`ClientOrderId` is stored on the order row with a unique index and is kept exactly as long as the order
is, which is forever: orders are never deleted, so the protection never expires and no cleanup job has
to be written or remembered.

**An item that went sold out, or was deactivated, after the catalog was fetched is accepted, not
rejected.** The guest ordered it, the server read the total aloud, and the cash may already be in their
apron. The split of sold out from deactivated in section 2.5 changes nothing here: both states are
accepted at submission, and neither one is ever a reason to refuse an order. **Only an item id that
does not exist at all is a 422**, and that is a broken client rather than a guest.

The server usually learns before they send rather than after, because `CatalogChanged` reaches the open
basket and flags the line while they are still standing at the table (section 8.6). When they send it
anyway, which is the right thing to do, the accepted consequence is stated plainly: **a slip can print
for something the kitchen has run out of, and the station sends word back with the tray.** That is what
happens with a paper order pad today, and no software can prevent it, because the only place the
information exists at the moment of the order is in the kitchen.

An unreachable printer never produces an error here. The order is accepted and the print state follows.
**No property of any printer or station is ever a reason to reject, delay, or hold an order at this
endpoint**, and section 3.1 states the same rule from the order's side.

#### GET /api/orders/mine

Device auth. Query `?since=<iso>` optional, `?limit=` default 50.

**Scoped by `ServerPersonId` and the active event session, not by device id.** A server whose phone was
revoked and set up again is the same person and sees the same evening. Scoping this by device would
mean that the obvious volunteer response to a misbehaving phone, setting it up again, silently orphans
every order that person placed and every unanswered question on them.

```json
{
  "orders": [
    {
      "orderId": "...",
      "globalOrderNumber": 137,
      "tableLabel": "Tisch 12",
      "totalCents": 1050,
      "status": "NeedsAttention",
      "createdAtUtc": "...",
      "tickets": [
        {
          "ticketId": "...",
          "locationName": "Küche",
          "sequenceNumber": 42,
          "status": "Unknown",
          "failureReason": "SocketDropped",
          "printerHasPaper": false
        }
      ]
    }
  ]
}
```

This is the call the phone makes after every reconnect and on every page load. It replaces the phone's
list outright, so the phone never has to reason about what it might have missed.

#### GET /api/orders/{orderId}

Device auth. Full order with lines, each line naming the station it went to. 404 unless the order
belongs to the caller's `ServerPersonId`, or the caller is admin from the laptop.

#### POST /api/orders/{orderId}/tickets/{ticketId}/resolve

Device auth, and only for the `ServerPersonId` that placed the order. Answers an `Unknown` outcome.

Request `{ "slipIsOnThePile": true }` or `{ "slipIsOnThePile": false }`.

Response 200 with the updated ticket. `true` moves it to `Printed`. `false` queues a reprint with the
same numbers and moves it to `Queued`. 403 when the caller is a different person. 409 if the ticket is
no longer `Unknown`, with a message saying that the question was already answered, which happens when
the same person answered it on the laptop or from a second phone.

#### POST /api/orders/{orderId}/tickets/{ticketId}/reprint

Device auth, same person scope. Allowed from `Failed`, `Printed`, and `PrintedOnTestPrinter`. Creates a
`PrintJob` of kind `Reprint`. Response 202 with the ticket. 409 if a job for that ticket is already
running.

There is no endpoint that cancels, edits, or deletes an order. Section 3.6 gives the reasoning, and
section 1.5 records it as a non-goal so the question is answered once rather than every time it is
asked.

#### GET /api/printers/status

Device auth. The list the phone uses to warn before an order is even placed.

```json
{
  "locations": [
    {
      "locationId": "kitchen-id",
      "name": "Küche",
      "isOnline": true,
      "isPaperEnd": false,
      "isPaperNearEnd": true,
      "isCoverOpen": false,
      "isFaulty": false,
      "lastChangedAtUtc": "..."
    }
  ]
}
```

### 5.5 Admin endpoints (the laptop only)

All admin paths return 404 to callers that are neither loopback nor one of the laptop's own bound
addresses.

**Production locations**

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /api/admin/locations | | 200 list, each with its printer configuration and live status |
| POST | /api/admin/locations | `{name, sortOrder}` | 201, also creating a printer configuration with `TransportKind: "Mock"` and a fresh access key |
| PUT | /api/admin/locations/{id} | `{name, sortOrder}` | 200 |
| POST | /api/admin/locations/{id}/deactivate | | 200, or 409 naming the open tickets or the items that would be left with no station |
| POST | /api/admin/locations/{id}/regenerate-access-key | | 200 with the new break-glass URL |
| GET | /api/admin/locations/{id}/station-card | | 200, a printable card with the station name and a QR code to its break-glass URL, meant to be taped inside the printer lid |

**Catalog**

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /api/admin/items | | 200 list with assignments |
| POST | /api/admin/items | `{name, categoryName, priceCents, sortOrder, locationIds[]}` | 201, 422 when `locationIds` is empty |
| PUT | /api/admin/items/{id} | same | 200, 422 when `locationIds` is empty |
| POST | /api/admin/items/{id}/availability | `{isAvailable}` | 200, pushes `CatalogChanged`. The sold-out toggle. Never refused, in either direction, at any time. |
| POST | /api/admin/items/{id}/deactivate | | 200, or 409 while a non-practice session is active, naming the sold-out toggle as tonight's tool |
| POST | /api/admin/catalog/import | CSV upload | 200 with a per-row result list, 422 with row numbers and reasons |

**The two item states are two endpoints on purpose.** `availability` is the sold-out toggle: one call,
one field, no confirmation step, and it is what the admin screen's toggle sends on each tap (section
8.9). `deactivate` takes an item off this festival's menu and is refused during a live event. Section
2.5 gives the table of who sets which one and when, and no screen or endpoint in the product treats
them as one concept.

`locationIds` is the whole assignment. There is no priority field: with more than one candidate the
server chooses, and the only automatic ordering left is `ProductionLocation.SortOrder`, which is what
decides where a line goes if the station the server chose was switched off in the meantime.

**Table suggestions**

| Method | Path | Body |
|---|---|---|
| GET | /api/admin/table-suggestions | |
| PUT | /api/admin/table-suggestions | `{labels: ["Tisch 1", ...]}` replaces the list |
| POST | /api/admin/table-suggestions/from-last-session | 200 with the distinct table labels servers actually typed, added to the list |

**Staff and devices**

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /api/admin/server-people | | 200 |
| POST | /api/admin/server-people | `{name}` | 201 |
| POST | /api/admin/server-people/{id}/deactivate | | 200 |
| GET | /api/admin/devices | | 200 with person, last seen, revoked state, user agent |
| POST | /api/admin/devices/{id}/revoke | | 200, pushes `DeviceRevoked` to that device |
| PUT | /api/admin/devices/{id} | `{displayName, serverPersonId}` | 200. Changing the person is the shift handover: the day crew's phone becomes the evening crew's phone without a revoke, and earlier orders stay with the person who placed them. |

**Enrolment**

| Method | Path | Response |
|---|---|---|
| POST | /api/admin/enrolment/session/start | 200 `{qrUrl, sixDigitCode, expiresAtUtc}`. Begins rotation, which then pushes `EnrolmentCodeRotated` about every 30 seconds. |
| POST | /api/admin/enrolment/session/stop | 204. Outstanding codes keep their 5 minute lifetime. |
| POST | /api/admin/enrolment/invalidate-all | 204. Immediately consumes every outstanding code. |
| GET | /api/admin/enrolment/locks | 200 with the addresses currently locked and when each lock expires |
| POST | /api/admin/enrolment/unlock | 204. Lifts every lock at once. |

The QR URL is built from the address the laptop is actually reachable on. The backend enumerates its
non-loopback IPv4 addresses at startup and on every enrolment session start. When there is more than
one, the admin picks which network the phones are on, and the choice is remembered. The URL has the
form `http://192.168.1.23:5000/j/8f2a1c...`.

**The address the phones hold is the address in that QR code, and nothing else in the product survives
it changing.** A phone's stored token lives in the browser's storage for that exact origin, so a router
reboot that hands the laptop a new address leaves every phone holding a token it cannot reach and any
unsent order stranded in storage for an address nobody will visit again. There is no software recovery
for that, which is why the setup checklist makes a fixed address a step rather than a hope, and why the
overview screen names the previous address when it changed. The recovery, when it happens anyway, is
that every phone scans the new QR code, and orders queued on the old address are written on paper.

**Printers**

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /api/admin/printers | | 200 configuration plus live status per location |
| PUT | /api/admin/printers/{locationId} | full configuration | 200. Changing the endpoint restarts the workers on both sides of the change. **An address another location already uses is accepted**, and the response says which locations now share it. |
| POST | /api/admin/printers/{locationId}/test-print | | 202. Prints a test slip naming the location, the current time, and carrying the station card's QR code. Section 7.7 gives the command sequence for that QR code. |
| POST | /api/admin/printers/{locationId}/reconnect | | 202. Also clears `IsFaulty`, which is how a human ends a circuit breaker. |
| POST | /api/admin/printers/discover | | 202, then `PrinterDiscovered` events. Scans the laptop's own /24 for open port 9100 and reports what answered. |

**Pointing a station at another station's printer is the product's answer to a dead printer**, and it
is this endpoint and nothing more. There is no fallback printer field to fill in beforehand and no
automatic rerouting, because both would have to guess during the one minute of the evening when nobody
wants a guess. What the admin does instead is type the working printer's address into the broken
station's configuration, which takes about as long as reading it off the other row. Section 2.12
describes what the configuration then means, and checklist step 21 is how a volunteer is told to do it.

Discovery exists because the two checklist steps most likely to go wrong on site are holding a feed
button while powering a printer on, reading an IP address off a self test, and typing it in, once per
printer, at every event, because DHCP moves them. A list to tap replaces all three.

**The test printer**

One endpoint, because the mock's slips are files in a folder (section 7.8) rather than a screen with a
list to fetch and clear.

| Method | Path | Body | Response |
|---|---|---|---|
| POST | /api/admin/mock/{locationId}/fault | `{fault, mode}` | 200. `fault` is one of `None`, `PaperEnd`, `CoverOpen`, `ConnectTimeout`, `DropSocketEarly`, `DropSocketMidJob`, `UnknownOutcome`. `mode` is `Once` or `Sticky`. 422 when the location's transport is not `Mock`. |

The fault set is enumerated in exactly two places, here and in the table in section 7.8, and the two
are the same seven values. `DropSocketEarly` is the zero byte drop that section 7.6 singles out as the
only failure the system retries by itself, and the end to end scenario in section 11.3 cannot be written
without arming it.

Arming `None` clears a sticky fault. For `PaperEnd` that is the paper change: the station reports itself
ready, and the slips parked behind it print without anybody re-sending anything.

**Event session and orders**

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /api/admin/event-session | | 200 current session, plus what currently blocks starting a new one |
| POST | /api/admin/event-session | `{name, isPractice, confirmedName}` | 201. Ends the current session and resets numbering to 1. 409 with the blocking conditions from section 2.3, each naming what to settle first. |
| GET | /api/admin/orders | `?status=&locationId=&since=&search=` | 200 |
| POST | /api/admin/orders/{id}/tickets/{ticketId}/resolve | `{slipIsOnThePile}` | 200. The laptop's answer to the `Unknown` question, for the evening when the placing server's phone is flat, lost, or in a pocket at the far end of the marquee. |
| POST | /api/admin/orders/{id}/tickets/{ticketId}/reprint | | 202 |
| GET | /api/admin/orders/{id}/print-history | | 200 with jobs and attempts, including `PrintAttempt.TransportDetail` and `Phase`. See the boundary note below. |
| GET | /api/admin/export/orders.csv | | 200 CSV of the session, for the treasurer to look at afterwards |
| POST | /api/admin/backup | | 200 `{fileName}`. Runs `VACUUM INTO` a dated file next to the database. |
| GET | /api/admin/diagnostics | | 200 with version, database path, database size, uptime, listening addresses, per printer status, and the path of the log file |
| GET | /api/admin/log | | 200, the current rolling log file as plain text, so the question "the marquee bar got nothing all evening, why" has an artifact to answer it |

**Where the technical detail may and may not go.** `PrintAttempt.TransportDetail` holds socket error
text, and section 2.11 says it is never shown raw to a server. The print-history endpoint above is the
one place in the product that serializes it, which is compatible with that rule because this path is
served only to the laptop and read by the person diagnosing the evening, not by a server holding a
phone. The boundary is worth stating as a rule rather than leaving to an implementer's judgment, since
the obvious shortcut is to reuse one ticket serializer for both audiences:

* **Admin, on the laptop:** the print-history response, the diagnostics response, and the log file may
  carry transport text in full.
* **Everything a device can reach:** every `/api/orders` response, every `/api/station` response, and
  every SignalR payload carry a `messageKey` and its parameters and never a transport string, in raw or
  summarized form. The client renders the sentence from its own resource file in its own language.

A failure that has no message key is a failure nobody wrote a sentence for, and the answer is to write
the sentence, never to fall back to the socket error.

**CSV formats.** Both files are UTF-8 **with a byte order mark** and use the semicolon as the delimiter,
because the audience opens them in German Excel, where a comma delimited file lands in one column and a
UTF-8 file without a mark shows broken umlauts.

The export has one row per order line and the header
`Bestellnummer;Zeit;Tisch;Bedienung;Artikel;Menge;Einzelpreis;Summe;Station;Bonnummer;Status`. Prices
are written with a comma as the decimal separator and no thousands separator. Practice sessions are
excluded.

The import has the header `Name;Kategorie;Preis;Stationen;Sortierung`. `Preis` accepts `3,50` and
`3.50`, and a value with no separator is read as whole euros, so `3` is three euros. `Stationen` is a
list of station names separated by `|`, and a name that does not match an active station rejects that
row. A row with an empty `Stationen` is rejected, because an item with no station cannot be ordered.
The 422 response names the row number and the reason for every rejected row, and no row is imported
when any row fails.

### 5.6 Station break-glass endpoints

Nobody opens these in normal operation. They exist for the evening when a printer dies and food still
has to be produced, and on that evening the station has to be able to work the whole pile off a phone
screen rather than only the slips that happened to fail.

| Method | Path | Response |
|---|---|---|
| GET | /station/{accessKey} | The single page app shell, in station mode |
| GET | /api/station/{accessKey}/locations | 200 with the active production locations and, for each, whether its printer can print right now. This is the filter's contents. |
| GET | /api/station/{accessKey}/tickets | 200 with the open tickets, oldest sequence number first. `?locationId=` selects one location and defaults to the key's own. |
| POST | /api/station/{accessKey}/tickets/{ticketId}/acknowledge | 200, moving the ticket to `HandledOnPaper`. 409 with a stated reason when the ticket's station can print. |
| GET | /api/station/{accessKey}/status | 200 with the selected location's printer status |

**The list is every open ticket, not only the broken ones.** A ticket is open when its status is
anything other than `Printed`, `PrintedOnTestPrinter` or `HandledOnPaper`, which is to say `Queued`,
`Blocked`, `Printing`, `Unknown` and `Failed`. When a printer has died and no spare exists, the kitchen
opens this page on somebody's phone and produces the orders straight off it, writing the table number
on a scrap of paper and sending it out with the food. That only works if every order is on the screen,
so every order is.

Each row carries what a person needs to make and label the food, and nothing else: the slip number, the
order number, the table label, every line with its quantity and any line note, the order note, and the
time the order was taken. It is the same information as the printed slip, in the same order, because a
station reading a screen and a station reading paper should not have to learn two layouts.

**Filtering by production location, because a printer can move.** The filter exists for the
configuration in section 2.12, where the admin has pointed a broken station at a working station's
printer. The person standing at the kitchen printer is then pulling the bar's slips off the same roll
and needs the bar's list, while holding a card that was printed for the kitchen. So a valid access key
opens the page for the whole site and the filter chooses which location's tickets it shows, opening on
the key's own location. That is a real widening of what one key grants, and it is taken deliberately:
the alternative is a bar crew needing a second card that is taped inside a printer they cannot get to.
The key still grants only reading and acknowledging, which is what it granted before.

**Showing is always safe. Acting is what is dangerous.**

That sentence is the whole safety of this page, and it replaces an earlier design that tried to buy
safety by hiding rows. Hiding rows never was the protection: a helper cannot drop an order by reading
about it. What drops an order is the acknowledge button, which marks a ticket as handled on paper,
prints nothing, and reports a normal successful outcome to the placing server. Tapped on a live order
at a healthy printer, to tidy a screen, it produces a gap in the pile that nothing can explain. So the
button is what is restricted, and the list is not.

**The acknowledge action is offered only for a ticket whose station genuinely cannot print right now.**
The condition is evaluated on the server, and the page renders the button only when the server says so:

> A ticket may be acknowledged when its own status is `Failed`, `Unknown` or `Blocked`, **or** when its
> production location cannot print right now.
>
> A location cannot print right now when any of these holds: `PrinterStatus.IsFaulty`,
> `PrinterStatus.IsOnline` is false, `PrinterStatus.IsPaperEnd`, `PrinterStatus.IsCoverOpen`,
> `PrinterStatus.IsInErrorState`, or `PrinterConfiguration.IsEnabled` is false.

Everything else is refused. `POST .../acknowledge` on a ticket at a working station answers **409 with
`station.takeRefused`**, which tells the reader to fetch the slip at the printer and says that the
printer is printing again. A ticket in `Printing` is refused by the same rule, and section 3.2 explains
why that closes the race rather than merely narrowing it: claiming a job sets the ticket to `Printing`
in the same transaction, so the human and the worker cannot both win.

The two halves of the condition cover the two shapes of the problem. The ticket half catches a slip
that failed at a station that is otherwise fine. The station half catches every slip queued behind a
dead or blocked printer, including the ones still sitting in `Queued` because a `Blocked` job ahead of
them is holding the machine: when the kitchen has decided to work off the screen, they need all of the
orders and not only the one at the head of the queue.

**Each ticket carries the decision rather than the page recomputing it.** The response gives every
ticket a `canAcknowledge` boolean and, when it is false, the reason key to render under the row. The
page never evaluates printer state itself. A rule that is enforced in one place and drawn in another
drifts the first time somebody edits one of them.

**The page is no longer empty during a working evening, and the old five minute delay is gone with
it.** An earlier design listed a `Queued` ticket only once its printer had been offline for longer than
the give-up window, which meant that a station whose printer had just died watched an empty screen for
five minutes with the food waiting. Tickets now appear the moment they are accepted, and what keeps
this from becoming the kitchen display system that section 1.5 refuses is not an empty list: it is that
there is no button to press while the printer works, and that the station is reading paper because
paper is faster than a phone.

An unknown or regenerated access key returns 404 with a message telling the reader to ask the person at
the laptop for the current link. The station card printed on the test slip means that link is usually
already taped inside the printer lid.

### 5.7 Health

`GET /api/health` is anonymous and returns 200 with `{ "status": "ok", "eventSession": "...",
"printersOnline": 2, "printersTotal": 3 }`. It is what the setup checklist tells the volunteer to open
in a browser to prove the laptop is reachable from a phone.

---

## 6. SignalR

One hub at `/hub`. Clients connect with the same bearer token they use for REST, passed as an
`access_token` query parameter, which is the transport SignalR supports for WebSockets. The station page
connects with its access key instead. The admin connects from the laptop.

### 6.1 Groups

| Group | Members |
|---|---|
| `person:{serverPersonId}` | Every unrevoked phone belonging to that person. Order and ticket events go here, so a re-enrolled phone keeps receiving the answers to its own questions. |
| `device:{deviceId}` | One phone. Used only for revocation. |
| `devices` | All enrolled, unrevoked phones |
| `admin` | The laptop's admin UI |
| `station:{locationId}` | Any open break-glass page for that location |

### 6.2 Events

| Event | Payload | Sent to | What the client does |
|---|---|---|---|
| `OrderAccepted` | `{orderId, globalOrderNumber, tableLabel, totalCents, tickets[]}` | `person:{placing}`, `admin`, `station:{locationId}` of every ticket | The phone adds the order to its list. The admin list gains a row. The station page gains a row, because its list is every open ticket (section 5.6) rather than only the failed ones. |
| `TicketStatusChanged` | `{orderId, globalOrderNumber, ticketId, locationId, locationName, sequenceNumber, status, failureReason, printerHasPaper, messageKey, parameters}` | `person:{placing}`, `admin`, `station:{locationId}` | The phone updates that ticket's chip and, when the status needs a human, renders the message and its next step in its own language. The station page adds or removes a row. |
| `OrderStatusChanged` | `{orderId, status}` | `person:{placing}`, `admin` | The phone updates the order's headline state. |
| `PrinterStatusChanged` | `{locationId, locationName, isOnline, isPaperEnd, isPaperNearEnd, isCoverOpen, isFaulty, waitingTicketCount, lastDetail}` | `devices`, `admin`, `station:{locationId}` | Phones show a banner when a station has no paper, is not answering, or has been declared faulty, so the server knows before they take the next order. `waitingTicketCount` is appended to that banner with `header.stationWaiting`. The admin printer screen updates its indicator. The station page re-evaluates which rows offer the acknowledge button. |
| `PrinterDiscovered` | `{host, port, respondedAtUtc}` | `admin` | The printer search screen adds a row the admin can tap to fill in the address. |
| `CatalogChanged` | `{version}` | `devices`, `admin` | The phone refetches `/api/catalog`. An item that just sold out stays in the picker, greyed and not selectable, and any quantity already in the basket for it is flagged rather than silently dropped. A changed price is picked up the same way, which is what keeps an open basket showing the backend's current prices. |
| `EnrolmentCodeRotated` | `{qrUrl, sixDigitCode, expiresAtUtc}` | `admin` | The enrolment screen swaps the QR image and the six digits, with the remaining seconds shown. |
| `EnrolmentCompleted` | `{deviceId, displayName, serverPersonName}` | `admin` | The enrolment screen adds the newly set up phone to a live list so the admin can watch a crew do it during a briefing. |
| `DeviceRevoked` | `{deviceId}` | `device:{deviceId}`, `admin` | The phone clears its token and shows the enrolment screen with an explanation. **The half-built order on screen is kept**, and comes back when the phone is set up again. |
| `EventSessionStarted` | `{eventSessionId, name, isPractice}` | `devices`, `admin`, all stations | Phones clear their local order list, because those orders belong to the previous session, and show a one line notice. A half-built order is untouched. |

### 6.3 Delivery and reconnection

* SignalR is a push channel, not a source of truth. Every event has a REST equivalent, and after any
  reconnect the client refetches (`/api/orders/mine`, `/api/catalog`, `/api/printers/status`) rather
  than assuming it missed nothing.
* Automatic reconnect is on, with the intervals 0, 2, 5, 10, and 30 seconds, then every 30 seconds
  indefinitely. Phones stay on the same page all evening and the connection has to come back on its
  own after a WiFi dropout.
* Connection state is visible in the header of the phone app. Section 8.5 gives the wording.
* Nothing in the ordering path depends on SignalR. If the hub never connects, orders still submit over
  REST and the phone falls back to polling `/api/orders/mine` every 15 seconds. The fallback is written
  once, in the store, and is not a separate code path in components.

---

## 7. Printing service

### 7.1 The hardware facts this design is built around

Taken from the Epson TM-T20IV Technical Reference Guide, part number C31CL47102 (USB, RS-232, and
Ethernet). These are not preferences.

* Port 9100 is bidirectional. Printer status comes back over the same socket that carries print data.
* **Only one printing connection per printer at a time.** The connection is held until it is released,
  with a 90 second timeout. A process that crashes while holding the connection blocks that station for
  90 seconds.
* **A dropped socket means the job outcome is unknown.** Re-querying before re-sending is mandatory, and
  even then the answer is a status, not a job history.
* Paper end detection uses `GS a` automatic status back as the push channel. `DLE EOT n=4` polling is a
  heartbeat, not the primary channel.
* `DLE EOT n=2` bit 2 set means the cover is open.
* `DLE EOT n=4` bits 5 and 6 both set (mask `0x60`) means paper end.

### 7.2 The transport interface

Nothing above this interface knows which implementation it is talking to. A station's transport is
configuration, not code, and a new transport is a new implementation rather than a branch inside an
existing one.

```csharp
public interface IPrinterTransport
{
    PrinterTransportKind Kind { get; }
    Task<IPrinterSession> ConnectAsync(PrinterEndpoint endpoint, CancellationToken cancellationToken);
}

public interface IPrinterSession : IAsyncDisposable
{
    IAsyncEnumerable<PrinterStatusSnapshot> StatusStream { get; }
    Task<PrinterStatusSnapshot> QueryStatusAsync(CancellationToken cancellationToken);
    Task<PrintDispatchResult> SendJobAsync(PrintPayload payload, CancellationToken cancellationToken);
}

public sealed record PrinterEndpoint(
    Guid ProductionLocationId,
    string? Host,
    int Port,
    string? AgentIdentifier,
    TimeSpan ConnectTimeout,
    TimeSpan JobTimeout,
    TimeSpan HeartbeatInterval);

public sealed record PrintPayload(
    int ProcessId,
    ReadOnlyMemory<byte> Bytes,
    string RenderedText);

public sealed record PrinterStatusSnapshot(
    bool IsOnline,
    bool IsPaperEnd,
    bool IsPaperNearEnd,
    bool IsCoverOpen,
    bool IsInErrorState,
    string Detail,
    DateTimeOffset ObservedAt);

public sealed record PrintDispatchResult(
    PrintDispatchOutcome Outcome,
    int BytesWritten,
    PrinterStatusSnapshot StatusAtEnd,
    string Detail);

public enum PrintDispatchOutcome
{
    Confirmed,
    Blocked,
    Unreachable,
    SocketDropped,
    Timeout,
    PrinterError
}

public enum PrinterTransportKind { Network, Agent, Mock }
```

Every multi-value return is a named record read by name. `BytesWritten` is on the result rather than
inferred, because it is the single fact that decides whether an automatic retry is safe, and it counts
bytes handed to the socket rather than bytes the printer acknowledged, which nothing can know.

**One outcome, one spelling, in all three enums that carry it.** `PrintDispatchOutcome` is what the
transport returns, `PrintAttempt.Outcome` is that same value persisted, and `PrintJob.FailureReason`
uses the same words plus the four reasons that never come from a transport at all (`PaperEnd`,
`CoverOpen`, `StationDisabled`, `StationFaulty`). `Blocked`, `Unreachable`, `Timeout`, `SocketDropped`,
`PrinterError` and `Confirmed` mean the same thing wherever they appear, so mapping between the three is
a copy rather than a translation table somebody has to keep correct.

Implementations:

| Implementation | Talks to | State |
|---|---|---|
| `NetworkPrinterTransport` | Raw TCP to port 9100, ESC/POS both directions | Version 1 |
| `AgentPrinterTransport` | The Python Pi agent over a small HTTP and WebSocket protocol, for USB attached printers | Deferred. The interface above is the whole contract it will implement. |
| `MockPrinterTransport` | A folder of text files, one file per slip | Version 1, and a shipped product feature |

### 7.3 One worker per printer

Serialization is structural, not advisory.

* At startup, and whenever a printer configuration changes, the `PrinterFleet` service starts one
  `PrinterWorker` **per distinct printer endpoint** among the enabled production locations, and stops
  a worker whose last location was disabled or moved elsewhere.
* **One worker per printer, not per location, because two locations may share a machine** (section
  2.12). Keying the worker on the location would put two workers and two sockets on one printer the
  moment an admin points a broken station at a working one, and the hardware allows exactly one
  printing connection at a time. A worker serves every active location whose configuration resolves to
  its endpoint, writes each of their `PrinterStatus` rows from the one socket it owns, and allocates
  from the one `PrinterProcessId` counter that belongs to that endpoint.
* A worker owns a queue of print job ids with a single consumer. Because there is exactly one consumer
  and exactly one worker per printer, two jobs can never be in flight at the same printer.
* **Jobs are attempted in `LocationTicket.CreatedAtUtc` order**, and a blocked job holds the printer
  rather than being overtaken by a later one. Within one location that is the same thing as
  `LocationSequenceNumber` order, because sequence numbers are allocated at acceptance in creation
  order, so each station's slips still reach its pile in an unbroken run. That is what the hardware
  does anyway, since a paper-out condition stops everything, and it is what keeps a pile from reading
  41, 43, 44 and then 42 a minute later. A pile that heals itself teaches a station to wait and see,
  and waiting and seeing is the same behaviour as ignoring gaps. When two locations share a printer,
  their slips interleave on the roll and each station's own run of numbers stays unbroken, which is the
  rule stated in section 4.2.
* The worker owns the connection. Nothing else in the process opens a socket to a printer. This is what
  keeps the "one connection at a time" rule true even while the admin runs a test print, which is why a
  test print is a `PrintJob` of kind `Test` rather than a side channel.
* The worker keeps its session open between jobs, so `GS a` status arrives as a push rather than being
  discovered on the next job. The 90 second idle timeout is kept alive by the `DLE EOT n=4` heartbeat
  every 10 seconds.
* **On startup the worker enqueues every ticket of every location it serves that is `Queued` or
  `Blocked`, regardless of which event session it belongs to**, oldest first. Scoping this to the
  current session would leave a ticket that was parked behind a paper-out when the next evening began
  invisible to the worker, invisible to the phone, and `Queued` in the database forever. Section 2.3
  also refuses to start a session while such a ticket exists, so the two rules cover the same hole from
  both sides.
* Every ticket that was `Printing` when the process died is moved to `Unknown`, because bytes may have
  been written. This is the crash recovery path and it is covered by an integration test.

### 7.4 The job sequence

1. **Take the job.** Load the ticket, its order, and its lines in one query.
2. **Claim the job, or discover that nobody wants it any more.** In one transaction, re-read the
   ticket and move it from `Queued` or `Blocked` to `Printing`. Nothing is attempted outside that
   claim, and this step is the one that makes a duplicate impossible:
   * **The ticket is no longer waiting**, because a human answered a question, asked for something, or
     took it on the break-glass page. The job ends `Failed` with `FailureReason: TicketResolvedByHuman`,
     the attempt records zero bytes, and **the socket is not touched**. A printer that recovers after a
     station has taken an order onto paper therefore prints nothing for it, which is what keeps a table
     from being served the same order twice.
   * **The claim succeeded.** The ticket is now `Printing`, and every endpoint that lets a human take a
     ticket over refuses a `Printing` ticket with a 409. Neither side can act after the other has, and
     there is no interval in which both can.
   * **The printer has since been declared faulty.** The job is left for the circuit breaker in section
     7.6 to resolve rather than attempted, and the ticket is not claimed.
3. **Ensure the connection.** If no session is open, connect with `ConnectTimeout`. Failure marks the
   printer offline, pushes `PrinterStatusChanged`, leaves the job `Queued`, and schedules a reconnect
   with backoff 1, 2, 5, 10, 30 seconds, capped at 30.
4. **Pre-flight.** Take the latest ASB snapshot if it is fresher than one heartbeat interval, otherwise
   query with `DLE EOT n=4` and `DLE EOT n=2`. If paper end, cover open, or a mechanical error is set,
   the job goes to `Blocked` and **no bytes are written**. `PrinterStatusChanged` and
   `TicketStatusChanged` go out. The job is re-queued automatically the moment the ASB reports the
   condition cleared.
5. **Render.** Build the ESC/POS payload for this ticket in the language configured for the location.
   Rendering is pure and has no side effects, so it is unit tested byte for byte.
6. **Take a process id.** The next value of that printer's `PrinterProcessId` counter, which lives in
   `NumberCounter` keyed by the printer's endpoint and therefore survives a crash and is shared by two
   locations on one machine. A counter that restarted at 1 could match a stale echo from a job sent
   before the restart, and a stale match turns a genuinely lost slip into a reported success, which is
   worse than an honest `Unknown`. **This is the first step at which `PrintJob.ProcessId` is anything
   but null**, which is why the column is nullable: a job that ended at step 2 or step 4 was never sent
   and has no legitimate value to hold, and a placeholder in that column would be a number a returning
   echo could match.
7. **Send.** Write the payload, then `GS ( H` requesting the process id response on print completion.
   Count bytes as they are written.
8. **Wait for the echo,** up to `JobTimeout` (90 seconds). Receiving the matching process id **on the
   same socket that sent the job** is the only thing that produces `Confirmed`. An echo arriving on a
   reconnected socket is discarded.
9. **Record the attempt** with its outcome, the phase it ended in, its byte count, and the status
   snapshot at the end, then map to the job and ticket states in section 3.
10. **Push.** `TicketStatusChanged` and, if it changed, `OrderStatusChanged`.

### 7.5 Status handling

* `GS a 15` is sent immediately after `ESC @` on every connect, which enables automatic status back for
  the drawer, online state, error state, and the roll paper sensor. The printer then sends a four byte
  status block on every change, unprompted.
* The worker parses every inbound four byte block that is not a process id response as an ASB block,
  decodes paper end, paper near end, cover open, and error state, and writes `PrinterStatus` when
  anything changed.
* `DLE EOT n=4` every 10 seconds is the heartbeat. It proves the socket is alive and covers a status
  change that arrived while the socket was down. Two consecutive unanswered heartbeats mark the printer
  offline and force a reconnect.
* **Paper near end is pushed and acted on.** Paper running out in the middle of a job is the single most
  common way this system produces an `Unknown`, and the near end sensor is the warning that prevents
  it. It appears as a row on the admin overview naming the station and asking for a fresh roll to be put
  ready. It is deliberately not shown on the phones: the person who can do something about it is at the
  laptop or at the station, and a warning shown to somebody who cannot act on it is noise that trains
  people to ignore the banner that matters.
* A status change never changes an order or a ticket by itself, with one exception: paper end clearing
  releases `Blocked` jobs at that printer, in sequence number order.

### 7.6 Retry, re-query, and the station circuit breaker

The rule in one sentence: **a job is retried automatically if and only if zero bytes reached the
printer.**

| Outcome | Bytes | Automatic retry | Job state | Ticket state |
|---|---|---|---|---|
| `Confirmed` | all | no | `Confirmed` | `Printed` |
| `Blocked` | 0 | yes, when the condition clears | `Blocked` | `Blocked` |
| `Unreachable` | 0 | yes, with backoff | `Queued` | `Queued`, then `Failed` after the give-up window |
| `SocketDropped` | 0 | yes, with backoff | `Queued` | `Queued`, then `Failed` after the give-up window |
| `SocketDropped` | > 0 | **never** | `Unknown` | `Unknown` |
| `Timeout` | 0 | yes, with backoff | `Queued` | `Queued`, then `Failed` after the give-up window |
| `Timeout` | > 0 | **never** | `Unknown` | `Unknown` |
| `PrinterError` | 0 | yes, when the error clears | `Blocked` | `Blocked` |
| `PrinterError` | > 0 | **never** | `Unknown` | `Unknown` |

**The table maps by outcome and byte count, and deliberately not by phase.** A `SocketDropped` during
`PreflightCheck` and a `SocketDropped` during `Sending` before the first byte are drawn as separate
arrows in section 3.3 because they are separate events on the wire, and `PrintAttempt.Phase` records
which one happened so the log can answer where the job was when the connection went. Neither one
changes the row that applies, because the only fact that decides whether a re-send is safe is whether
any byte reached the printer, and in both cases none did. A phase that changed an outcome would be a
second rule competing with the sentence above it, and one of the two would eventually be wrong.

The zero byte rows are the ones worth being explicit about. A socket that dies during the connect
handshake or before the first write is a common event on festival WiFi. Treating that as `Unknown`
would put a "walk to the kitchen and read the top of the pile" question on a phone several times an
evening for a slip that was never sent, and after the third one the crew learns to answer "Der Bon
liegt da" without walking. That destroys the mechanism for the case where it matters.

Re-query before re-sending, in the exact order:

1. Reconnect and read `DLE EOT n=1`, `n=2`, and `n=4`.
2. Write the result into `PrinterStatus` and push it, so the phone's question can include "the printer
   has no paper" when that is true.
3. **Stop.** Do not re-send. The status tells you the printer's condition, not whether the slip came
   out. The reprint is queued only after a human answers, through
   `POST /api/orders/{id}/tickets/{ticketId}/resolve` on the phone or the matching admin endpoint on
   the laptop.

A reprint keeps the ticket's numbers, increments `ReprintCount`, and prints a reprint banner.

**The station circuit breaker.** A printer that answers TCP but stops echoing, because of a jam, a
wedged firmware, or a cable half out, is the worst shape of failure this system can meet: every job
takes the full 90 seconds and then produces `Unknown`. At three orders a minute, ten minutes of that
leaves roughly 27 jobs waiting, the newest of which would not be attempted for another 40 minutes, and
seven separate "check the pile for Bon NNN" questions on four different phones, each about a slip
queued long enough ago that the server has forgotten the table. Nobody has been told that the station
itself is the problem.

So the worker stops. **When two consecutive attempts at one printer end `Unknown` or `Timeout`**, the
worker:

1. Sets `PrinterStatus.IsFaulty` and pushes `PrinterStatusChanged`, which reaches every phone, the
   admin, and that station's break-glass page at once.
2. Moves every waiting ticket at that station to `Failed` with `FailureReason: StationFaulty`, in one
   transaction, so every affected server is told at the same moment rather than one at a time over the
   next hour.
3. Attempts nothing further at that station.

A human ends it with the reconnect action on the printer screen, which clears `IsFaulty` and restarts
the worker. The tickets are reprinted from the order list or taken at the station on paper.

**Two consecutive unknown outcomes is the only trigger, and queue depth is deliberately not one.** An
earlier draft also tripped the breaker at ten waiting tickets, and that rule was wrong in the way that
matters most: it turned an ordinary paper change into a reported station fault. The paper runs out,
somebody walks off to find a roll, and at a busy bar ten orders take four minutes. The breaker fired,
ten tickets went to `Failed` in one transaction, and ten servers were told their order had failed
sixty seconds before the roll went in and all ten printed correctly. A crew that is told that twice in
an evening stops believing the message, and the message is the whole product. Depth is not a diagnosis
either way: ten slips waiting at a station whose printer has no paper is exactly what a working system
looks like at minute four of a paper change.

The two-unknowns trigger stays exactly as it was, because it detects the one thing nothing else
detects: a printer that answers and has stopped making sense. Section 3.0 is the general form of this
distinction, and the breaker is where it bites hardest.

**Depth is information, and it is shown as information.** The count of tickets waiting at a station
rides on `PrinterStatusChanged` as `waitingTicketCount` and appears in two places: appended to the
station banner on every phone with `header.stationWaiting`, so a banner reads "Der Drucker an der
Station Theke innen hat kein Papier. Dort warten 8 Bons.", and on the admin overview and printer screen
with `admin.overview.stationBlocked` and `admin.printers.waiting`. It changes nothing about what the
worker does. Nothing in the system reads a queue depth and takes an action.

### 7.7 The slip

The slip is user-facing output, so every fixed word on it is a localized resource string, resolved
through the same localization service as the rest of the backend. The language is a per location
setting, defaulting to German, because a kitchen crew reads one language.

**Physical parameters**

| Parameter | Value |
|---|---|
| Paper | 80 mm roll, 72 mm printable, 576 dots |
| Font | Font A, 12 dots wide, so 48 characters per line |
| Double size | `GS ! 0x11`, so 24 characters per line in the large regions |
| Code page | PC858, selected with `ESC t 19`. It contains ä, ö, ü, Ä, Ö, Ü, ß, and the euro sign. |
| Cut | `GS V 66 3`, feed and partial cut, leaving the slip hanging for one hand to tear off |

**Command sequence**

| Region | Commands | Content |
|---|---|---|
| Init | `ESC @`, `ESC t 19`, `GS a 15` | Reset, code page, enable status back |
| Reprint banner, only on a reprint | `ESC a 1`, `GS ! 0x11`, `ESC E 1` | `NACHDRUCK` / `REPRINT`, then the reprint time in normal size |
| Location name | `ESC a 1`, `GS ! 0x11`, `ESC E 1` | Up to 24 characters, truncated with a full stop if longer. **The intended station, and the first thing on every slip.** |
| Sequence number | `ESC a 1`, `GS ! 0x11`, `ESC E 1` | `BON 042` / `SLIP 042` |
| Order header | `ESC a 0`, `GS ! 0x00`, `ESC E 1` for the number line | Order number, table, server, time |
| Lines | `ESC a 0`, `GS ! 0x00` | Quantity, item, and any line note indented by four spaces |
| Footer | `ESC a 0` | Item count, order note, the other stations this order went to, and the chosen station when it differs |
| Station card, only on a test slip | `ESC a 1`, the `GS ( k` sequence below, `ESC a 0` | The QR code to this station's break-glass page, with the same URL underneath as text |
| Finish | `ESC d 4`, `GS V 66 3` | Feed and cut |

**The station name at the top is what makes a shared printer safe, and it needs no change to do it.**
When two locations print on one machine (section 2.12), both stations' slips come off one roll
interleaved, so the person tearing them off has to sort them without reading carefully. The location
name is already the first region after the init sequence, centred, bold, and at double width and double
height, which is 24 columns of 72 mm paper: `KÜCHE` and `THEKE ZELT` are legible across a marquee
before anyone reads a single item line. Nothing in this layout was added for the shared case and
nothing needs to be. The only slip that carries a different first line is a reprint, whose banner sits
above the station name, and that banner is itself the warning it is meant to be.

**The QR code on a test slip.** The test print in section 5.5 carries the station card, and that card,
taped inside the printer lid, is the whole of how the 32 character `StationAccessKey` reaches the people
who need it at 21:00 (sections 2.4 and 8.10). So the renderer has to print a two-dimensional barcode,
and the command sequence is specified here rather than left as something the implementer will work out.

The `GS ( k` functions, in the order they are sent:

| Step | Bytes | Meaning |
|---|---|---|
| Select the model | `1D 28 6B 04 00 31 41 32 00` | `cn` 49, `fn` 65. Model 2, which is the model every phone camera reads. |
| Set the module size | `1D 28 6B 03 00 31 43 06` | `fn` 67, six dots per module |
| Set the error correction level | `1D 28 6B 03 00 31 45 31` | `fn` 69, `n` 49, level M |
| Store the data | `1D 28 6B pL pH 31 50 30`, then the URL as bytes | `fn` 80, `m` 48. `pL + pH * 256` is the length of the URL plus three. |
| Print what is stored | `1D 28 6B 03 00 31 51 30` | `fn` 81, `m` 48 |

**What is encoded is the station's break-glass URL and nothing else**, in the form
`http://192.168.1.23:5000/station/8f2a1c...`, built by the same code that builds the URL for the admin's
own station card rather than assembled a second time, per hard rule 6. Its longest realistic form is 7
characters of scheme, 15 of address, 6 of port, 9 of `/station/` and 32 of key, so 69 bytes.

**Why six dot modules and level M.** Sixty-nine bytes in byte mode at level M needs QR version 5, which
is 37 modules square. At 203 dots per inch one dot is 0.125 mm, so a six dot module is 0.75 mm and the
symbol is 222 dots, 27.8 mm, across. The four module quiet zone on each side adds 48 dots, which gives
270 dots of the 576 the print head has and 33.8 mm of the 72 mm printable width, so the symbol fits with
room to spare and `ESC a 1` centres it. Level L would fit version 4 and a slightly smaller symbol, but
this card spends a season taped inside a warm printer lid and thermal paper fades, so 15 percent
recovery is worth the four extra modules. Level H would push the symbol to version 7 for a robustness no
phone camera at this size can use. A 0.75 mm module is comfortably above what a phone reads at arm's
length in a dark marquee.

**The URL is printed underneath the symbol as plain text as well**, wrapped over two lines at 48
columns. That is what somebody reads out when a symbol will not scan, and it is what
`MockPrinterTransport` writes into its file, because a text file cannot hold a symbol (section 7.8).

**This sequence is an assumption until a printer is on a desk.** `GS ( k` is documented for the TM
series, but the TM-T20IV's firmware has not been checked, exactly as `GS ( H` has not been checked.
Open question 11 records it with its fallback.

**The time on the slip is the time the order was taken**, never the time it was printed. On a reprint
40 minutes later the two differ, and a print time on a slip that reuses the original sequence number
would tell the kitchen that a 40 minute old order had just arrived. The reprint time is printed once,
under the reprint banner, where it belongs.

The price total is **not** printed on the slip. The kitchen does not need it, and printing a total next
to a list of goods is the closest this product would ever come to looking like a receipt.

**Rendered example, German, 48 columns**

```
================================================
KÜCHE
================================================
BON 042
================================================
Bestellung 137
Tisch 12
Bedienung: Anna
26.08.2026, 19:42 Uhr
------------------------------------------------
2 x Bratwurst mit Brot
1 x Pommes groß
    Hinweis: ohne Ketchup
3 x Kartoffelsalat
------------------------------------------------
Artikel gesamt: 6
Hinweis: Ein Teller extra für ein Kind.
Diese Bestellung geht auch an: Theke
================================================
```

**Rendered example, English, 48 columns**

```
================================================
KITCHEN
================================================
SLIP 042
================================================
Order 137
Table 12
Server: Anna
26/08/2026, 19:42
------------------------------------------------
2 x Sausage with bread
1 x Chips, large
    Note: no ketchup
3 x Potato salad
------------------------------------------------
Items in total: 6
Note: One extra plate for a child.
This order also goes to: Bar
================================================
```

The footer counts units, not lines: three lines with quantities 2, 1 and 3 print `Artikel gesamt: 6`.
"Position" means one line and "Artikel" means one unit, in that order of size, and the two words are
never swapped anywhere in the product.

**Rendered example, reprint header, German**

```
================================================
NACHDRUCK
Nachdruck um 20:31 Uhr
================================================
KÜCHE
================================================
BON 042
================================================
Bestellung 137
Tisch 12
```

The reprint banner exists so that two slips with the same number on the same pile are immediately
distinguishable from two separate orders. In English the banner reads `REPRINT`.

**Rendered example, test slip with the station card, German, 48 columns**

```
================================================
KÜCHE
================================================
TESTBON
26.08.2026, 17:05 Uhr
------------------------------------------------
         [ QR-Code, 37 x 37 Module ]
http://192.168.1.23:5000/station/
8f2a1c4b9d0e7f6a3b2c1d0e9f8a7b6c
------------------------------------------------
Kleben Sie diese Karte in den Deckel des
Druckers. Wenn der Drucker ausfällt, führt der
QR-Code zur Notfallseite dieser Station.
================================================
```

**Rendered example, test slip with the station card, English, 48 columns**

```
================================================
KITCHEN
================================================
TEST SLIP
26/08/2026, 17:05
------------------------------------------------
          [ QR code, 37 x 37 modules ]
http://192.168.1.23:5000/station/
8f2a1c4b9d0e7f6a3b2c1d0e9f8a7b6c
------------------------------------------------
Tape this card inside the printer lid. If the
printer fails, the QR code opens this station's
emergency page.
================================================
```

The bracketed line is the symbol itself, which no example in a text document can show. A test slip
carries no order number and no sequence number, because it belongs to a printer rather than to an order
(section 2.11).

**When a line went to a different station than the server chose**, because that station was switched
off between the catalog fetch and the order, the footer carries one more line: `Gewählt war: Theke
Zelt` / `Chosen station was: Bar marquee`. The receiving station can then see at a glance that the
order came to it because somewhere else went dark.

Truncation rules: no information on a slip is ever dropped to make it fit. An item name longer than the
printable width wraps onto a continuation line indented by four spaces. A table label, a server name,
and a station name wrap the same way. Every line on the slip is a single field, so nothing shares a
line with anything that could push it off the paper.

### 7.8 MockPrinterTransport

The mock is a product feature, not a test fixture that leaked into this document. No printer has been
bought, and none will be bought until the fire department has run an evening on the mock and said the
system is a tool they want, so every demonstration between now and that decision runs on it. It stays
the test double afterwards.

It behaves like a real printer session: it holds one connection at a time, it emits an ASB style status
stream, it answers `QueryStatusAsync`, and it returns the same `PrintDispatchResult` record. What it
does instead of printing is write a file.

**One slip, one file.**

| Part | Value |
|---|---|
| Root folder | `mock-slips`, in the same directory as the database file, whose path is the `appsettings.json` setting in section 10.4 |
| Folder per location | `{sanitised location name}-{first eight characters of the location id}` |
| File name | `{session start, yyyyMMdd-HHmmss}_{folder name of the location}_slip-{sequence, three digits}_print-{n}.txt` |
| File name of a test print | `{session start, yyyyMMdd-HHmmss}_{folder name of the location}_test-{process id}.txt` |
| Content | `PrintPayload.RenderedText` verbatim, which is exactly the lines the ESC/POS payload would have put on the paper |
| Encoding | UTF-8 with no byte order mark, lines separated by carriage return and line feed |

Sanitising a location name replaces every character that is not a letter, a digit, a hyphen or an
underscore with an underscore. The eight character id suffix is what keeps two stations both called
"Theke" apart, and the readable half is what lets somebody find the right folder without opening it.

`{n}` in `print-{n}` is `ReprintCount + 1`, so the first print of slip 042 is `print-1` and its first
reprint is `print-2`. A reprint deliberately keeps the sequence number of the slip it repeats (section
3.4), so without that counter a reprint would overwrite the file it is meant to lie beside. The session
start does the same job across evenings, because sequence numbers restart at 1 with every event session
(section 4.3). The location is named in the file as well as in the folder, so a file copied out of its
folder still says where it belongs.

A QR code cannot exist in a text file. On a test slip (section 7.7) the mock's file therefore carries
the break-glass URL as a line of text, which is the string the QR code encodes, so a demonstrator can
still open that station's page.

**The mock has no artificial delay.** It writes the file and returns. The 400 millisecond render that
made a demonstration look like real printing is gone: it slowed every integration test and showed
nothing that a folder of files does not show already. A test that needs to watch a job while it is still
in flight arms `UnknownOutcome`, which holds the job in `Printing` until `JobTimeoutSeconds` expires.

**A folder that cannot be written is a printer fault and is reported as one.** When a worker starts, it
creates its location's folder and writes and deletes a probe file in it. If any of that fails,
`PrinterStatus` goes offline with `IsInErrorState` set and `LastDetail` naming the full path and the
reason the operating system gave, and every job at that station returns `PrinterError` with zero bytes
written. That is the `PrinterError` with zero bytes row of the table in section 7.6, so the ticket goes
`Blocked` and the phone is told the station needs a human. This is one of the four known causes in
section 3.2, because the message names a folder and a person can move the program into one they are
allowed to write: the give-up window is suspended while it holds, and the 20 minute outer bound ends it
in `Failed` rather than in silence if nobody acts. The admin printer screen shows
`admin.printers.mockFolderUnwritable` with the path in it. Nothing is ever reported as printed when no
file was written.

**A ticket printed by the mock reaches `PrintedOnTestPrinter`, never `Printed`.** That distinction is
the whole defence against the trap in section 3.5: a station whose printer was never configured is
created on the mock, and if the mock reported `Printed` then that station would report every order all
evening as successfully printed, to the phone, to the admin list, and to the attention filter, while the
files piled up in a folder nobody has opened. The volunteer who set up two of three stations and missed
the third would learn about it from a guest.

During a practice run that state is shown as the normal outcome it is. During a real event it puts the
order in `NeedsAttention` and tells the placing server that the order went to the test printer. Starting
a real event refuses while any active station is still on the mock, which is the check that stops it
from happening at all.

**Fault injection stays, and it is one control.** The tests need every failure mode, and a person
demonstrating the system needs to be able to produce one on request. That is the whole requirement, so
a fault is armed in exactly two ways: `POST /api/admin/mock/{locationId}/fault` (section 5.5), and one
row on the admin printer screen holding the list of faults and the choice between `Once` and `Sticky`.
There is no button per fault, and no fake station screen to press it on.

| Fault | What the mock does | Outcome returned | Bytes written | Resulting ticket state |
|---|---|---|---|---|
| `None` | Writes the file and returns | `Confirmed` | all | `PrintedOnTestPrinter` |
| `PaperEnd` | Reports paper end in its status stream and refuses at pre-flight | `Blocked` | 0 | `Blocked` |
| `CoverOpen` | Reports cover open, refuses at pre-flight | `Blocked` | 0 | `Blocked` |
| `ConnectTimeout` | Never completes `ConnectAsync` | `Unreachable` | 0 | `Queued`, then `Failed` |
| `DropSocketEarly` | Drops before the first byte, writing no file | `SocketDropped` | 0 | `Queued`, then `Failed` |
| `DropSocketMidJob` | Writes about half the text to the file, then throws as if the socket died | `SocketDropped` | partial | `Unknown` |
| `UnknownOutcome` | Accepts the whole payload, writes no file, and never sends the process id echo | `Timeout` | all | `Unknown` |

A half-written file is what a half-printed slip looks like, which is the point of `DropSocketMidJob`.
`UnknownOutcome` writes nothing, so a demonstrator who walks to the folder and answers the question in
section 3.4 honestly answers that the slip is missing, and the reprint path runs.

`PaperEnd` armed as `Sticky` is cleared by arming `None`, which is the same sequence a real paper change
produces: blocked, then printed by itself with nobody re-sending anything.

Every failure mode the real transports can produce is reproducible in the mock. That is the acceptance
criterion for the mock, and section 11 lists the tests that hold it.

---

## 8. Frontend screens

### 8.1 How the text on every screen is written

These rules bind every string in this section and every string added later.

* **German and English are both complete.** A key that exists in one language is an unfinished change.
  Strings live in vue-i18n resource files, never as literals in a template. The two languages carry the
  same placeholders.
* **German uses the Sie form throughout.** The tool is handed to volunteers who may not know each other,
  and mixing du and Sie across screens reads as sloppy. One form, everywhere, including the printed
  slips.
* **Guidance is a complete sentence with a verb at the front.** "Legen Sie eine neue Papierrolle ein."
  Not "Papier leer".
* **A message about a problem names the next step first and the cause second.** A message that only
  names a cause leaves a volunteer holding a phone with no idea what to do.
* **At most three sentences in any guidance block.** If more is needed, the block has more than one job
  and belongs at more than one place on the screen.
* **The app checks whatever it can check instead of writing a sentence about it.** A disabled button
  with a reason underneath beats a paragraph nobody reads. Where a rule appears below as text, it is
  because the app cannot know the answer.
* **One rule is stated in exactly one place.** The same rule written twice at two strengths reads as
  two rules and the reader cannot tell which one binds.
* **No jargon.** The words token, sync, queue, endpoint, session, and cache never appear on a screen.
  "Der Laptop" and "das WLAN" are the two technical nouns a volunteer already owns.
* **One term per concept per language.** A printed slip is always "Bon" in German and "slip" in English.
  A production location is always "Station" in German and "station" in English, and is called by its own
  name ("Küche", "Theke innen") wherever a specific one is meant.
* **The two numbers have one word each.** Always "Bestellung 137" or "Order 137" for the order, always
  "Bon 042" or "Slip 042" for the slip. No "Nr.", no "#", no "No.", on any screen or any piece of
  paper. Section 4.4 says why this is a safety rule and not a style preference.

### 8.2 Screen map

| Screen | Audience | Where |
|---|---|---|
| Enrolment by QR code | Server | `/j/{code}` |
| Enrolment with a six digit code | Server | `/` when the phone has no token |
| Catalog and order building | Server | `/` |
| Review and total | Server | `/review` |
| Order list and order detail | Server | `/orders` |
| Settings sheet | Server | Opened from the header |
| Admin configuration | Admin, on the laptop | `/admin/...` |
| Break-glass station page | Station staff, in an emergency | `/station/{accessKey}` |

There is no shift-start screen. A server who has just set up their phone lands on the catalog and can
take an order immediately.

### 8.3 Enrolment by QR code

**Purpose.** Turn a phone that has never seen the tool into an enrolled device, in under fifteen
seconds, during a briefing.

**What is on it.** The name picker, filled from the admin's list of staff, a way to type a name that is
not in the list, and one button. Nothing else.

**What the user can do.** Pick a name or type one, then continue. On success the phone stores its token
and goes straight to the catalog.

**The scan happens in the phone's own camera app**, which opens the URL in the browser. The web app
never asks for the camera. It cannot: `getUserMedia` needs a secure context and this product is served
over plain HTTP, so an in-page scanner is not a feature that was skipped, it is a feature that cannot
exist here.

| Key | Deutsch | English |
|---|---|---|
| `enrol.title` | Dieses Telefon einrichten | Set up this phone |
| `enrol.intro` | Wählen Sie Ihren Namen aus. Danach können Sie Bestellungen aufnehmen. | Choose your name. After that you can take orders. |
| `enrol.nameLabel` | Ihr Name | Your name |
| `enrol.notInList` | Mein Name steht nicht in der Liste | My name is not in the list |
| `enrol.typeName` | Namen eingeben | Enter your name |
| `enrol.continue` | Weiter | Continue |
| `enrol.error.nameMissing` | Geben Sie Ihren Namen ein, damit die Küche sieht, wer die Bestellung aufgenommen hat. | Enter your name so the kitchen can see who took the order. |
| `enrol.error.codeUsed` | Scannen Sie den QR-Code noch einmal. Der Code auf dem Laptop wechselt alle 30 Sekunden, und jeder gilt für ein Telefon. | Scan the QR code again. The code on the laptop changes every 30 seconds and each one is for one phone. |
| `enrol.error.locked` | Warten Sie fünf Minuten und scannen Sie den QR-Code dann noch einmal. Von diesem Telefon wurde zu oft ein falscher Code gesendet. | Wait five minutes and then scan the QR code again. A wrong code was sent from this phone too often. |
| `enrol.error.noConnection` | Prüfen Sie, ob Sie im WLAN des Festes sind. Dieses Telefon erreicht den Laptop nicht. | Check that you are on the festival WiFi. This phone cannot reach the laptop. |
| `enrol.orderHeld` | Ihre angefangene Bestellung ist noch da. Sie steht wieder auf dem Bildschirm, sobald das Telefon eingerichtet ist. | The order you had started is still here. It comes back on the screen as soon as the phone is set up. |
| `enrol.success` | Das Telefon ist eingerichtet. | Your phone is ready. |

`enrol.error.codeUsed` used to tell the reader to ask the person at the laptop for a new code. During a
briefing that is the wrong instruction, because the laptop is already showing a fresher code and
rotating every thirty seconds. The instruction has to be the one that works from where the reader is
standing.

### 8.4 Enrolment with a six digit code

**Purpose.** The fallback when a camera does not work, or when a phone cannot open a QR code. The
laptop shows its own address in large type next to the QR image, so the volunteer types the address
into the browser and lands here.

| Key | Deutsch | English |
|---|---|---|
| `enrolCode.title` | Einrichten mit dem sechsstelligen Code | Set up with the six digit code |
| `enrolCode.intro` | Geben Sie den sechsstelligen Code ein, der auf dem Laptop steht. | Enter the six digit code shown on the laptop. |
| `enrolCode.validity` | Jeder Code gilt fünf Minuten und kann nur einmal verwendet werden. | Each code is valid for five minutes and can be used only once. |
| `enrolCode.field` | Code | Code |
| `enrolCode.continue` | Weiter | Continue |
| `enrolCode.error.wrong` | Lesen Sie die sechs Ziffern noch einmal vom Laptop ab. Dieser Code stimmt nicht. | Read the six digits from the laptop again. This code is not correct. |

The numeric keypad is opened by `inputmode="numeric"`, the field accepts digits only, and the button
stays disabled until six digits are present. That is the app checking what it can check, so no sentence
about the code's length is needed.

### 8.5 The header, always visible

**Purpose.** Two facts the server needs without looking for them: whether the phone is talking to the
laptop, and whether any of their orders needs them.

**What is on it.** The link to the order list on the left, carrying a count when something needs
checking, and the connection state on the right. When the connection is healthy and nothing needs
checking, both sides are quiet. A permanent "connected" badge would train people to ignore that corner.
A station banner appears under the header when a printer has a problem, because the server should know
before they take the next order rather than after they send it.

The settings button opens a sheet with the language choice and the name this phone is serving under.
Language is a device setting, so a server whose phone is set to English gets English from the app and
in every message the laptop sends them.

**`header.stationWaiting` is appended to whichever station banner is already showing**, rather than
being a banner of its own. A station with a problem produces one banner, which names the station and
the one thing to do about it, and the count of waiting slips is the second sentence of that banner:
"Der Drucker an der Station Theke innen hat kein Papier. Dort warten 8 Bons." The count is what tells a
server how much of the evening is parked behind the roll, and section 7.6 says why it is shown and
never acted on. It appears once the station has been held for longer than the give-up window, so an
ordinary two second queue never draws it.

**Every string with a `{count}` placeholder is resolved through vue-i18n's plural forms**, so a count
of one reads correctly in both languages rather than producing "1 Bons". The tables in this section
give the general form, and the singular is written alongside it in the resource file.

| Key | Deutsch | English |
|---|---|---|
| `header.reconnecting` | Keine Verbindung zum Laptop. Es wird weiter versucht. | No connection to the laptop. The app keeps trying. |
| `header.backOnline` | Die Verbindung ist wieder da. | The connection is back. |
| `header.attention` | {count} Bestellungen müssen geprüft werden. | {count} orders need checking. |
| `header.stationPaperOut` | Der Drucker an der Station {name} hat kein Papier. | The printer at {name} has no paper. |
| `header.stationOffline` | Die Station {name} antwortet gerade nicht. | Station {name} is not answering right now. |
| `header.stationFaulty` | Sagen Sie an der Station {name} Bescheid. Der Drucker dort nimmt nichts mehr an. | Tell the people at {name}. The printer there is not accepting anything any more. |
| `header.stationWaiting` | Dort warten {count} Bons. | {count} slips are waiting there. |
| `header.settings` | Einstellungen | Settings |
| `settings.title` | Einstellungen | Settings |
| `settings.person` | Sie bedienen als {name}. | You are serving as {name}. |
| `settings.language` | Sprache | Language |
| `settings.languageGerman` | Deutsch | German |
| `settings.languageEnglish` | Englisch | English |

### 8.6 Catalog and building an order

**Purpose.** Turn what a guest says into lines and quantities with as few taps as possible, one handed,
in the dark.

**What is on it.**

* Category strip across the top, horizontally scrollable.
* A grid of item buttons. Each shows the name and the price. Touch targets are at least 56 by 56
  logical pixels with 8 pixels of spacing.
* Tapping an item adds one. Once an item has a quantity, a minus button and the count appear in the
  button itself, so adding and removing never move the finger far.
* **A sold-out item stays in the grid**, greyed, not tappable, with `catalog.soldOut` under the name.
  Hiding it would send a server searching the categories for something that was there a minute ago. An
  item that was deactivated at the laptop is a different thing and is not in the catalog at all
  (section 2.5), because it is not on this festival's menu.
* A banner at the top when a station has no paper, is not answering, or has been declared faulty.
* The basket bar at the bottom, always visible, showing the number of items, the running total, and the
  button to the summary.

**What the user can do.** Add and remove quantities, add a note to a line, and move on to the summary.

**Where an item is prepared.** Most items can be prepared in exactly one place, and for those the server
is never asked and no station control is drawn. That is the normal case at a site with one kitchen and
one bar, and it is meant to be invisible. For the few items that more than one station can produce, for
example beer at a site with two bars, tapping the item opens a sheet with one large button per station,
and the line is added once the server picks. The choice is shown on the line, is changeable until the
order is sent, and applies to that line only. Nothing is remembered for the next line or the next order,
because a server carrying a tray to the marquee and then one to the terrace would otherwise be fighting
a setting they never set.

| Key | Deutsch | English |
|---|---|---|
| `catalog.title` | Bestellung aufnehmen | Take an order |
| `catalog.searchPlaceholder` | Artikel suchen | Search for an item |
| `catalog.soldOut` | Ausverkauft | Sold out |
| `catalog.lineNote` | Hinweis für diese Position | Note for this item |
| `catalog.lineNotePlaceholder` | Zum Beispiel: ohne Zwiebeln | For example: no onions |
| `catalog.basketEmpty` | Noch nichts ausgewählt | Nothing chosen yet |
| `catalog.basketSummary` | {count} Artikel, {total} | {count} items, {total} |
| `catalog.toReview` | Weiter zur Übersicht | Go to the summary |
| `catalog.paperWarning` | Der Drucker an der Station {name} hat kein Papier. Sie können weiter bestellen, der Bon wird gedruckt, sobald Papier eingelegt ist. | The printer at {name} has no paper. You can keep ordering, and the slip prints as soon as paper is loaded. |
| `catalog.offlineWarning` | Die Station {name} antwortet gerade nicht. Sie können weiter bestellen, der Bon wird nachgedruckt. | Station {name} is not answering right now. You can keep ordering and the slip prints later. |
| `catalog.itemSoldOut` | Fragen Sie den Gast, ob er etwas anderes möchte. {name} ist gerade ausverkauft. | Ask the guest whether they would like something else. {name} has just sold out. |
| `line.whereTitle` | Wo soll {item} zubereitet werden? | Where should {item} be prepared? |
| `line.whereHelp` | Die Auswahl gilt nur für diese Position. | The choice applies to this item only. |
| `line.station` | Station: {name} | Station: {name} |
| `line.changeStation` | Station ändern | Change the station |

**When an item sells out while it is already in the basket**, the quantity stays exactly where it is
and the line is flagged with `catalog.itemSoldOut`. Silently deleting a line a guest already ordered
would be a change the server never sees. The flag usually arrives while they are still standing at the
table, which is the point of the live push: they ask the guest for a second choice instead of coming
back to it with a tray. If they send it anyway, the order is accepted (section 5.4), the slip prints,
and the station sends word back. The guest ordered it and the cash may already be counted, so refusing
at that moment would solve nothing and lose the order.

### 8.7 Review and total

**Purpose.** Two jobs: name the table, and show the total large enough to read out at arm's length in
the dark.

**What is on it.**

* The lines, grouped by the station they will be printed at, so the split is visible before sending.
* The table field, with suggestion chips above it.
* An optional note for the whole order.
* The total, in the largest type on the screen.
* One sentence under the total saying what the total is for.
* The send button, full width, at the bottom.

**What the user can do.** Change quantities, change the station on a line that has a choice, set the
table, add a note, and send. The send button is disabled while the table field is empty, with the reason
directly under the button.

| Key | Deutsch | English |
|---|---|---|
| `review.title` | Bestellung prüfen | Check the order |
| `review.tableLabel` | Tisch | Table |
| `review.tablePlaceholder` | Zum Beispiel: Tisch 12 | For example: Table 12 |
| `review.tableHelp` | Tragen Sie den Tisch ein, damit das Tablett zum richtigen Tisch kommt. | Enter the table so the tray reaches the right table. |
| `review.tableMissing` | Tragen Sie einen Tisch ein, bevor Sie senden. | Enter a table before you send. |
| `review.orderNote` | Hinweis für die Küche | Note for the kitchen |
| `review.goesTo` | Geht an {name} | Goes to {name} |
| `review.total` | Gesamt | Total |
| `review.totalHelp` | Der Betrag ist nur eine Rechenhilfe. Das Geld nehmen Sie wie bisher am Tisch ein. | The amount is only an aid for adding up. You take the cash at the table as before. |
| `review.send` | Bestellung senden | Send order |
| `review.sending` | Wird gesendet | Sending |
| `review.sent` | Bestellung {number} ist angekommen. | Order {number} has arrived. |
| `review.totalChanged` | Sagen Sie dem Gast die neue Summe: {total}. Der Preis wurde gerade am Laptop geändert. | Tell the guest the new total: {total}. The price was changed at the laptop a moment ago. |
| `review.back` | Zurück zur Auswahl | Back to the items |

**When sending fails.** The screen keeps the order exactly as it was, with every line, quantity, station
and the total, and shows the failure with the retry button directly under it. Nothing is cleared, and
nothing is sent in the background. Section 9 describes the mechanism and why it is deliberately this
plain.

| Key | Deutsch | English |
|---|---|---|
| `review.sendFailed` | Tippen Sie auf "Noch einmal senden". Der Laptop war nicht erreichbar, die Bestellung steht noch vollständig hier. | Tap "Send again". The laptop could not be reached, and the order is still here in full. |
| `review.retry` | Noch einmal senden | Send again |
| `review.sendFailedAgain` | Schreiben Sie die Bestellung auf Papier und bringen Sie sie zur Station. Das Senden hat mehrmals nicht geklappt. | Write the order on paper and take it to the station. Sending has failed several times. |
| `review.duplicateRisk` | Sehen Sie in Ihren Bestellungen nach, bevor Sie diese noch einmal aufnehmen. Sie wurde bereits gesendet. | Check your orders before you take this one again. It has already been sent. |

Prices are formatted by locale: `10,50 €` in German and `€10.50` in English. Both use the euro sign
because the money is euros in both languages, and the symbol goes where each language puts it, because
this is the number a server reads out loud.

### 8.8 Order list and order detail

**Purpose.** Answer one question at a glance: did my orders actually reach the kitchen. This screen is
the reason the product exists, and every state on it must be distinguishable without reading carefully.

**What is on it.** The orders this **person** placed today, newest first, each row showing the order
number, the table, the total, and one status chip. A phone that was set up again mid-evening shows the
same list, because the list belongs to the human, not to the handset. Tapping a row opens the detail,
which lists every station slice with its own slip number, its own state, and the action that state
needs.

| Key | Deutsch | English |
|---|---|---|
| `orders.title` | Meine Bestellungen | My orders |
| `orders.empty` | Sie haben heute noch keine Bestellung aufgenommen. | You have not taken an order yet today. |
| `orders.row` | Bestellung {number}, {table} | Order {number}, {table} |
| `orders.status.printing` | Wird gedruckt | Printing |
| `orders.status.printed` | Gedruckt | Printed |
| `orders.status.attention` | Bitte prüfen | Please check |
| `orders.ticket` | {station}, Bon {sequence} | {station}, slip {sequence} |
| `orders.detailTitle` | Bestellung {number} | Order {number} |

The three states stay distinguishable by colour, by icon, and by wording at the same time, so none of
the three carries the whole signal on its own.

**Messages for each failure, with the action first**

| Key | Deutsch | English |
|---|---|---|
| `ticket.unknown.action` | Schauen Sie am Stapel bei {station} nach Bon {sequence} und antworten Sie hier. | Check the pile at {station} for slip {sequence} and answer here. |
| `ticket.unknown.reason` | Die Verbindung zum Drucker ist abgerissen, während der Bon gesendet wurde, deshalb ist hier nicht bekannt, ob er gedruckt wurde. | The connection to the printer broke while the slip was being sent, so nobody can tell from here whether it printed. |
| `ticket.unknown.paperHint` | Der Drucker hat außerdem kein Papier mehr. | The printer has also run out of paper. |
| `ticket.unknown.yes` | Der Bon liegt da | The slip is there |
| `ticket.unknown.no` | Der Bon fehlt | The slip is missing |
| `ticket.unknown.answered` | Diese Frage wurde bereits beantwortet. | This question has already been answered. |
| `ticket.paperEnd` | Legen Sie eine neue Papierrolle in den Drucker bei {station}. Der Bon wird danach von selbst gedruckt. | Put a new paper roll into the printer at {station}. The slip prints by itself afterwards. |
| `ticket.coverOpen` | Schließen Sie die Klappe am Drucker bei {station}. Der Bon wird danach von selbst gedruckt. | Close the cover on the printer at {station}. The slip prints by itself afterwards. |
| `ticket.failed` | Sagen Sie die Bestellung {number} bei {station} persönlich an. Der Drucker dort antwortet seit fünf Minuten nicht. | Tell {station} about order {number} in person. The printer there has not answered for five minutes. |
| `ticket.failedAfterWaiting` | Sagen Sie die Bestellung {number} bei {station} persönlich an. Der Bon wartet dort seit {minutes} Minuten auf den Druck. | Tell {station} about order {number} in person. The slip has been waiting there {minutes} minutes to print. |
| `ticket.stationFaulty` | Sagen Sie die Bestellung {number} bei {station} persönlich an. Der Drucker dort nimmt nichts mehr an. | Tell {station} about order {number} in person. The printer there is not accepting anything any more. |
| `ticket.stationDisabled` | Sagen Sie die Bestellung {number} bei {station} persönlich an. Die Station ist am Laptop ausgeschaltet. | Tell {station} about order {number} in person. The station is switched off at the laptop. |
| `ticket.printerError` | Holen Sie jemanden, der sich den Drucker bei {station} ansehen kann. Der Drucker meldet eine Störung. | Fetch somebody who can look at the printer at {station}. The printer is reporting a fault. |
| `ticket.testPrinter` | Sagen Sie die Bestellung {number} bei {station} persönlich an. Die Station steht am Laptop noch auf dem Testdrucker, es liegt also kein Bon auf dem Stapel. | Tell {station} about order {number} in person. The station is still on the test printer at the laptop, so there is no slip on the pile. |
| `ticket.reprintQueued` | Der Bon wird noch einmal gedruckt. Er trägt wieder dieselbe Nummer und den Vermerk Nachdruck. | The slip is printed again. It carries the same number again and is marked as a reprint. |
| `ticket.handledOnPaper` | Die Station hat diese Bestellung vom Bildschirm übernommen. Es wird kein Bon mehr gedruckt. | The station has taken this order from the screen. No slip will be printed. |
| `ticket.reprint` | Erneut drucken | Print again |
| `order.changedMind` | Sagen Sie der Station Bescheid und nehmen Sie die Änderung als neue Bestellung auf. Eine gesendete Bestellung lässt sich hier nicht zurücknehmen, weil der Bon schon gedruckt wird. | Tell the station and take the change as a new order. A sent order cannot be taken back here, because its slip is already printing. |

`order.changedMind` stands in the order detail, on every order, because that is where a server is
looking when a guest changes their mind. It is the whole of section 3.6 in two sentences, at the point
of action.

**`ticket.paperEnd` and `ticket.coverOpen` keep saying that the slip prints by itself, and they keep
saying it for as long as it is true.** The give-up window does not run while one of those is the cause
(section 3.2), so a ticket showing one of these messages is not counting down towards a failure and the
message does not have to hedge. `ticket.failedAfterWaiting` is what replaces it if the roll never goes
in, at the 20 minute outer bound, and it is the only message on this screen that reports a failure
whose cause the system knew all along. It names the minutes rather than the cause, because by then the
cause has stopped being the useful fact and the walk is.

### 8.9 Admin configuration

Runs on the laptop, in a browser, at `http://localhost:5000/admin`. It is a wider layout than the phone
app and shares the same localization files.

Opened from a phone, the same page renders one sentence and nothing else, because a volunteer who reads
the laptop's address off the overview screen and types it into a phone would otherwise get a broken
screen with no explanation.

| Key | Deutsch | English |
|---|---|---|
| `admin.notOnLaptop` | Öffnen Sie die Verwaltung direkt am Laptop unter {url}. | Open the admin pages on the laptop itself at {url}. |

**Overview.** The first screen. It is a readiness list, not a dashboard: every item is either done or
names exactly what is missing.

| Key | Deutsch | English |
|---|---|---|
| `admin.overview.title` | Übersicht | Overview |
| `admin.overview.ready` | Alles ist eingerichtet. Sie können jetzt die Telefone einrichten. | Everything is set up. You can now set up the phones. |
| `admin.overview.missingLocation` | Legen Sie mindestens eine Station an, zum Beispiel Küche und Theke. | Create at least one station, for example Kitchen and Bar. |
| `admin.overview.missingPrinter` | Tragen Sie für {name} einen Drucker ein. Solange dort der Testdrucker steht, kommt kein Bon auf den Stapel. | Set up a printer for {name}. While the test printer is set there, no slip reaches the pile. |
| `admin.overview.missingItems` | Legen Sie die Artikel mit ihren Preisen an. | Create the items with their prices. |
| `admin.overview.itemsWithoutLocation` | Ordnen Sie {count} Artikeln eine Station zu. Ohne Station können sie nicht bestellt werden. | Give {count} items a station. Without one they cannot be ordered. |
| `admin.overview.paperNearEnd` | Legen Sie bei {name} eine neue Papierrolle bereit. Die eingelegte Rolle geht zu Ende. | Put a new paper roll ready at {name}. The roll in the printer is running out. |
| `admin.overview.openTickets` | Sehen Sie in der Bestellliste nach. {count} Bons warten noch auf den Druck. | Check the order list. {count} slips are still waiting to print. |
| `admin.overview.stationBlocked` | Kümmern Sie sich um den Drucker bei {name}. Dort warten {count} Bons seit {minutes} Minuten. | Sort out the printer at {name}. {count} slips have been waiting there for {minutes} minutes. |
| `admin.overview.address` | Die Telefone erreichen den Laptop unter {url}. | Phones reach the laptop at {url}. |
| `admin.overview.addressChanged` | Lassen Sie alle Telefone den QR-Code neu scannen. Die Adresse des Laptops war zuletzt {previous} und ist jetzt {current}. | Have every phone scan the QR code again. The laptop's address was {previous} and is now {current}. |
| `admin.overview.console` | Lassen Sie das schwarze Fenster offen. Wenn Sie es schließen, nimmt das Programm keine Bestellungen mehr an. | Leave the black window open. If you close it, the program stops taking orders. |

**Stations, items, assignment, tables, staff.** Plain list and form screens. The strings that carry a
rule:

| Key | Deutsch | English |
|---|---|---|
| `admin.locations.title` | Stationen | Stations |
| `admin.locations.help` | Eine Station ist eine Küche oder eine Theke mit einem eigenen Drucker. | A station is a kitchen or a bar with its own printer. |
| `admin.locations.openTickets` | Diese Station hat noch {count} offene Bons und kann jetzt nicht abgeschaltet werden. | This station still has {count} open slips and cannot be switched off right now. |
| `admin.locations.lastForItems` | Ordnen Sie {names} zuerst eine andere Station zu. Diese Station ist für diese Artikel die einzige. | Give {names} a different station first. This station is the only one for those items. |
| `admin.locations.stationCard` | Stationskarte drucken | Print the station card |
| `admin.locations.stationCardHelp` | Kleben Sie die Karte in den Deckel des Druckers. Wenn der Drucker ausfällt, führt der QR-Code auf der Karte zur Notfallseite dieser Station. | Tape the card inside the printer lid. If the printer fails, the QR code on the card opens this station's emergency page. |
| `admin.items.title` | Artikel | Items |
| `admin.items.priceHelp` | Preise dienen nur zum Zusammenrechnen. Über die App wird kein Geld bezahlt. | Prices are only there for adding up. No money is paid through the app. |
| `admin.items.needsLocation` | Kreuzen Sie mindestens eine Station an. Ohne Station kann dieser Artikel nicht bestellt werden. | Tick at least one station. Without one this item cannot be ordered. |
| `admin.items.soldOut` | Ausverkauft | Sold out |
| `admin.items.soldOutHelp` | Schalten Sie einen Artikel auf ausverkauft, sobald er alle ist. Er bleibt auf den Telefonen stehen, ist aber nicht mehr auswählbar. | Switch an item to sold out as soon as it has run out. It stays on the phones and can no longer be chosen. |
| `admin.items.soldOutUndo` | Wieder verfügbar | Available again |
| `admin.items.deactivate` | Nicht auf der Karte | Not on the menu |
| `admin.items.deactivateHelp` | Nehmen Sie einen Artikel damit für dieses Fest ganz von der Karte. Auf den Telefonen erscheint er dann gar nicht mehr. | Take an item off the menu for this festival entirely. It then does not appear on the phones at all. |
| `admin.items.deactivateBlocked` | Schalten Sie den Artikel stattdessen auf "Ausverkauft". Während einer laufenden Veranstaltung lässt er sich nicht von der Karte nehmen. | Switch the item to "Sold out" instead. It cannot be taken off the menu while an event is running. |
| `admin.items.soldOutWalk` | Der Laptop ist die einzige Stelle, an der Sie das umschalten können. | The laptop is the only place where you can switch this. |
| `admin.assignment.title` | Zuordnung | Assignment |
| `admin.assignment.help` | Kreuzen Sie an, wo ein Artikel zubereitet werden kann. Bei einer Station läuft es von selbst, bei mehreren wählt die Bedienung beim Aufnehmen aus. | Tick where an item can be prepared. With one station it happens by itself, with several the server chooses while taking the order. |
| `admin.assignment.preview` | Vorschau: {item} geht an {location}. | Preview: {item} goes to {location}. |
| `admin.assignment.previewChoice` | Vorschau: Bei {item} wählt die Bedienung zwischen {locations}. | Preview: for {item} the server chooses between {locations}. |
| `admin.tables.title` | Tische | Tables |
| `admin.tables.help` | Diese Namen erscheinen als Vorschläge auf dem Telefon. Die Bedienung kann jederzeit einen anderen Tisch eintippen. | These names appear as suggestions on the phone. A server can always type a different table. |
| `admin.tables.fromLastSession` | Tischnamen der letzten Veranstaltung übernehmen | Add the table names from the last event |
| `admin.people.title` | Bedienungen | Servers |
| `admin.people.help` | Diese Namen stehen bei der Einrichtung eines Telefons zur Auswahl. | These names can be chosen when a phone is set up. |

**Sold out is a toggle in the item list, and that is a design requirement rather than a layout note.**
It is set by somebody who has just been told the kitchen is out of Bratwurst, and it is unset twenty
minutes later when somebody finds another crate. One tap each way, in the list, with no editor to open,
no form to fill and no dialog to confirm. Everything about it is reversible, nothing about it is
destructive, and a confirmation step would only make a busy person tap twice. Taking an item off the
menu lives in the item editor instead, where a considered edit belongs, and is refused during a live
event.

**Marking an item sold out means walking to the laptop, and the owner has accepted that for version 1.**
The admin screens answer only on the laptop (section 5.1), so there is no way to do this from a phone
and none is planned. The walk is short, it happens a handful of times an evening, and the person who
hears "we are out of Bratwurst" is usually within sight of the laptop anyway. `admin.items.soldOutWalk`
says so on the screen so nobody hunts for the control on their phone. Open question 3 records the
decision rather than leaving it open.

**Printers.**

| Key | Deutsch | English |
|---|---|---|
| `admin.printers.title` | Drucker | Printers |
| `admin.printers.kindNetwork` | Netzwerkdrucker | Network printer |
| `admin.printers.kindMock` | Testdrucker ohne Gerät | Test printer without hardware |
| `admin.printers.kindAgent` | Drucker am Raspberry Pi | Printer on a Raspberry Pi |
| `admin.printers.hostHelp` | Tragen Sie die IP-Adresse des Druckers ein oder suchen Sie ihn im Netz. | Enter the printer's IP address, or search the network for it. |
| `admin.printers.search` | Drucker im Netz suchen | Search the network for printers |
| `admin.printers.searching` | Es wird gesucht. Das dauert etwa eine Minute. | Searching. This takes about a minute. |
| `admin.printers.searchNone` | Tragen Sie die Adresse vom Selbsttest des Druckers ein. Es wurde kein Drucker gefunden. | Enter the address from the printer's self test. No printer was found. |
| `admin.printers.testPrint` | Testbon drucken | Print a test slip |
| `admin.printers.testPrintHelp` | Drucken Sie an jeder Station einen Testbon, bevor die Gäste kommen. | Print a test slip at every station before the guests arrive. |
| `admin.printers.online` | Antwortet | Answering |
| `admin.printers.offline` | Antwortet nicht | Not answering |
| `admin.printers.paperEnd` | Kein Papier | No paper |
| `admin.printers.paperNearEnd` | Papier geht zu Ende | Paper running out |
| `admin.printers.coverOpen` | Klappe offen | Cover open |
| `admin.printers.faulty` | Störung. Es wird nichts mehr an diesen Drucker gesendet. | Fault. Nothing more is being sent to this printer. |
| `admin.printers.reconnect` | Wieder verbinden | Connect again |
| `admin.printers.reconnectHelp` | Sehen Sie zuerst am Drucker nach Papierstau und Kabel. Danach nimmt der Drucker wieder Bons an. | Check the printer for a paper jam and a loose cable first. After that the printer accepts slips again. |
| `admin.printers.waiting` | {count} Bons warten auf diesen Drucker. | {count} slips are waiting for this printer. |
| `admin.printers.shared` | Diese Adresse ist auch bei {names} eingetragen. Die Bons dieser Stationen kommen aus demselben Drucker. | This address is also set at {names}. The slips for these stations come out of the same printer. |
| `admin.printers.sharedHelp` | Das ist so vorgesehen. Wenn ein Drucker ausfällt, tragen Sie bei dieser Station die Adresse eines Druckers ein, der noch arbeitet. | This is intended. When a printer fails, enter the address of a printer that is still working for that station. |
| `admin.printers.lastHeard` | Zuletzt gemeldet: {time} | Last heard from at {time} |
| `admin.printers.mockFolder` | Die Bons dieses Testdruckers liegen im Ordner {path}. | The slips from this test printer are in the folder {path}. |
| `admin.printers.mockFolderUnwritable` | Der Testdrucker bei {name} kann keine Bons ablegen. In den Ordner {path} lässt sich nichts schreiben. Legen Sie das Programm in einen Ordner, in dem Sie Dateien anlegen dürfen. | The test printer at {name} cannot put slips anywhere. Nothing can be written into the folder {path}. Move the program into a folder where you are allowed to create files. |
| `admin.printers.faultTitle` | Störung am Testdrucker simulieren | Simulate a fault on the test printer |
| `admin.printers.faultHelp` | Damit führen Sie vor, was am Telefon passiert, wenn ein Drucker ausfällt. Die Auswahl gilt nur für den Testdrucker. | This shows what happens on the phone when a printer fails. The setting applies to the test printer only. |
| `admin.printers.fault.none` | Keine Störung | No fault |
| `admin.printers.fault.paperEnd` | Kein Papier | No paper |
| `admin.printers.fault.coverOpen` | Klappe offen | Cover open |
| `admin.printers.fault.connectTimeout` | Drucker antwortet nicht | The printer does not answer |
| `admin.printers.fault.dropEarly` | Verbindung bricht ab, bevor etwas gesendet wurde | The connection breaks before anything is sent |
| `admin.printers.fault.dropMidJob` | Verbindung bricht mitten im Druck ab | The connection breaks in the middle of printing |
| `admin.printers.fault.unknown` | Drucker meldet nicht, ob er gedruckt hat | The printer does not report whether it printed |
| `admin.printers.fault.once` | Einmal | Once |
| `admin.printers.fault.sticky` | Bis zum Zurücksetzen | Until it is reset |

**`admin.printers.shared` appears on the row of every location that names the address**, so the two
stations describe each other and neither one looks like a stray edit. It is a statement of fact rather
than a warning, because it is a supported configuration and the most likely reason for it is that
somebody has just answered a dead printer correctly. `admin.printers.sharedHelp` sits under the address
field, where the admin is standing when a printer has died and they are deciding what to type.

The fault row appears only for a station whose transport is the test printer, and it is the whole of the
mock's user interface: the list of faults above, the choice between `Once` and `Sticky`, and the folder
path so that a demonstrator knows where the slips are landing. Setting it back to "Keine Störung" is
what clears a fault that was set to hold, which for "Kein Papier" is the paper change.

**Setting up a server phone.** The enrolment screen, kept open during a briefing.

| Key | Deutsch | English |
|---|---|---|
| `admin.enrol.title` | Telefon einrichten | Set up a server phone |
| `admin.enrol.step1` | Lassen Sie diesen Bildschirm offen. | Leave this screen open. |
| `admin.enrol.step2` | Die Bedienung scannt den QR-Code mit der Kamera ihres Telefons. | The server scans the QR code with the camera on their phone. |
| `admin.enrol.step3` | Die Bedienung wählt im Browser ihren Namen aus. | The server chooses their name in the browser. |
| `admin.enrol.rotation` | Der Code wird alle 30 Sekunden erneuert. Jeder Code gilt fünf Minuten und für ein Telefon. | The code is renewed every 30 seconds. Each code is valid for five minutes and for one phone. |
| `admin.enrol.cameraTitle` | Wenn die Kamera nicht funktioniert | If the camera does not work |
| `admin.enrol.cameraStep` | Öffnen Sie im Browser des Telefons {url} und geben Sie dort den Code {code} ein. | Open {url} in the browser on the phone and enter the code {code} there. |
| `admin.enrol.enrolled` | Eingerichtet: {names} | Set up so far: {names} |
| `admin.enrol.stop` | Einrichtung beenden | Stop setting up phones |
| `admin.enrol.invalidate` | Alle offenen Codes sofort ungültig machen | Make every open code invalid now |
| `admin.enrol.locked` | {count} Telefone sind gesperrt, weil sie zu oft einen falschen Code gesendet haben. | {count} phones are locked because they sent a wrong code too often. |
| `admin.enrol.unlock` | Sperren aufheben | Lift the locks |

**Devices.**

| Key | Deutsch | English |
|---|---|---|
| `admin.devices.title` | Eingerichtete Telefone | Phones that are set up |
| `admin.devices.lastSeen` | Zuletzt gesehen: {time} | Last seen at {time} |
| `admin.devices.reassign` | Telefon einer anderen Bedienung geben | Give this phone to a different server |
| `admin.devices.reassignHelp` | Ab jetzt steht der neue Name auf den Bons. Die bisherigen Bestellungen bleiben bei der Person, die sie aufgenommen hat. | From now on the new name is on the slips. Earlier orders stay with the person who took them. |
| `admin.devices.revoke` | Einrichtung entfernen | Remove this phone |
| `admin.devices.revokeConfirm` | Das Telefon von {name} kann danach keine Bestellungen mehr senden. {name} kann es mit einem neuen QR-Code wieder einrichten. | The phone belonging to {name} can no longer send orders afterwards. {name} can set it up again with a new QR code. |
| `admin.devices.revoked` | Einrichtung entfernt | Removed |

**Orders and the event.**

| Key | Deutsch | English |
|---|---|---|
| `admin.orders.title` | Bestellungen | Orders |
| `admin.orders.filterAttention` | Nur Bestellungen, die geprüft werden müssen | Only orders that need checking |
| `admin.orders.unknownQuestion` | Schauen Sie am Stapel bei {station} nach Bon {sequence} und antworten Sie hier. | Check the pile at {station} for slip {sequence} and answer here. |
| `admin.orders.slipIsThere` | Der Bon liegt da | The slip is there |
| `admin.orders.slipIsMissing` | Der Bon fehlt | The slip is missing |
| `admin.orders.reprint` | Erneut drucken | Print again |
| `admin.orders.noCancel` | Sagen Sie der Station Bescheid. Eine gesendete Bestellung lässt sich nicht zurücknehmen, weil ihr Bon schon gedruckt wird. | Tell the station. A sent order cannot be taken back, because its slip is already printing. |
| `admin.event.title` | Veranstaltung | Event |
| `admin.event.current` | Laufende Veranstaltung: {name}, seit {time} | Current event: {name}, since {time} |
| `admin.event.startNew` | Neue Veranstaltung starten | Start a new event |
| `admin.event.startEffect` | Die Bonnummern beginnen wieder bei 1. Alle bisherigen Bestellungen bleiben gespeichert. | Slip numbers start again at 1. Every order so far stays saved. |
| `admin.event.blockedOpenTickets` | Klären Sie zuerst {count} offene Bons in der Bestellliste. Beim Start einer neuen Veranstaltung verschwinden sie von allen Telefonen. | Settle {count} open slips in the order list first. Starting a new event makes them disappear from every phone. |
| `admin.event.blockedQuestions` | Beantworten Sie zuerst {count} offene Fragen zu Bons in der Bestellliste. | Answer {count} open questions about slips in the order list first. |
| `admin.event.blockedMock` | Tragen Sie bei {names} einen Drucker ein oder starten Sie stattdessen eine Übung. Auf dem Testdrucker kommt kein Bon auf den Stapel. | Set up a printer at {names}, or start a practice run instead. On the test printer no slip reaches the pile. |
| `admin.event.confirmName` | Tippen Sie den Namen der neuen Veranstaltung ein, um sie zu starten. In der letzten Stunde wurden noch Bestellungen aufgenommen. | Type the name of the new event to start it. Orders were still being taken in the last hour. |
| `admin.event.practice` | Übung starten | Start a practice run |
| `admin.event.practiceHelp` | In einer Übung ist der Testdrucker in Ordnung, und die Bestellungen stehen später nicht in der Abrechnung. | In a practice run the test printer is fine, and the orders are left out of the takings list later. |
| `admin.event.practiceRunning` | Es läuft eine Übung. Starten Sie die richtige Veranstaltung, bevor die Gäste kommen. | A practice run is going on. Start the real event before the guests arrive. |

**Backup and diagnosis.**

| Key | Deutsch | English |
|---|---|---|
| `admin.backup.title` | Datensicherung | Backup |
| `admin.backup.create` | Sicherungsdatei anlegen | Create a backup file |
| `admin.backup.done` | Kopieren Sie die Datei {name} aus dem Programmordner auf einen USB-Stick. Sie enthält den ganzen Verlauf. | Copy the file {name} from the program folder onto a USB stick. It holds the whole history. |
| `admin.backup.help` | Legen Sie die Sicherungsdatei über diese Schaltfläche an. Die Datenbankdatei einfach zu kopieren, während das Programm läuft, kann die letzten Bestellungen auslassen. | Create the backup file with this button. Copying the database file while the program is running can leave out the most recent orders. |
| `admin.diagnostics.title` | Technische Angaben | Technical details |
| `admin.diagnostics.log` | Protokolldatei öffnen | Open the log file |
| `admin.diagnostics.logHelp` | Hier steht, was das Programm heute Abend getan hat. Diese Datei hilft, wenn eine Station nichts bekommen hat. | This holds what the program did this evening. The file helps when a station received nothing. |

### 8.10 Break-glass station page

**Purpose.** One evening in ten, a printer dies and no spare exists. This page is how the station keeps
producing anyway: the kitchen opens it on somebody's phone, works the orders off the screen, and writes
the table number on a scrap of paper that goes out with the food. It is not a kitchen display system,
and it must never become part of the normal workflow.

**What is on it.** A warning block at the top saying when to use it, a filter naming the production
location, then that location's open tickets, oldest first. Each row is dominated by the slip number, in
the same three digit form as the printed slip, then the order number, the table, the time the order was
taken, and every line with its quantity and note. That is the whole of what somebody needs to make the
food and label it, and it is laid out in the same order as the printed slip so nobody has to learn a
second layout at the worst moment of the evening.

**Every open order is listed, not only the broken ones.** The old design showed only failed, unknown
and blocked tickets, which is useless in the case the page exists for: when the printer is dead, every
order is one the station has to make, and a list of three out of forty is a list of three orders that
will be produced and thirty seven that will not. Section 5.6 defines open as anything not yet
`Printed`, `PrintedOnTestPrinter` or `HandledOnPaper`.

**The filter is there because a printer can move.** When the admin has pointed a broken station at a
working one (section 2.12), the person at the kitchen printer is tearing off the bar's slips too and
needs the bar's list while holding a card printed for the kitchen. The filter opens on the station whose
card was scanned and lists the others by name.

**Showing is always safe. Acting is what is dangerous.** Reading a row cannot lose an order. Tapping
"Übernommen" on a live order can: it marks the ticket handled on paper, prints nothing, and tells the
server who placed it that everything is fine. So the restriction is on the button and not on the list.
**A row offers the button only when its station genuinely cannot print right now**, which section 5.6
defines exactly and the server decides; every other row renders with `station.takeUnavailable` in place
of the button, and the endpoint refuses the same tickets with `station.takeRefused` if anybody reaches
it another way. During a working evening the page is a list with no buttons on it, which is both safe
and honest: it says what is coming, and it offers nothing to press.

**What the user can do.** Read, filter by station, and mark a row as taken when that row offers it.
Nothing else. There is no login, no configuration, and no way to change an order from here. Marking a
row as taken can be undone for ten seconds.

| Key | Deutsch | English |
|---|---|---|
| `station.title` | {name}: Bestellungen am Bildschirm | {name}: orders on screen |
| `station.warningTitle` | Diese Seite ist nur für den Notfall. | This page is only for emergencies. |
| `station.warningBody` | Öffnen Sie sie nur, wenn der Drucker ausgefallen ist. Im Normalbetrieb arbeitet die Station den Bonstapel ab. | Open it only when the printer has failed. In normal operation the station works off the pile of slips. |
| `station.empty` | Es liegen keine offenen Bestellungen an. Alle Bons sind gedruckt. | There are no open orders. Every slip has printed. |
| `station.filterLabel` | Station | Station |
| `station.filterHelp` | Wählen Sie die Station, deren Bons Sie hier sehen wollen. | Choose the station whose slips you want to see here. |
| `station.row` | Bon {sequence}, Bestellung {order}, {table} | Slip {sequence}, order {order}, {table} |
| `station.rowTime` | Aufgenommen um {time} | Taken at {time} |
| `station.line` | {quantity} x {item} | {quantity} x {item} |
| `station.lineNote` | Hinweis: {note} | Note: {note} |
| `station.orderNote` | Hinweis zur Bestellung: {note} | Note for the order: {note} |
| `station.status.waiting` | Wartet auf den Druck | Waiting to print |
| `station.status.cannotPrint` | Der Drucker kann gerade nicht drucken | The printer cannot print right now |
| `station.status.failed` | Nicht gedruckt | Not printed |
| `station.status.unknown` | Unklar, ob gedruckt | Not known whether it printed |
| `station.take` | Übernommen | Taken |
| `station.takeHelp` | Schreiben Sie die Tischnummer auf einen Zettel und legen Sie ihn zum Essen. | Write the table number on a piece of paper and put it with the food. |
| `station.takeUnavailable` | Holen Sie diesen Bon am Drucker. Der Drucker dieser Station arbeitet. | Fetch this slip at the printer. This station's printer is working. |
| `station.takeRefused` | Holen Sie den Bon am Drucker. Der Drucker dieser Station druckt wieder, deshalb wurde dieser Bon nicht übernommen. | Fetch the slip at the printer. This station's printer is printing again, so this slip was not taken. |
| `station.undo` | Rückgängig | Undo |
| `station.takenNote` | Für diese Bestellung wird kein Bon mehr gedruckt. | No slip will be printed for this order any more. |
| `station.alreadyPrinted` | Dieser Bon wurde inzwischen gedruckt und liegt auf dem Stapel. | This slip has printed in the meantime and is on the pile. |
| `station.printerBack` | Der Drucker antwortet wieder. Die Bons kommen wieder aus dem Drucker. | The printer is answering again. The slips are coming out of the printer again. |
| `station.unknownKey` | Fragen Sie die Person am Laptop nach dem aktuellen Link. Diese Adresse gilt nicht mehr. | Ask the person at the laptop for the current link. This address is no longer valid. |

`station.takeHelp` sits under the take button rather than in the warning block at the top, because
writing the table on a scrap of paper is the step that gets forgotten and the button is where the
person is looking when they are about to forget it.

The page connects to SignalR and updates itself, so a station working off it does not refresh. It gains
a row on `OrderAccepted`, updates one on `TicketStatusChanged`, and re-evaluates which rows offer the
take button on `PrinterStatusChanged`. When the printer comes back, `station.printerBack` says so and
the take buttons disappear, which is the same rule as everywhere else on this page: the button exists
only while the printer cannot print.

**How the station gets the link at 21:00 is a printed card, not a sentence.** Each station's card,
printed from the admin and also printed on its test slip during setup, carries the station name and a
QR code to that station's break-glass URL. It is taped inside the printer lid. Nobody reads 32 hex
characters aloud across a loud marquee.

---

## 9. Offline and failure behaviour on the phone

### 9.1 What is possible and what is not

Plain HTTP means no secure context, which means no service worker and no installed app. A phone is
online-only, and nothing in this product runs while its page is closed.

That single fact decides the design of this whole section. **There is no background queue, no retry
timer, no give-up window for a submission, and no data structure holding orders that are waiting to be
sent.** A queue that only runs while somebody is looking at the screen is not a queue, it is a timer
with a promise attached, and a volunteer who reads "5 Bestellungen warten auf die Verbindung" and puts
the phone in their apron has been told something the product cannot keep.

What exists instead is smaller and true: the order stays on the screen, and the server taps a button.

All of the rules below live in `src/core/`, as plain TypeScript with no Vue and no DOM, with unit tests.
Components read the store; they do not implement sending logic.

### 9.2 The draft cart, and why it is not a queue

The order being built lives in `localStorage` under the key `draftOrder`, and it is written on every
change: a line added, a quantity changed, a station chosen, a note typed, the table entered.

```json
{
  "tableLabel": "Tisch 12",
  "note": null,
  "lines": [
    { "catalogItemId": "...", "quantity": 2, "note": null, "productionLocationId": null }
  ],
  "clientOrderId": null
}
```

Its only job is that a reload does not lose a half-built order. `localStorage` survives a tab being
closed, a browser being killed, and a phone rebooting, which is why the device token lives there too.
On page load the app reads `draftOrder` and puts the order back on the screen exactly as it was.

**This is a draft cart and not a queue, and the distinction is load-bearing.** A draft cart holds one
order, the one on the screen, and nothing ever sends it except a person tapping the send button. It has
no retry loop, no ordering, no head, no ages, and no state beyond "the server has not sent this yet".
Any implementation that gives it a list, a timer, or a `state` field has rebuilt the queue this section
deliberately removed, and it should be rejected in review no matter what it is called.

The draft is cleared when the order is accepted, and only then.

### 9.3 Sending an order, and the submission id

When the server taps send:

1. The app generates a `clientOrderId`, a UUID, **once**, and writes it into the draft before the
   request starts.
2. It posts the order.
3. On 201 or 200 it shows the confirmation with the order number, clears the draft, and returns to the
   catalog.
4. On any failure it leaves everything on the screen, shows `review.sendFailed`, and waits.

Tapping "Noch einmal senden" repeats step 2 with **the same `clientOrderId`**. It is regenerated by
nothing: not a retry, not a reload, not a re-enrolment, not the start of a new event session. A new id
is created only when the next order is started.

**This id is the whole reason a manual retry is safe, and the case it covers is the one a volunteer
cannot see.** If the first submission reached the backend and its response was lost coming back, then
an order exists, with numbers, with slips already printing, while the phone shows a failure. The server
taps retry, because that is what the screen told them to do. Without the id the backend would create a
second order, both stations would print, and the table would get everything twice. Two identical orders
in one evening is the second worst outcome in this system, and it would be produced by the honest
behaviour of a server following an instruction.

With the id, the second submission finds the existing row through the unique index on
`Order.ClientOrderId`, inside the same transaction that would otherwise have inserted, and returns the
original order with its original numbers and its original tickets. Nothing new is created and nothing
new is printed. Section 5.4 gives the response.

**The phone shows that answer exactly as it shows a first-time success**, with the same wording and the
same order number, because to the server it is the same event: the order arrived. A screen that
distinguished the two would be reporting on the network instead of on the order, and it would invite
somebody to worry about a case that has already been handled.

The id is stored on the order row and kept as long as the order is, which is forever. Orders are never
deleted, so there is nothing to expire and no cleanup to forget.

### 9.4 The specific failure cases

**WiFi drops while the server is still picking items.** Nothing happens. The basket is local. The header
shows the reconnect line so the server is not surprised at the moment they send.

**WiFi drops after tapping send.** The request fails, the order stays on the screen in full, and the
retry button is under the message. The server can walk ten metres towards the marquee and tap it again.

**The request left and the answer never came.** The dangerous one, and section 9.3 is entirely about it.
The retry carries the same `clientOrderId`, so exactly one order exists either way.

**Sending fails several times.** After the second failure the screen adds the paper instruction: write
the order down and carry it to the station. Telling a server to fall back to paper is the honest answer
when the laptop is not reachable, and paper is what they were doing last year. The order stays on the
screen and the retry button stays there too, so if the WiFi comes back while they are still writing,
one tap still sends it.

**The backend is unreachable when the server taps send.** Same path. Nothing about picking items
requires the backend to be up, so the server can keep taking orders on paper and send them when the
signal is back.

**Reconnect.** SignalR reconnects, then the store calls `GET /api/orders/mine` and replaces its list.
The phone never has to work out what it missed.

**The device was revoked while it was offline.** The next call answers 401. The app clears the token,
**keeps the draft order**, and shows the enrolment screen with a line saying the started order is still
there. Throwing away a half-built order because an admin tapped the wrong row would be destroying a
guest's order to solve an administrative problem.

**The device is revoked and set up again.** The same person's orders are still on the order list,
because the list is scoped by `ServerPersonId` and not by the phone. Every unanswered slip question the
person has is still answerable on the same handset.

**An item sold out, or a price changed, while the basket was open.** See section 8.6. The line stays and
is flagged, the phone shows the backend's new price, and the order is still accepted when sent.

**The event session was restarted while the phone was open.** The `EventSessionStarted` event clears the
phone's order list, because those orders belong to the previous session, and a one line notice explains
it. A half-built draft is untouched and is submitted into the new session when the server sends it.

**Two phones send the same order.** Not prevented, and not preventable: two servers can genuinely take
the same table. The station sees two slips with two different numbers, which is the same situation as
two paper slips, and is resolved the same way. The `clientOrderId` protects against one order being
sent twice, not against two people taking the same order, which is a problem software cannot see.

---

## 10. Configuration and setup

### 10.1 What the admin configures, in order

The order matters, because each step needs the one before it. The overview screen enforces it by naming
the next missing thing rather than letting the admin wander.

1. **Stations.** One per kitchen or bar. At a normal site this is two rows.
2. **Printers.** One per station. During preparation at home, leave every station on the test printer
   and the whole system can be tried out and demonstrated without any hardware. Each slip is written as
   a text file into that station's folder under `mock-slips`, next to the database, and the printer
   screen names the path.
3. **Items and prices.**
4. **Assignment.** Tick which stations can produce each item. An item must have at least one, and the
   screen shows a live preview of where each item lands or which stations the server will choose
   between.
5. **Tables.** Optional. Only suggestions.
6. **Server names.** Optional. A server can also type their own name during setup.
7. **A practice run.** Optional but recommended, and the right way to place test orders: they are kept
   out of the treasurer's export and the test printer is expected rather than reported as a fault.
8. **Start the event.** This resets slip numbering to 1 and refuses while a station is still on the test
   printer.
9. **Set up the phones.** Last, because the phones fetch the catalog when they are set up.

### 10.2 Setup checklist, English

Print this page and take it with you.

**At home, the day before**

1. Copy the program file onto the laptop and double click it. A black window opens. **Leave it open.**
   Closing that window stops the program and the evening with it.
2. Open the browser on the laptop and go to `http://localhost:5000/admin`.
3. Create the stations, for example Kitchen and Bar.
4. Enter the items with their prices.
5. Tick, for each item, which stations can prepare it. Food usually gets only the kitchen. Beer at a
   site with two bars gets both, and the server then picks one while taking the order.
6. Start a practice run and place a few practice orders from your own phone, with every station still
   on the test printer. Practice orders stay out of the list the treasurer gets.

**On site, before the guests arrive**

7. Switch on the WiFi router and connect the laptop to the same network the phones will use.
8. **Switch off client isolation in the WiFi router.** It is sometimes called AP isolation or guest
   mode. With it switched on the phones cannot reach the laptop, and nothing else in this list will
   help.
9. **Give the laptop a fixed address.** Either reserve one for it in the router, which is usually called
   a DHCP reservation, or set a static address on the laptop's WiFi adapter. If the address changes
   during the evening, every phone loses the laptop at once and the whole crew has to scan the QR code
   again.
10. Plug the laptop into power. Set it so that it does not go to sleep and the screen stays on, and do
    not close the lid.
11. When Windows asks whether the program may communicate on the network, allow it for the private
    network.
12. Set up each printer: paper roll in, power on, network cable or WiFi bridge connected.
13. Open the printer screen and search the network for printers, then tap the one that belongs to each
    station. If the search finds nothing, print the printer's self test to read its address and type it
    in: hold the feed button down while switching the printer on, then let go.
14. Print a test slip at every station. Fetch the slip, check that the station name on it is the right
    one, and tape the station card that prints with it inside that printer's lid. The QR code on the
    card is what the kitchen needs on the evening the printer dies.
15. Open the overview and read the address shown there, for example `http://192.168.1.23:5000`. Open
    that address once from one phone to prove the phones can reach the laptop.
16. Start the event. Slip numbers now begin at 1. The program refuses to start while a station is still
    on the test printer, which is what catches a station nobody set up.
17. Open "Set up a server phone" and let the servers scan the QR code one after another. Watch their
    names appear in the list.

**During the festival**

18. Leave the laptop running with the program open and the black window open.
19. If you are running the evening, carry a phone that is set up. A station that stops answering or runs
    out of paper appears as a banner on every phone, so you find out where you are standing rather than
    by walking back to the laptop.
20. When a printer runs out of paper, put a new roll in. The waiting slips print by themselves, and
    nothing is reported as failed while the paper is out.
21. **When a printer dies and you have no spare, point that station at a printer that still works.**
    Open the printer screen, read the address off a station whose printer is fine, and enter that same
    address for the broken station. Both stations' slips then come out of the one printer, each with
    its station name in large letters at the top, and somebody carries the other station's slips
    across.
22. **If there is no working printer at all, use the station page.** Scan the QR code on the card taped
    inside the printer lid. The station's orders are on the screen, and whoever makes the food writes
    the table number on a scrap of paper and sends it out with the tray.

**Afterwards**

23. Open "Only orders that need checking" and make sure it is empty. This is the one check that catches
    an order nobody produced, and it takes five seconds.
24. Open the backup screen, create the backup file, and copy it onto a USB stick. That file is the whole
    history.

### 10.3 Setup checklist, German

Drucken Sie diese Seite aus und nehmen Sie sie mit.

**Zu Hause, am Tag vorher**

1. Kopieren Sie die Programmdatei auf den Laptop und starten Sie sie mit einem Doppelklick. Es öffnet
   sich ein schwarzes Fenster. **Lassen Sie es offen.** Wenn Sie es schließen, ist das Programm aus und
   der Abend mit ihm.
2. Öffnen Sie am Laptop den Browser und rufen Sie `http://localhost:5000/admin` auf.
3. Legen Sie die Stationen an, zum Beispiel Küche und Theke.
4. Tragen Sie die Artikel mit ihren Preisen ein.
5. Kreuzen Sie bei jedem Artikel an, welche Stationen ihn zubereiten können. Essen bekommt meist nur
   die Küche. Bier bekommt an einem Platz mit zwei Theken beide, und die Bedienung wählt dann beim
   Aufnehmen aus.
6. Starten Sie eine Übung und geben Sie ein paar Übungsbestellungen vom eigenen Telefon auf, während
   alle Stationen noch auf dem Testdrucker stehen. Übungsbestellungen stehen später nicht in der
   Abrechnung.

**Am Festplatz, bevor die Gäste kommen**

7. Schalten Sie den WLAN-Router ein und verbinden Sie den Laptop mit demselben Netz, das die Telefone
   nutzen.
8. **Schalten Sie im WLAN-Router die Client-Isolierung aus.** Sie heißt manchmal AP-Isolation oder
   Gastmodus. Solange sie eingeschaltet ist, erreichen die Telefone den Laptop nicht, und nichts
   anderes aus dieser Liste hilft dagegen.
9. **Geben Sie dem Laptop eine feste Adresse.** Reservieren Sie ihm eine im Router, das heißt dort
   meist DHCP-Reservierung, oder stellen Sie am WLAN-Adapter des Laptops eine feste Adresse ein. Wenn
   sich die Adresse während des Abends ändert, verlieren alle Telefone auf einen Schlag die Verbindung
   und die ganze Mannschaft muss den QR-Code neu scannen.
10. Schließen Sie den Laptop ans Stromnetz an. Stellen Sie ein, dass er nicht in den Ruhezustand geht
    und der Bildschirm anbleibt, und klappen Sie ihn nicht zu.
11. Wenn Windows fragt, ob das Programm im Netzwerk kommunizieren darf, erlauben Sie es für das private
    Netzwerk.
12. Richten Sie jeden Drucker ein: Papierrolle einlegen, einschalten, Netzwerkkabel oder WLAN-Brücke
    anschließen.
13. Öffnen Sie die Seite Drucker, suchen Sie die Drucker im Netz und tippen Sie den an, der zu der
    jeweiligen Station gehört. Wenn die Suche nichts findet, drucken Sie am Drucker den Selbsttest, um
    die Adresse abzulesen, und tragen Sie sie ein. Halten Sie dazu die Papiertaste gedrückt, während Sie
    den Drucker einschalten, und lassen Sie sie dann los.
14. Drucken Sie an jeder Station einen Testbon. Holen Sie den Bon, prüfen Sie, ob der Stationsname
    darauf stimmt, und kleben Sie die Stationskarte, die mit ausgedruckt wird, in den Deckel dieses
    Druckers. Der QR-Code auf der Karte ist das, was die Küche an dem Abend braucht, an dem der Drucker
    ausfällt.
15. Öffnen Sie die Übersicht und lesen Sie die dort angezeigte Adresse ab, zum Beispiel
    `http://192.168.1.23:5000`. Rufen Sie diese Adresse einmal von einem Telefon aus auf, um zu prüfen,
    dass die Telefone den Laptop erreichen.
16. Starten Sie die Veranstaltung. Die Bonnummern beginnen jetzt bei 1. Das Programm startet die
    Veranstaltung nicht, solange eine Station noch auf dem Testdrucker steht, und genau das fällt sonst
    niemandem auf.
17. Öffnen Sie "Telefon einrichten" und lassen Sie die Bedienungen nacheinander den QR-Code scannen. Die
    Namen erscheinen dabei in der Liste.

**Während des Festes**

18. Lassen Sie den Laptop mit dem geöffneten Programm und dem schwarzen Fenster laufen.
19. Wenn Sie den Abend leiten, tragen Sie selbst ein eingerichtetes Telefon bei sich. Eine Station, die
    nicht mehr antwortet oder kein Papier mehr hat, erscheint auf jedem Telefon als Hinweis. So erfahren
    Sie es dort, wo Sie gerade stehen, und nicht erst am Laptop.
20. Wenn ein Drucker kein Papier mehr hat, legen Sie eine neue Rolle ein. Die wartenden Bons werden
    danach von selbst gedruckt, und solange das Papier fehlt, wird keine Bestellung als gescheitert
    gemeldet.
21. **Wenn ein Drucker ausfällt und Sie keinen Ersatz haben, tragen Sie bei dieser Station einen
    Drucker ein, der noch arbeitet.** Öffnen Sie die Seite Drucker, lesen Sie die Adresse einer Station
    ab, deren Drucker in Ordnung ist, und tragen Sie dieselbe Adresse bei der ausgefallenen Station
    ein. Die Bons beider Stationen kommen dann aus diesem einen Drucker, jeder mit seinem Stationsnamen
    in großen Buchstaben oben, und jemand trägt die Bons der anderen Station hinüber.
22. **Wenn gar kein Drucker mehr arbeitet, nehmen Sie die Stationsseite.** Scannen Sie den QR-Code auf
    der Karte im Deckel des Druckers. Die Bestellungen der Station stehen dann am Bildschirm, und wer
    das Essen macht, schreibt die Tischnummer auf einen Zettel und gibt ihn mit dem Tablett hinaus.

**Danach**

23. Öffnen Sie "Nur Bestellungen, die geprüft werden müssen" und prüfen Sie, dass die Liste leer ist.
    Das ist die eine Kontrolle, die eine Bestellung findet, die niemand zubereitet hat, und sie dauert
    fünf Sekunden.
24. Öffnen Sie die Datensicherung, legen Sie die Sicherungsdatei an und kopieren Sie sie auf einen
    USB-Stick. Diese Datei ist der gesamte Verlauf.

### 10.4 Configuration that is not in the UI

A small `appsettings.json` next to the executable holds what a volunteer never touches: the port, the
bind address, the scheme, the database path, and the log level. The scheme, host, and port live in one
options object so a later move to HTTPS is a setting rather than a rewrite.

The log is a rolling file next to the database, one file per day, kept for the last fourteen days. It
is reachable from the diagnostics screen. When the marquee bar reports that it received nothing all
evening, the log is the only artifact that can answer why, and a log that only exists on a console
window somebody closed answers nothing.

### 10.5 The backup, and why it is a button

SQLite runs in WAL mode, which is the right journal mode for one writer and several readers. It also
means that the most recent transactions live in `gastronomy.db-wal` rather than in `gastronomy.db`, so
copying the one file a volunteer can see loses the end of the evening, silently, which is the worst
possible way to lose data.

The backup screen therefore has a button. It runs `VACUUM INTO` a dated file next to the database, which
produces one consistent file with everything in it, and then names that file on screen so the volunteer
knows exactly which one to drag onto the USB stick. The program also writes one automatically when an
event session ends, so a volunteer who forgets step 22 still has one.

---

## 11. Testing strategy

Test first, every time, including bug fixes. A failing run that can be quoted is the gate before any
production code is written. Two layers per change, unit and integration, with end to end coverage
required for the order placement flow and the printing pipeline.

### 11.1 Unit tests

**Backend core**, with fakes for every port, no database and no sockets:

| Class under test | What is proven |
|---|---|
| `OrderTotalCalculator` | Totals, including zero priced items, large quantities, and that the stored total equals the recomputed one |
| `OrderRoutingResolver` | One candidate routes with no input, more than one requires the line to name a station, a named station that is not assigned to the item is rejected, a named station that is no longer active falls to the lowest `SortOrder` active candidate and records what was chosen, and the candidate set is never empty for an orderable item |
| `OrderStatusCalculator` | The priority table in section 3.1, exhaustively over every combination of ticket states, including that no combination falls through |
| `TicketStateMachine` | Every transition in section 3.2, and that every transition not listed is refused |
| `PrintJobStateMachine` | Every transition in section 3.3, and specifically that a result with bytes written can never reach a retryable state |
| `RetryPolicy` | The full table in section 7.6, one case per row, including both `SocketDropped` rows and both `Timeout` rows |
| `StationCircuitBreaker` | Two consecutive unknown outcomes trip it, a confirmed job resets the counter, tripping moves every waiting ticket at once, and **no queue depth trips it**: fifty tickets waiting at a station that is out of paper leave it untripped |
| `GiveUpWindow` | The window is measured from ticket creation, so a ticket that waited behind others expires on time. It is suspended for each of the four known causes in section 3.2 and resumes without resetting when the cause clears. The 20 minute outer bound expires under every one of those causes, including a station that stayed blocked for the whole time. |
| `TicketAcknowledgePolicy` | The condition in section 5.6, one case per clause: each ticket status that always allows it, each printer condition that allows it, and a `Queued` or `Printing` ticket at a healthy station refused |
| `EscPosSlipRenderer` | Byte for byte output for a normal slip, a reprint with its reprint time, a wrapped long item name, a chosen station that differs from the printing one, umlauts under PC858, and the double size regions |
| `EscPosSlipRenderer`, station card | A test slip emits the five `GS ( k` functions in the order given in 7.7, with model 2, a module size of 6, error correction level M, `pL` and `pH` equal to the URL length plus three for both a short and a 69 character URL, and the URL itself byte for byte, followed by the same URL wrapped underneath as text |
| `ProcessIdAllocator` | Cycling at 9999, uniqueness within a printer, resumption from the persisted value after a restart, and that two locations sharing one endpoint draw from one counter and never receive the same value |
| `EnrolmentCodeVerifier` | Correct code, wrong code, expired code, already consumed code, that a consumed code does not count as a failed attempt, and that a lockout applies to one source address and leaves other codes valid |
| `DeviceTokenHasher` | A token verifies against its own hash, a different token does not, and a stored iteration count is honoured |

**Frontend core** (Vitest, `src/core/`, no component mounting):

| Module | What is proven |
|---|---|
| `basket` | Adding, removing, quantity limits, note handling, choosing and changing a line's station, and total formatting in both locales |
| `routingPreview` | Same rules as the backend resolver, with a shared fixture set so the two cannot drift, including that a one candidate item never asks |
| `draftCart` | Written on every change, restored on load exactly as it was, cleared only on acceptance, kept across a revocation, and holding no list, no timer, and no retry state |
| `submission` | The id is generated once on the first send, reused by every retry, not regenerated by a reload or a re-enrolment, and a new order gets a new id |
| `orderStateMachine` | Exhaustive switch coverage with `assertNever`, so a new state is a compile error |
| `messageForTicket` | Each ticket state and failure reason maps to exactly one message key in both languages, and no mapping ever falls back to a transport string |
| `catalogItemState` | A sold-out item is rendered and not selectable, a deactivated item is absent from the payload and so cannot be rendered at all, and a sold-out line already in the basket keeps its quantity and gains the flag |

### 11.2 Integration tests

In-memory SQLite (`Data Source=:memory:`) and temp directories from `Path.GetTempPath()`, disposed in
teardown. No test touches a developer's real database or filesystem.

| Area | Scenarios |
|---|---|
| Numbering | Sequential allocation, no gaps under 200 concurrent submissions, correct resumption after a simulated restart, per location independence, reset on a new event session, a rolled back transaction consuming no number, and that every counter key round trips through the composite key with real GUID values |
| Idempotency | Same `clientOrderId` twice returns the same order with 200 and creates no second ticket and no second print job, a different body with the same id returns 409, a hundred parallel duplicates create exactly one order |
| Order acceptance | Split across locations, snapshot of names and prices, a line that names a station, a line whose named station was deactivated in between, a sold out item still accepted, an unreachable printer still producing 201, and a total that changed since the catalog fetch returned alongside the accepted order |
| Configuration invariants | An item cannot be saved without a station, a station cannot be deactivated while it is the last one for an item, and an item cannot be deactivated during a live event |
| Event session start | Refused while a ticket is non-final, refused while a question is unanswered, refused while a station is on the test printer, allowed for a practice run in the same state, and allowed after the typed confirmation when orders are recent |
| Authentication | Valid token, unknown token, revoked token, admin path from a foreign address returns 404, admin path from the laptop's own address succeeds, admin page served with an explanation to a phone, station access key valid and regenerated |
| Enrolment | Code consumed by the first redemption, second redemption returns 410 without counting as a failure, expiry, rotation, lockout confined to one address, unlock, and that the device row and the consumption commit together |
| SignalR | Each event reaches exactly the groups listed in section 6.2 and no others, including that ticket events reach every phone of the placing person |
| Printer worker | Every row of the failure table in section 3.5 against `MockPrinterTransport`, jobs attempted in sequence number order, and a blocked job holding the station rather than being overtaken |
| Mock transport | One file per slip in that location's folder holding exactly the rendered text, a reprint written beside its original rather than over it, two sessions in the same folder not colliding, two locations with the same name kept apart, all seven faults armable through the admin endpoint in both `Once` and `Sticky` modes, and a folder that cannot be written producing `PrinterError` with zero bytes rather than a reported print |
| Circuit breaker | Two unknown outcomes trip the station, every waiting ticket fails in one transaction, the reconnect action clears it, and a station with fifty tickets queued behind a paper-out is never tripped by depth |
| Break-glass listing and acknowledge | Every open ticket is listed including `Queued` and `Printing` ones, a `Printed` or `HandledOnPaper` ticket is not, `canAcknowledge` is false for every ticket at a healthy station and true once the printer is faulty, offline, out of paper, cover-open, in error, or disabled, acknowledging a healthy station's ticket answers 409 with `station.takeRefused`, the filter returns another location's tickets for the same access key, and an unknown key answers 404 |
| No duplicate after recovery | A ticket taken on the break-glass page while a job sits queued for it leaves that job `Failed` with `TicketResolvedByHuman` and zero bytes when the printer recovers, no slip file is written, and acknowledging a ticket the worker has already moved to `Printing` answers 409 |
| Shared printer | Two locations configured with one endpoint are served by one worker and one socket, their slips interleave on the roll while each location's sequence numbers stay unbroken, each slip carries its own station name, both `PrinterStatus` rows track the one machine, and both locations draw process ids from one counter |
| Two item states | The sold-out toggle is accepted during a live event and reverses cleanly, deactivation is refused during a live event, a sold-out item is present in `/api/catalog` with `isAvailable: false`, a deactivated item is absent, and an order carrying either is accepted at 201 |
| Prices | The accepted total is computed from the backend's current prices, an `expectedTotalCents` that is absent, zero, or wildly wrong changes neither the stored total nor acceptance, and no request field anywhere can influence a price |
| Crash recovery | A ticket left in `Printing` when the process died comes back as `Unknown`, `Queued` and `Blocked` tickets are re-enqueued in order, and a ticket left over from a previous session is enqueued too |
| Storage failures | A busy database waits rather than failing, a write failure returns 503 with a message key, and a read-only database directory refuses to start with a stated reason |
| Files | The CSV export opens with the declared delimiter and encoding, the import accepts `3,50`, `3.50` and `3`, rejects a row with no station, and imports nothing when any row fails |
| Backup | `VACUUM INTO` produces a file that opens on its own and contains the orders written a moment earlier |
| Localization | Every resource key exists in both `Strings.de.resx` and `Strings.en.resx`, every vue-i18n key exists in both locales, and every key's placeholder set is identical in both languages |

### 11.3 End to end tests

Playwright, against the real backend with every station on `MockPrinterTransport`, inside a practice
session so the test printer is the expected transport.

**Order placement flow, required**

1. A phone is set up by redeeming a code, builds an order, sends it, and sees it confirmed printed, with
   the slip file written into that station's folder.
2. An order that spans two stations produces two slips with one shared order number and two independent
   sequence numbers.
3. An item with one candidate station is added with a single tap and never asks where it goes.
4. An item with two candidate stations asks once, the chosen station appears on the line and in the
   review screen, changing it before sending moves the line, and the second line of the same item asks
   again rather than reusing the first answer.
5. The total shown on the review screen matches the total stored on the order.
6. **The WiFi drops between tapping send and the request leaving.** The order stays on screen in full,
   the retry button appears, the connection comes back, one tap sends it, and exactly one order exists.
7. **The answer to a submission is lost on the way back.** The server taps retry, the retry carries the
   same `clientOrderId`, and afterwards exactly one order exists, with one set of numbers, and exactly
   one slip file in each location's folder. The phone shows the same confirmation it would have shown
   the first time.
8. A price is changed at the laptop between the catalog fetch and the send. The order is accepted and
   the phone shows the new total.
9. A revoked device returns to the enrolment screen with its half-built order intact, is set up again,
   and sees its own earlier orders in the list.

**Printing pipeline, required, one scenario per printer failure mode**

| Scenario | Expected |
|---|---|
| Normal print | Ticket `Printed` on a real transport and `PrintedOnTestPrinter` on the mock, slip rendered, phone shows the matching state |
| Paper out before sending | Ticket `Blocked`, order `NeedsAttention`, phone shows the paper message, zero bytes written |
| Paper loaded afterwards | The parked slip prints by itself with no human action, and the ticket reaches its printed state |
| Cover open before sending | Ticket `Blocked`, cover message, zero bytes written |
| Connection timeout | Ticket stays `Queued`, then reaches `Failed` after the give-up window measured from its creation, phone shows the message telling the server to walk over |
| Socket dropped before the first byte | Automatic retry happens, no question is ever asked, and the slip prints |
| Socket dropped mid job | Ticket `Unknown`, no automatic retry happens, the question appears on every phone of the placing person and in the admin list |
| Unknown answered on the laptop | Ticket resolves from the admin endpoint while the placing phone is offline |
| Unknown outcome answered "the slip is there" | Ticket `Printed`, no reprint, one slip in total |
| Unknown outcome answered "the slip is missing" | Exactly one reprint, same numbers, reprint banner and reprint time present |
| Re-send after any failure | Never produces two slips with different numbers for one ticket |
| Two orders arriving at once at one printer | Serialized, both printed, sequence numbers consecutive and in order |
| A blocked ticket and a later one | The later slip does not overtake the blocked one |
| Station declared faulty | Every waiting ticket fails at once, every affected phone shows the message, and the admin printer screen shows the fault |
| Ten orders arrive during a paper change | No ticket fails, no station is declared faulty, every phone's banner names the station and the count, and all ten print when the roll goes in |
| A blocked station is never fixed | The tickets stay `Blocked` past five minutes with the banner and the admin row escalating, and reach `Failed` at the 20 minute outer bound with `ticket.failedAfterWaiting` |
| Backend restarted mid job | Ticket comes back as `Unknown` and the question appears |
| Station disabled with a slip queued | The slip waits without failing while the station is off, prints by itself when it is switched back on, and reaches `Failed` only if the outer bound arrives first |
| A printer is repointed at another station's printer | Both locations' slips come off one transport, each carrying its own station name and its own unbroken run of sequence numbers, and one worker holds the connection |
| Break-glass with a dead printer | Every open ticket at that station is listed immediately with its lines and table, every row offers the take button, taking one moves it to `HandledOnPaper` and tells the placing phone so |
| The printer recovers after a ticket was taken on paper | The queued job ends `Failed` with `TicketResolvedByHuman`, no slip is printed for it, and the table is not served twice |
| Station left on the test printer in a real event | The order reaches `NeedsAttention` and the phone says no slip is on the pile |
| Break-glass acknowledgement | A failed ticket reaches `HandledOnPaper`, disappears from the station page, no slip is printed afterwards, and the order leaves `NeedsAttention` |
| Break-glass during normal printing | A queued ticket at a healthy printer **is listed** with its lines and table but renders no take button, and acknowledging it by id is refused with `station.takeRefused` |

**Admin flow**

1. Configure a station, a printer, an item, and an assignment from an empty database, and place an order
   end to end.
2. Mark an item sold out and watch it grey out on an open phone without a reload, with the line already
   in a basket kept and flagged, then mark it available again and watch it come back.
3. Try to start an event with a station on the test printer, get the refusal, set up the printer, and
   start it.
4. Start a new event and watch numbering restart at 1 while old orders keep their numbers.

### 11.4 What is not tested

No mutation testing and no coverage gate. Load testing is not attempted, because the load is a handful
of orders per minute. Real hardware verification against a physical TM-T20IV is a manual checklist
carried out once before the first festival, and it is listed in the open questions below because two
commands on it need confirming.

---

## 12. Open questions

These are genuinely undecided and need the owner, or a printer on a desk, before they can be closed.

**One owner decision has been taken since this document was written, and it moves two of the questions
below rather than closing them.** No printer hardware will be bought until the fire department has run
the system on `MockPrinterTransport` and agreed that it is a tool they want. That is why the mock is a
shipped product feature rather than a test fixture, and why it is the vehicle the whole system is
evaluated on (section 7.8). It also means that questions 1 and 11, which both need a TM-T20IV on a desk,
cannot be answered until after that decision, rather than at leisure before the first festival. Nothing
else in this list has been settled.

1. **The process id echo on real hardware.** The confirmation design in section 7.4 depends on
   `GS ( H` returning the specified process id after printing completes on a TM-T20IV over port 9100.
   The command is documented for the TM series, but it needs confirming on the actual firmware before
   the design is trusted. If it turns out not to be available, the fallback is to treat a clean write
   plus a clean `DLE EOT n=4` read afterwards as a weaker confirmation, and to widen the `Unknown` state
   to cover more cases. That fallback prints correctly but asks the human question more often. The
   question stays open, and it now sits behind the hardware decision above: there is no printer to test
   against until the fire department has evaluated the system on the mock and asked for one.

2. **A second printer as a station's fallback. Closed by the owner, and the number is kept so the
   answer stays findable.** The question proposed a fallback printer field: let a station name another
   station whose printer takes its slips when its own dies. The owner's answer is that the field is not
   needed, because the capability is already there without it. A printer configuration is an address
   the admin can change during the evening, and two locations may point at one address, so the fix for
   a dead printer is to type the working printer's address into the broken station's configuration.
   Both stations' slips then come off one machine with the station name in large type at the top of
   each, and somebody carries the other station's slips over: exactly the outcome the fallback field
   was proposed to buy, with no field, no second address to keep correct, and no rerouting decision the
   software has to make while nobody wants it making decisions. Section 2.12 specifies what that
   configuration means, section 7.3 keeps it to one connection per machine, and checklist step 21 tells
   a volunteer how to do it. **No fallback printer field, no secondary printer, and no automatic
   rerouting will be added.**

3. **Admin access from something other than the laptop.** Version 1 restricts every admin endpoint to
   the laptop, which means the admin has to stand there. The task that actually needs mobility is not
   revoking a phone, it is knowing that a printer has stopped, and that already reaches every phone
   through `PrinterStatusChanged`, so the checklist now says the person running the evening should carry
   a phone that is set up. That covers most of the value with no new attack surface. Is an admin device
   still wanted for the rest, knowing it is a real widening of the attack surface on an open network?

   **One use case under this heading is already settled and is not part of the open question.** Marking
   an item sold out is an admin action, so it means walking to the laptop, and the owner has accepted
   that for version 1. No phone-reachable sold-out control will be added, and section 8.9 says so on
   the screen with `admin.items.soldOutWalk` rather than leaving somebody hunting for it.

4. **Address form in German.** This specification uses the Sie form everywhere. A fire department crew
   normally says du to each other. Which one does the owner want on the phones, on the slips, and in the
   printed checklist? Changing it later is a single pass over the resource files, but it should be
   decided once.

5. **Reprint on demand from the station.** Right now a station with a working printer that lost a slip
   has to ask the server to tap "Erneut drucken" on their phone, or open the break-glass page. Should
   the break-glass page also be allowed to reprint a slip? It would be convenient, and it is also the
   first step toward the station using a screen in normal operation, which the product deliberately
   refuses.

6. **Paper width.** Everything here assumes 80 mm rolls, so 48 columns in Font A. If the department
   already owns 58 mm rolls, the column counts and the double size regions change, and the layout in
   section 7.7 needs a second variant.

7. **Order note versus line note on the slip.** Both are printed. Is an order level note actually
   wanted, or is it a field that will be used to write things the kitchen has to read on every slip of
   the order?

8. **The five minute give-up window and the twenty minute outer bound.** Five minutes before a ticket
   whose cause is unknown is called failed, and twenty minutes before one is called failed whatever the
   cause, both measured from when the order was taken (section 3.2). Five minutes is a reasoned guess
   about how long a volunteer waits before walking. Twenty is a reasoned guess about the longest a
   paper change can honestly take, bounded by how long a guest waits before asking where the food is.
   Both should be checked against one real evening, and the second one is the number that decides
   whether the suspension rule is generous or negligent.

9. **The black console window.** Closing it ends the evening, and nothing in the product defends that
   beyond a bold line in the checklist and a line on the overview screen. A tray icon, or a window that
   asks for confirmation before it closes, is worth more than several of the features specified above.
   Is it worth building for version 1?

10. **A name the phones can use instead of an address.** A fixed address is now a checklist step, which
    is the cheap answer. The thorough answer is for the laptop to advertise a name on the local network
    so a changed address strands nobody. Doing that on Windows means a dependency for mDNS, and backend
    rule 5 is deliberately hostile to dependencies, so this is a trade the owner should make rather than
    the author.

11. **QR printing on real hardware.** Section 7.7 specifies the station card's QR code with the
    `GS ( k` family: model 2, a six dot module, error correction level M, and the break-glass URL as the
    stored data. The family is documented for the TM series and the geometry fits the 72 mm printable
    width with room to spare, but the TM-T20IV's own firmware has not been checked, exactly as question
    1's `GS ( H` has not been checked. Both are answered on the same afternoon with the same printer. If
    `GS ( k` does not print, the fallback is to build the QR matrix in the backend and send it as a
    raster bit image with `GS v 0`, which every ESC/POS printer supports. That costs a QR encoding
    dependency, which backend rule 5 is hostile to, so if the owner refuses the dependency the second
    fallback is the URL printed as wrapped text with no symbol at all. That one works, and nobody enjoys
    typing it.
