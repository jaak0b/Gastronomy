# CLAUDE.md (backend)

Rules for the C# backend. The repository root `CLAUDE.md` binds here too; this file adds to it and
never contradicts it.

## The stack

ASP.NET Core on .NET 10. `GastronomyApp.Api` is a **class library** that configures and returns the web
application; the Avalonia desktop app (`desktop/GastronomyApp.Desktop`) hosts it in-process and is the
only executable. One process serves the REST API, the SignalR hub, and the built frontend from
`wwwroot`, on one port. No runtime install on the operator's laptop, no separate web server, no
reverse proxy.

- **SQLite via EF Core.** One file. A backup is a file copy, which is what a volunteer can actually do.
- **SignalR** for every server-to-client push: an order accepted, the queue of a station changed, an
  order's status changed, items settled, a device enrolled or revoked, the catalog changed, a
  festival's start state changed. Clients never poll for state that the server already knows has
  changed.
- **REST** for commands and queries.

## Hard rules

0. **Read the `csharp-design-guidelines` skill before touching any C# file.** This comes before the
   first edit, before a rename, before a new test, and before a review, and it binds the main agent
   and every subagent without exception: a subagent prompt that concerns C# must say so. The skill
   lives at `backend/.claude/skills/csharp-design-guidelines/SKILL.md` and is the checklist of the
   official .NET design guidelines; its reference files hold the full rules. Code written without
   reading it is handed back, and a name, type or member that fails a checklist item is a review
   finding.

1. **No static methods or properties.** Framework metadata registration is the only exception.

2. **Localization is resx-only.** Every translatable string lives in `Strings.en.resx` /
   `Strings.de.resx` or a domain-specific resx pair, resolved through the localization service. Both
   language files must contain every key.

3. **No empty catch blocks.** Log the error, and surface it to the user for anything they initiated.
   A failure that a server or a station can act on must reach their screen, in their language, with a
   stated cause and a stated next step.

4. **No test touches the developer's database or filesystem.** In-memory SQLite
   (`Data Source=:memory:`) and `Path.GetTempPath()` temp directories, disposed in teardown.

5. **NuGet packages: official Microsoft or highly regarded community only.** No niche or unmaintained
   single-author packages. Prefer built-in BCL APIs over a third-party dependency: this binary is
   handed to volunteer fire departments and every dependency is a thing that can rot.

6. **Device tokens are treated as credentials.** Stored hashed with built-in PBKDF2-HMAC-SHA512, a
   per-token random salt, and the iteration count plus algorithm stored alongside the hash. Never
   stored in plaintext, never logged, never reversible. Enrolment codes are single-use and
   short-lived: the first scan consumes the code, so a photographed QR cannot enrol a second device.
   Revoking a device invalidates its token immediately.

7. **Every schema change is a new migration, and a migration that has run anywhere is never
   touched again.** The product has been installed since 2026-09-20 (release from commit 49fd605),
   so the migrations under `backend/GastronomyApp.Infrastructure/Migrations/` are history: editing,
   renaming, reordering, squashing, deleting or recreating an existing migration or its designer
   file is **forbidden**, `InitialCreate` included. A schema change is made by editing the model and
   adding a migration with `dotnet ef migrations add <Name> --project backend/GastronomyApp.Infrastructure`,
   forward only and append only; a wrong migration is corrected by a new one. Read the generated
   `Up` before handing back, because SQLite rebuilds a table for most alterations, and prove the
   change with the released-database upgrade test in `GastronomyApp.Infrastructure.Tests`: a database
   built by the released schema, rows in it, `MigrateAsync`, rows intact. The auto-managed model
   snapshot is the single file EF may regenerate.

8. **No positional tuple access.** Never read a tuple by element position (`.Item1`) and never
   destructure one positionally. Every multi-value return is a named `record` or `record struct` read
   by name, so reordering or renaming a member is a compile error rather than a silent value swap.

9. **LINQ is written in method syntax.** Every query is a fluent chain (`.Where`, `.Select`,
   `.Join`, `.OrderBy`), never query-comprehension syntax (`from ... in ...`). The whole codebase
   keeps one query style, and a join stays a chain instead of a block of SQL pasted into C#.

