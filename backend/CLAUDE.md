# CLAUDE.md (backend)

Rules for the C# backend. The repository root `CLAUDE.md` binds here too; this file adds to it and
never contradicts it.

How the backend is layered, where a request is checked, how it is committed and how the wire
records reach the frontend are in the `architecture` skill. Adding a refusal and changing a wire
record have their pages in `how-to`.

## Hard rules

1. **No static methods or properties on classes that hold state or dependencies.** Statics exist only where there is nothing to inject: framework metadata registration, the `Names` constants, the `Refusal` factories, and a contract record's empty value such as `CatalogView.Empty`.

2. **Localization is resx-only.** Every translatable string lives in `Strings.en.resx` /
   `Strings.de.resx` or a domain-specific resx pair, resolved through the localization service. Both
   language files must contain every key.

3. **No test touches the developer's database or filesystem.** In-memory SQLite
   (`Data Source=:memory:`) and `Path.GetTempPath()` temp directories, disposed in teardown.

4. **NuGet packages: official Microsoft or highly regarded community only.** No niche or unmaintained
   single-author packages. Prefer built-in BCL APIs over a third-party dependency: this binary is
   handed to volunteer fire departments and every dependency is a thing that can rot.

5. **Device tokens are treated as credentials.** Stored hashed with built-in PBKDF2-HMAC-SHA512, a
   per-token random salt, and the iteration count plus algorithm stored alongside the hash. Never
   stored in plaintext, never logged, never reversible. Enrolment codes are single-use and
   short-lived: the first scan consumes the code, so a photographed QR cannot enrol a second device.
   Revoking a device invalidates its token immediately.

6. **Every schema change is a new migration. A committed migration is never edited; a new one in
   the current, uncommitted change is regenerated from the model.** The product has been installed
   since 2026-09-20 (release from commit 49fd605), so the migrations under
   `backend/GastronomyApp.Infrastructure/Migrations/` are history: editing,
   renaming, reordering, squashing, deleting or recreating a committed migration or its designer
   file is **forbidden**, `InitialCreate` included. A schema change is made by editing the model and
   adding a migration with `dotnet ef migrations add <Name> --project backend/GastronomyApp.Infrastructure`,
   forward only and append only; a wrong migration is corrected by a new one. Read the generated
   `Up` before handing back, because SQLite rebuilds a table for most alterations, and prove the
   change with the released-database upgrade test in `GastronomyApp.Infrastructure.Tests`: a database
   built by the released schema, rows in it, `MigrateAsync`, rows intact. The auto-managed model
   snapshot is the single committed file EF may regenerate. Enums persist as numbers with pinned
   values, so a member may be renamed freely but never reordered.

7. **No positional tuple access.** Never read a tuple by element position (`.Item1`) and never
   destructure one positionally. Every multi-value return is a named `record` or `record struct` read
   by name, so reordering or renaming a member is a compile error rather than a silent value swap.

8. **LINQ is written in method syntax.** Every query is a fluent chain (`.Where`, `.Select`,
   `.Join`, `.OrderBy`), never query-comprehension syntax (`from ... in ...`). The whole codebase
   keeps one query style, and a join stays a chain instead of a block of SQL pasted into C#.

9. **Every touched C# file is run through the ReSharper cleanup before hand-back.** Line width,
   wrapping, alignment, initializer layout and braces are settings in the repository
   `.editorconfig`, and every formatting setting lives there, never only in an IDE's own settings.
   Nobody wraps or formats by hand, and a diff that only a formatter could have made is a review
   finding. The cleanup runs on every C# file an agent created or edited, in the same turn as the
   edit; the `how-to` skill's page `format-csharp.md` holds the commands. This binds `desktop/` as
   well.

## Testing

**NUnit** is the test framework. **FakeItEasy** provides fakes for port interfaces (`A.Fake<IXxx>()`).
Both are chosen because the owner already works with them; do not introduce a second framework or a
second faking library alongside them.

**Conventions:** fixture is `<ClassUnderTest>Test`, method is `MethodName_State_Expected`. One fixture
per production class, one file per fixture. Never a catch-all fixture name.

**Service tests** run against the real DbContext on in-memory SQLite, because a fake cannot show
whether EF Core translates a query. FakeItEasy fakes the other ports, such as the clock.
**Infrastructure and API tests** use in-memory SQLite.

## Build and run

```powershell
dotnet build GastronomyApp.slnx
dotnet run --project desktop/GastronomyApp.Desktop
dotnet test backend/<TestProject>/<TestProject>.csproj --filter "FullyQualifiedName~<Fixture>"
dotnet ef migrations add <Name> --project backend/GastronomyApp.Infrastructure
```
