# CLAUDE.md (backend)

Rules for the C# backend. The repository root `CLAUDE.md` binds here too; this file adds to it and
never contradicts it.

## The stack

ASP.NET Core on .NET 9. `GastronomyApp.Api` is a **class library** that configures and returns the web
application; the Avalonia desktop app (`desktop/GastronomyApp.Desktop`) hosts it in-process and is the
only executable. One process serves the REST API, the SignalR hub, and the built frontend from
`wwwroot`, on one port. No runtime install on the operator's laptop, no separate web server, no
reverse proxy.

- **SQLite via EF Core.** One file. A backup is a file copy, which is what a volunteer can actually do.
- **SignalR** for every server-to-client push: an order accepted, the queue of a station changed, an
  order's status changed, items settled, a device enrolled or revoked, the catalog changed. Clients
  never poll for state that the server already knows has changed.
- **REST** for commands and queries.

## Hard rules

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

7. **Shipped migrations are frozen from `20260906070525_InitialCreate` onward.** The owner approved
   recreating the initial migration for the change that put a tablet at every station; that recreated
   migration is the new baseline and history starts there. Once a migration has run on a real fire
   department's laptop, it is history. Editing, renaming, reordering, squashing, or deleting an
   existing migration or its designer file is forbidden: it desynchronizes the migrations history from
   the schema and bricks the app on startup. Migrations are append-only and forward-only. A wrong
   migration is corrected by a new corrective migration, never by touching the old one. The
   auto-managed model snapshot is the single file EF may regenerate.

8. **No positional tuple access.** Never read a tuple by element position (`.Item1`) and never
   destructure one positionally. Every multi-value return is a named `record` or `record struct` read
   by name, so reordering or renaming a member is a compile error rather than a silent value swap.

## The order model

One order, split per station, worked off item by item on that station's tablet.

- **Order**: what the waiter sent. Global order number, table name, note, who took it, when. It has no
  status and no total: both are derived, never stored.
- **StationOrder**: the slice of that order belonging to one station, carrying the number that station
  shows for it. Unique on `(OrderId, StationId)`, so a station can never receive two slices of one
  order. It carries the `DeliveryMode` the waiter chose for that station: `Together` means the station
  hands the whole slice over at once, `AsItComes` means each item leaves as soon as it is ready.
- **OrderItem**: one entry of that slice. The item name comes from the catalog; the price is the one
  the phone displayed to the guest and the laptop stores it untouched, including when the item is given
  away. `ProductionStatus` is how far the station has got with it: `Waiting`, `InProduction`,
  `Finished`, and it only ever moves forward. It also carries whether it has been settled:
  `SettledAtUtc` is the paid flag (null means still open, so a flag and a timestamp can never
  disagree), `ChargedPriceCents` is what was actually collected, and `PaymentNotice` is the reason
  typed when less than the displayed price was collected. A settled item is never settled again, so a
  double tap cannot double count. What a table still owes and what was given away are derived from
  these on every read, never stored.
- **OrderItemStatusChange**: an append-only log of every production status an item has been in, with
  the moment it moved there. It exists so the evening can be reconstructed and so waiting times can be
  measured afterwards. **It is never read to work out an item's current state**: that is
  `OrderItem.ProductionStatus`, and nothing else.
- **Device**: one phone or one tablet. It is owned 1:1 by exactly one `StaffMember` or one `Station`,
  and the owner points at it (`StaffMember.DeviceId`, `Station.DeviceId`), so an owner holds at most
  one device and at most one outstanding enrolment invitation. Setting a device up again deletes the
  old row, which is what revoking is.

The status of an order is calculated from the production status of its items, so the two can never
disagree. Enums persist as numbers with pinned values, so a member may be renamed freely but never
reordered. Counters live where they belong: `Station.NextStationOrderNumber` per station, and one
single row for the next global order number.

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
