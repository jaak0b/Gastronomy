# CLAUDE.md (backend)

Rules for the C# backend. The repository root `CLAUDE.md` binds here too; this file adds to it and
never contradicts it.

## The stack

ASP.NET Core on .NET 9, published as a **single-file self-contained executable**. No runtime install on
the operator's laptop, no separate web server, no reverse proxy: one process serves the REST API, the
SignalR hub, and the built frontend from `wwwroot`, on one port.

- **SQLite via EF Core.** One file. A backup is a file copy, which is what a volunteer can actually do.
- **SignalR** for every server-to-client push: print confirmed, print failed, printer out of paper,
  station online or offline, catalog changed. Clients never poll for state that the server already
  knows has changed.
- **REST** for commands and queries.

## Hard rules

1. **No static methods or properties.** Framework metadata registration is the only exception.

2. **Localization is resx-only.** Every translatable string lives in `Strings.en.resx` /
   `Strings.de.resx` or a domain-specific resx pair, resolved through the localization service. Both
   language files must contain every key. This includes the text printed on receipt slips: a slip is
   user-facing output, not a debug artifact.

3. **No empty catch blocks.** Log the error, and surface it to the user for anything they initiated.
   A printing failure must reach the phone of the server who placed the order, in their language, with
   a stated cause and a stated next step.

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

7. **Shipped migrations are frozen.** Once a migration has run on a real fire department's laptop, it
   is history. Editing, renaming, reordering, squashing, or deleting an existing migration or its
   designer file is forbidden: it desynchronizes the migrations history from the schema and bricks the
   app on startup. Migrations are append-only and forward-only. A wrong migration is corrected by a
   new corrective migration, never by touching the old one. The auto-managed model snapshot is the
   single file EF may regenerate.

8. **No positional tuple access.** Never read a tuple by element position (`.Item1`) and never
   destructure one positionally. Every multi-value return is a named `record` or `record struct` read
   by name, so reordering or renaming a member is a compile error rather than a silent value swap.

9. **Printer transports go behind `IPrinterTransport`.** Implementations: `NetworkPrinterTransport`
   (raw TCP 9100 to an Ethernet printer), `AgentPrinterTransport` (via the Pi agent, deferred), and
   `MockPrinterTransport`. Nothing above that interface may know which one it is talking to, and a
   station's transport is configuration, not code. New transports are new implementations, never a
   branch inside an existing one.

10. **`MockPrinterTransport` writes slips to a folder, and stays that simple.** One file per slip,
    holding exactly the content that would have been printed. No rendered station screen, no pile
    visualisation, no styling: a folder of files is enough to show that the right lines reached the
    right station. It must still be able to produce, on demand, every failure mode the real transports
    can: paper out, cover open, connection timeout, dropped socket mid-job, and a job whose outcome is
    genuinely unknown. Keep that to the smallest control a test or a demonstrator can trigger. This is
    how the whole system is developed and demonstrated without hardware, and it stays the test double
    afterwards.

11. **Print jobs are serialized per printer, and a job's outcome is never assumed.** The hardware
    constraints in the root `CLAUDE.md` are load-bearing: one connection at a time, 90 second timeout,
    re-query before re-sending. An order's print state is explicit and persisted, never inferred from
    "we sent it and got no error".

## Intended project structure

Planned, not yet built. Update this table as it lands.

| Project | Role |
|---|---|
| `GastronomyApp.Core` | Domain models, ports, use cases. No framework dependencies. |
| `GastronomyApp.Infrastructure` | EF Core SQLite, printer transports, device token store. |
| `GastronomyApp.Api` | ASP.NET Core host: REST endpoints, SignalR hub, static frontend, composition root. |
| `*.Tests` | Unit tests against Core, integration tests against Infrastructure and the API. |

## Testing

**NUnit** is the test framework. **FakeItEasy** provides fakes for port interfaces (`A.Fake<IXxx>()`).
Both are chosen because the owner already works with them; do not introduce a second framework or a
second faking library alongside them.

**Conventions:** fixture is `<ClassUnderTest>Test`, method is `MethodName_State_Expected`. One fixture
per production class, one file per fixture. Never a catch-all fixture name.

**Core tests** use fakes for port interfaces. **Infrastructure and API tests** use in-memory SQLite and
the mock printer transport.

Every printing test must cover the failure paths, not only the happy one: paper out before sending,
paper out mid-job, socket dropped with unknown outcome, station offline, and a re-send that must not
duplicate.

## Build and run

```powershell
dotnet build backend/GastronomyApp.Api/GastronomyApp.Api.csproj
dotnet run --project backend/GastronomyApp.Api
dotnet test backend --filter "FullyQualifiedName~MethodName"
dotnet ef migrations add <Name> --project backend/GastronomyApp.Infrastructure
```