10. **Every touched C# file is run through the ReSharper cleanup before hand-back.** Line width,
    wrapping, alignment, initializer layout and braces are settings in the repository
    `.editorconfig`; nobody wraps or formats by hand, and a diff that only a formatter could have
    made is a review finding. The cleanup is `mcp__resharper__format_file` in `cleanup` mode
    (Rider's Code Cleanup on the same profile), run on every file an agent created or edited, in
    the same turn as the edit. This binds the main agent and every subagent, and it binds
    `desktop/` as well.

## Code style

- Two spaces per level, never tabs, and no trailing whitespace.
- A fluent chain stays on one line while it fits. When it wraps, it breaks after every dot, one call
  per line, indented to the start of the chain:

  ```csharp
  var stationOrders = await database.StationOrders
                                    .AsNoTracking()
                                    .Where(stationOrder => stationOrder.FestivalId == festivalId)
                                    .OrderBy(stationOrder => stationOrder.StationOrderNumber)
                                    .ToListAsync(cancellationToken);
  ```

- Never break an assignment after `=`. Keep the line whole and wrap the chain, or align a plain
  call's arguments under its opening parenthesis as `OrderAcceptanceService` does.
- Two statements on one line are never written, however short they are.

## The order model

One order, split per station, worked off item by item on that station's tablet, and all of it belongs
to one festival.

- **Festival**: a named period with a start and an end. It owns the menu, the prices, the stations and
  the orders of one event, and it carries the next global order number. Running means not hidden and
  `StartsAtUtc <= now < EndsAtUtc`; `FestivalSchedule` is the only place that decides that, and the
  hide flag is `IsHidden`, never `IsActive`.
- **FestivalStation**: a station taking part in one festival, carrying the number that station shows
  at that festival. Unique on `(FestivalId, StationId)`.
- **FestivalCatalogItem**: an item on one festival's menu, with the price it costs there and whether
  it has sold out tonight. Unique on `(FestivalId, CatalogItemId)`. `CatalogItem` itself carries
  neither, because both are facts about an item at one festival.
- **ItemStationAssignment**: which station prepares an item at one festival. Unique on
  `(FestivalId, CatalogItemId, StationId)`.
- **Order**: what the waiter sent. The festival it belongs to, global order number, table name, note,
  who took it, when. It has no status and no total: both are derived, never stored.
- **StationOrder**: the part of that order belonging to one station, carrying the number that station
  shows for it. Unique on `(OrderId, StationId)`, so a station can never receive two station orders of
  one order. It carries the `DeliveryMode` the waiter chose for that station: `Together` means the
  station hands the whole station order over at once, `AsItComes` means each item leaves as soon as it
  is ready. It also carries `IsHiddenFromAsItComesQueue`, the employee's manual decision to keep that
  order out of the as-it-comes column; hiding only removes it from that column and is only meaningful
  for an `AsItComes` station order.
- **OrderItem**: one entry of that station order. The item name comes from the catalog; the price is the one
  the phone displayed to the guest and the laptop stores it untouched, including when the item is given
  away. `FulfilledAtUtc` records the hand-out: null means the item is still open, a value means the
  station handed it out at that moment, and it is cleared again when a mistaken tap is put back. It
  also carries whether it has been settled:
  `SettledAtUtc` is the paid flag (null means still open, so a flag and a timestamp can never
  disagree), `ChargedPriceCents` is what was actually collected, and `PaymentNotice` is the reason
  typed when less than the displayed price was collected. The phone sends one price per line and the
  laptop stores exactly that price; it never distributes an amount across items. A line another owner
  already settled is never touched, while the owner who settled it may send it again and overwrites
  the price and the reason, keeping the time and the collector. What a table still owes and what was
  given away are derived from these on every read, never stored.
- **Device**: one phone or one tablet. It is owned 1:1 by exactly one `StaffMember` or one `Station`,
  and the owner points at it (`StaffMember.DeviceId`, `Station.DeviceId`), so an owner holds at most
  one device and at most one outstanding enrolment invitation. Setting a device up again deletes the
  old row, which is what revoking is.

The status of an order is derived from its items, so the two can never disagree: `Open` when none is
done, `PartiallyFulfilled` when some are, `Fulfilled` when all are. It is never stored. Enums persist
as numbers with pinned values, so a member may be renamed freely but never
reordered. Counters live where they belong: `Festival.NextOrderNumber` for the global order number
and `FestivalStation.NextStationOrderNumber` for each station at that festival, so every festival
starts at 1 by itself and nothing ever sets a counter back. Removing a station link deletes the row
that holds its counter, so adding the station back computes the next number from the highest
`StationOrderNumber` already used at that festival, and its numbering continues instead of
repeating.

## Intended project structure

Planned, not yet built. Update this table as it lands.

| Project | Role |
|---|---|
| `GastronomyApp.Core` | Domain models, ports, use cases. No framework dependencies. |
| `GastronomyApp.Infrastructure` | EF Core SQLite, device token store, enrolment invitations. |
| `GastronomyApp.Api` | Class library: REST endpoints, SignalR hub, static frontend, composition root. Hosted by `GastronomyApp.Desktop`. |
| `*.Tests` | Unit tests against Core, integration tests against Infrastructure and the API. |

## Testing

**NUnit** is the test framework. **FakeItEasy** provides fakes for port interfaces (`A.Fake<IXxx>()`).
Both are chosen because the owner already works with them; do not introduce a second framework or a
second faking library alongside them.

**Conventions:** fixture is `<ClassUnderTest>Test`, method is `MethodName_State_Expected`. One fixture
per production class, one file per fixture. Never a catch-all fixture name.

**Core tests** use fakes for port interfaces. **Infrastructure and API tests** use in-memory SQLite.

## Build and run

```powershell
dotnet build GastronomyApp.slnx
dotnet run --project desktop/GastronomyApp.Desktop
dotnet test GastronomyApp.slnx --filter "FullyQualifiedName~MethodName"
dotnet ef migrations add <Name> --project backend/GastronomyApp.Infrastructure
```
