# T003: Infrastructure persistence (vertical slice)

This is the original implementation brief, written before any of the code existed. Printing has
since been removed from the product entirely, and every printer instruction has been taken out of
this file. For a current description of the product, read `docs/spec.md`. Where this brief cites a
section number of the specification, it means the older and much longer version of that document. The
specification has since been cut back to purpose, decisions, reasons and the setup checklist, and the
source code is now the authority on every contract.

## 1. Objective

Build the persistence layer of `GastronomyApp.Infrastructure` that the vertical slice needs: the EF
Core `DbContext` over every entity in spec section 2, the frozen initial migration, repository
implementations for `IOrderRepository` and `INumberAllocator` (T002's ports), the transactional shell
around T002's `OrderAcceptanceService` (`BEGIN IMMEDIATE`, gapless counter allocation, same-transaction
idempotent lookup), and the device token / enrolment invitation store (this task's own ports,
PBKDF2-HMAC-SHA512, per-secret salt, hashed lookup). Test-first throughout: every behaviour is
proven by a red integration test before the code that greens it exists. Definition of done: `dotnet
test GastronomyApp.slnx --filter "FullyQualifiedName~GastronomyApp.Infrastructure"` green, `dotnet
build GastronomyApp.slnx` zero warnings, every scenario in spec section 11.2 that is listed as
Infrastructure-scope in section 7 below is covered.

Out of scope, explicit, restated at the end: HTTP, SignalR, any migration beyond the initial one,
seeding data.

## 2. Assumed from T002

T002 defines `GastronomyApp.Core`. This task cannot read its output, so every Core type and port this
task depends on is named here with the signature this task assumes. The consistency review reconciles
any mismatch against what T002 actually produced.

### 2.1 Domain entities

Plain C# classes (not EF entities, no ORM attributes, no navigation-property backing fields required
beyond ordinary collections), one file per type under the `GastronomyApp.Core.Entities` namespace
(T002 section 6), with every field from spec section 2 and the types spec section 2.1 specifies
(`Guid`, `string(n)` as `string` with the length enforced by Fluent API in this task, `int` cents,
`byte[]` for hash/salt columns). Types, matching the entity list in spec 2.2, with the field types in
the tables there: `EventSession`, `ProductionLocation`, `CatalogItem`, `ItemLocationAssignment`,
`TableSuggestion`, `ServerPerson`, `Device`, `EnrolmentInvitation`, `Order`, `OrderLine`,
`LocationTicket`, `NumberCounter`.

Status representation: T002 defines a C# `enum` per state machine (`OrderStatus`,
`LocationTicketStatus`) rather than storing the spec's naming strings directly as
free text. `Order.Status` is typed `OrderStatus`, `LocationTicket.Status` is typed
`LocationTicketStatus`, and each enum's member names are
exactly the state names spec sections 3.1 and 3.2 use, confirmed row by row against T002. This task
maps each enum to a SQLite `TEXT`
column via `.HasConversion<string>()`, never to an `int`, so a manual look at the database during a
live event shows a readable value.

One assumed value type that is not a full entity:
- `CounterKind`: assumed to be a C# `enum` with members `GlobalOrder` and `LocationSequence`, mapped
  the same `HasConversion<string>()` way, matching spec 2.13 exactly.

### 2.2 Ports (interfaces) this task implements

T002 declares `IOrderRepository` and `INumberAllocator` in `GastronomyApp.Core.Ports`, and the order
acceptance request/result records in `GastronomyApp.Core.Services` and `GastronomyApp.Core.Results`.
This task implements those interfaces exactly as T002 declares them and does not widen them:

```csharp
namespace GastronomyApp.Core.Ports;

public interface IOrderRepository
{
    Task<Order?> FindByClientOrderIdAsync(Guid clientOrderId, CancellationToken cancellationToken);
    Task AddAsync(Order order, CancellationToken cancellationToken);
}

public interface INumberAllocator
{
    Task<int> AllocateGlobalOrderNumberAsync(Guid eventSessionId, CancellationToken cancellationToken);
    Task<int> AllocateLocationSequenceNumberAsync(Guid eventSessionId, Guid productionLocationId, CancellationToken cancellationToken);
}
```

`IOrderRepository` is a plain storage port: it does not decide anything about acceptance. The domain
logic that used to be assumed to live inside an `AcceptAsync` method here (global number allocation,
per-location ticket creation, sequence number allocation, line insertion, total computation) belongs to
`GastronomyApp.Core.Services.OrderAcceptanceService.AcceptAsync(OrderAcceptanceRequest request,
CancellationToken cancellationToken)`, which is T002's, not this task's. Section 4 step 4 below
describes exactly how this task composes the two: `OrderAcceptanceService` decides, `IOrderRepository`
and `INumberAllocator` are what it decides through, and this task's own code supplies only the
transaction shell around that call.

The request and result records this task's step 4 constructs and reads, as T002 declares them:

```csharp
namespace GastronomyApp.Core.Services;

public sealed record OrderAcceptanceRequest(
    Guid ClientOrderId,
    Guid EventSessionId,
    Guid ServerPersonId,
    Guid DeviceId,
    string TableLabel,
    string? Note,
    IReadOnlyList<OrderAcceptanceLineRequest> Lines);

public sealed record OrderAcceptanceLineRequest(
    Guid CatalogItemId,
    Guid? ProductionLocationId,
    int Quantity,
    string? Note);
```

```csharp
namespace GastronomyApp.Core.Results;

public sealed record OrderAcceptanceResult
{
    public required Order Order { get; init; }
    public required bool WasAlreadyAccepted { get; init; }
}
```

Note the field rename inside the line request: T002 calls the chosen station `ProductionLocationId`
(matching spec 5.4's request field `productionLocationId`), not `ChosenProductionLocationId`. The
entity field stays `OrderLine.ChosenProductionLocationId`; the request field and the entity field are
deliberately different words, and this task's persistence code is the one place that maps one onto the
other.

Device and enrolment ports are this task's own, not T002's (T002 excludes them; no domain service
consumes them):

```csharp
namespace GastronomyApp.Core.Ports;

public interface IDeviceTokenStore
{
    Task<IssuedDeviceToken> IssueAsync(Guid serverPersonId, string language, string userAgentSnapshot, CancellationToken cancellationToken);
    Task<DeviceVerificationResult> VerifyAsync(string tokenLookupId, string secret, CancellationToken cancellationToken);
    Task RevokeAsync(Guid deviceId, CancellationToken cancellationToken);
}

public sealed record IssuedDeviceToken(Device Device, string PlaintextToken);

public record DeviceVerificationResult(bool IsValid, Device? Device);

public interface IEnrolmentInvitationStore
{
    Task<EnrolmentInvitationCreated> CreateAsync(Guid? serverPersonId, CancellationToken cancellationToken);
    Task<EnrolmentRedemptionResult> RedeemAsync(EnrolmentRedemptionRequest request, CancellationToken cancellationToken);
}

public record EnrolmentInvitationCreated(Guid InvitationId, string QrCodeValue, string SixDigitCode, DateTime ExpiresAtUtc);

public record EnrolmentRedemptionRequest(string? Code, string? SixDigitCode, string Name, string UserAgent, string AcceptLanguageHeader);

public record EnrolmentRedemptionResult(EnrolmentRedemptionOutcome Outcome, Device? Device, ServerPerson? ServerPerson);

public enum EnrolmentRedemptionOutcome { Redeemed, CodeInvalid, CodeExpired, SixDigitAttemptsExhausted }
```

`IDeviceTokenStore.IssueAsync` returns `IssuedDeviceToken` rather than the bare `Device` entity,
because the plaintext token (spec 5.2's `deviceToken`, returned exactly once) has nowhere else to
travel from this call: `Device` itself never carries the plaintext, only its hash. `VerifyAsync`
returns the named `DeviceVerificationResult` rather than a bare `Device?`, because a caller needs to
distinguish "no such token" from "revoked" from "wrong secret" only as far as "invalid", and this
shape is deliberately the narrower one; any consumer wanting a bare `Device?` adapts at its own
boundary.

`Order`, `Device`, `ServerPerson` above are the Core domain entities from section 2.1, not DTOs.

### 2.3 Infrastructure exception for database-unavailable

Per architect ruling 1, database unavailability is a cross-cutting **infrastructure** concern, not a
Core domain result. This task declares the type itself, in `GastronomyApp.Infrastructure`, not in
Core:

```csharp
namespace GastronomyApp.Infrastructure;

public enum InfrastructureFailureReason { DatabaseUnavailable }

public sealed class InfrastructureException : Exception
{
    public InfrastructureFailureReason Reason { get; }
    public InfrastructureException(InfrastructureFailureReason reason, string message, Exception? inner = null)
        : base(message, inner) => Reason = reason;
}
```

This task's repositories catch `Microsoft.Data.Sqlite.SqliteException` for `SQLITE_BUSY` /
`SQLITE_IOERR` / `SQLITE_CORRUPT` / `SQLITE_READONLY` cases that survive the 5 second busy timeout and
rethrow as `InfrastructureException(InfrastructureFailureReason.DatabaseUnavailable, ...)`, never
swallowing the original exception (it becomes `inner`). This is the type the (out of scope, later)
API-layer exception middleware catches and maps to HTTP 503 with `code: DatabaseUnavailable`, and per
ruling 1 it is the **only** type that middleware catches; this task does not touch HTTP and only needs
the type to exist, live in the `GastronomyApp.Infrastructure` namespace, and be thrown correctly.

## 3. File layout

```
backend/
  GastronomyApp.Infrastructure/
    GastronomyAppDbContext.cs
    Configurations/
      EventSessionConfiguration.cs
      ProductionLocationConfiguration.cs
      CatalogItemConfiguration.cs
      ItemLocationAssignmentConfiguration.cs
      TableSuggestionConfiguration.cs
      ServerPersonConfiguration.cs
      DeviceConfiguration.cs
      EnrolmentInvitationConfiguration.cs
      OrderConfiguration.cs
      OrderLineConfiguration.cs
      LocationTicketConfiguration.cs
      NumberCounterConfiguration.cs
    Migrations/
      <timestamp>_InitialCreate.cs
      <timestamp>_InitialCreate.Designer.cs
      GastronomyAppDbContextModelSnapshot.cs
    Repositories/
      OrderRepository.cs
      OrderAcceptanceTransaction.cs
      NumberCounterAllocator.cs
      DeviceTokenStore.cs
      EnrolmentInvitationStore.cs
    Security/
      Pbkdf2SecretHasher.cs
    SqliteConnectionFactory.cs
  GastronomyApp.Infrastructure.Tests/
    GastronomyAppDbContextTest.cs
    OrderRepositoryTest.cs
    OrderAcceptanceTransactionTest.cs
    NumberCounterAllocatorTest.cs
    DeviceTokenStoreTest.cs
    EnrolmentInvitationStoreTest.cs
    Pbkdf2SecretHasherTest.cs
    TestSupport/
      SqliteInMemoryFixture.cs
```

Delete `backend/GastronomyApp.Infrastructure.Tests/ScaffoldingSmokeTest.cs` (T001's placeholder) in
the same step that adds the first real fixture, per T001's own rule.

## 4. Ordered steps

Each step pairs a red integration test with the production code that greens it. Run all commands from
the repository root (`E:\Development\GastronomyApp`).

### Step 0: Test infrastructure

Create `backend/GastronomyApp.Infrastructure.Tests/TestSupport/SqliteInMemoryFixture.cs`: opens a
`Microsoft.Data.Sqlite.SqliteConnection` on `Data Source=:memory:`, keeps it open for the test's
lifetime (SQLite's `:memory:` database is destroyed when the last connection to it closes, so EF
Core's own connection-per-context-instance default would wipe the schema between calls), builds a
`GastronomyAppDbContext` from a `DbContextOptionsBuilder` pointed at that open connection, and runs
`Database.Migrate()` once per test. Implements `IDisposable`, closing the connection in `Dispose`.
This satisfies backend rule 4 (`Data Source=:memory:`, no developer filesystem) for every test that
does not specifically exercise the on-disk `Path.GetTempPath()` behaviour (Step 8 needs a real file,
since `:memory:` cannot simulate a read-only directory).

No production code in this step; it is test scaffolding only, so there is no red/green pair.

### Step 1: `GastronomyAppDbContext` and the entity configurations

Red test first, `GastronomyAppDbContextTest.cs`:

```csharp
[Test]
public async Task Migrate_OnEmptyDatabase_CreatesEveryTable()
```

Asserts that after `Database.Migrate()` on a fresh `:memory:` connection, each of the twelve tables
from spec 2.2 exists (query `sqlite_master` for `type = 'table'` and assert the name set, excluding
EF's own `__EFMigrationsHistory`). This test is red until Step 2 (the migration) exists, so Steps 1
and 2 are written together: the `DbContext` and its configurations are meaningless without a migration
to apply, and the migration cannot be generated without the `DbContext`. Write the `DbContext` and
configurations first, run `dotnet ef migrations add InitialCreate` to generate the migration (that
command itself is the point where the red test in Step 2 goes green), then confirm this test passes
too.

Implementation, `GastronomyAppDbContext.cs`:

```csharp
namespace GastronomyApp.Infrastructure;

public sealed class GastronomyAppDbContext : DbContext
{
    public GastronomyAppDbContext(DbContextOptions<GastronomyAppDbContext> options) : base(options) { }

    public DbSet<EventSession> EventSessions => Set<EventSession>();
    public DbSet<ProductionLocation> ProductionLocations => Set<ProductionLocation>();
    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<ItemLocationAssignment> ItemLocationAssignments => Set<ItemLocationAssignment>();
    public DbSet<TableSuggestion> TableSuggestions => Set<TableSuggestion>();
    public DbSet<ServerPerson> ServerPeople => Set<ServerPerson>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<EnrolmentInvitation> EnrolmentInvitations => Set<EnrolmentInvitation>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<LocationTicket> LocationTickets => Set<LocationTicket>();
    public DbSet<NumberCounter> NumberCounters => Set<NumberCounter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GastronomyAppDbContext).Assembly);
    }
}
```

Each `Configurations/*Configuration.cs` implements `IEntityTypeConfiguration<T>` and states every
`string(n)` length from spec 2.2's tables with `.HasMaxLength(n)`, every `[nullable]` field as
`.IsRequired(false)`, and the constraints spec section 2 calls out explicitly:

- `OrderConfiguration`: `builder.HasIndex(o => o.ClientOrderId).IsUnique();` per spec 2.9 ("`ClientOrderId`
  carries a unique index and is the whole duplicate protection for a resubmission").
- `NumberCounterConfiguration`: `builder.HasKey(c => new { c.CounterKind, c.EventSessionId,
  c.ProductionLocationId });` matching spec 2.13's composite primary key exactly,
  with `CounterKind` mapped `HasConversion<string>().HasMaxLength(20)`. EF Core allows nullable columns
  inside a composite primary key on the SQLite
  provider (they are stored, and SQLite treats a primary key containing a nullable column as not
  enforcing true uniqueness across NULLs the way a `UNIQUE` constraint would); this task accepts that
  and does not add a workaround unique index, because the counter allocator in Step 3 is the only
  writer and always supplies the same null/non-null pattern per `CounterKind`, so
  no two logical counters can collide on all three columns having equal values including matching nulls.
- `OrderLineConfiguration`: `ItemNameSnapshot` and `UnitPriceCentsSnapshot` are plain persisted columns
  (spec 2.9's "snapshot fields on order lines"), not computed or shadow properties, so an item rename
  or price change never changes an existing line, per spec 2.5's invariant.
- `DeviceConfiguration`: `builder.HasIndex(d => d.TokenLookupId).IsUnique();`. `TokenHash` and
  `TokenSalt` mapped as `byte[]` (SQLite `BLOB`), never `string`.
- `EnrolmentInvitationConfiguration`: a partial unique filtered index expressing spec 2.8's "at most
  one invitation is outstanding at any moment": `builder.HasIndex(i => i.Id).HasFilter("ConsumedAtUtc
  IS NULL")` is not sufficient on its own since it does not enforce singleton-ness; the actual
  constraint needed is a unique index over a constant expression filtered to the outstanding condition.
  SQLite supports this as a partial unique index over an always-equal expression:
  `builder.HasIndex("ConsumedAtUtc").IsUnique().HasFilter("ConsumedAtUtc IS NULL")` does not by itself
  guarantee singleton either, because `ConsumedAtUtc IS NULL` is not a value EF can dedupe on when
  multiple rows share it (a unique index over a column that is always NULL for every matching row does
  not conflict in SQLite, since SQLite's default treats NULL as distinct from every other NULL in a
  UNIQUE index). Because of that, this task does **not** rely on `ExpiresAtUtc`/`ConsumedAtUtc` values
  themselves for the uniqueness and instead adds a computed generated column that is deterministically
  `1` for every row that is currently outstanding and `NULL` otherwise, then places the unique index on
  that column: a raw SQL migration statement (see Step 2) creates `IsOutstandingMarker` as `GENERATED
  ALWAYS AS (CASE WHEN ConsumedAtUtc IS NULL AND ExpiresAtUtc > CURRENT_TIMESTAMP THEN 1 END) STORED`
  with a `UNIQUE` constraint on it, because SQLite's NULL-distinctness only kicks in for `NULL` values,
  and every truly-outstanding row computes the same non-null `1`. This is expressed as a raw SQL step
  inside the generated migration (`migrationBuilder.Sql(...)`) rather than through the Fluent API,
  because EF Core's Fluent API has no first-class generated-column primitive for SQLite; the model
  snapshot still tracks the column via `.HasColumnType("INTEGER")` and `.HasComputedColumnSql(...)` so
  future migrations see it. Document this reasoning as the answer to the ambiguity flagged in section
  8 below rather than silently picking a weaker index.
- Every `Guid` primary key: `.ValueGeneratedNever()`, because Core generates ids (spec 2.1: "`Guid`
  primary keys are generated on the backend, except `Order.ClientOrderId`"), never the database.

WAL mode and the 5 second busy timeout are not `OnModelCreating` concerns; they are connection-level
pragmas, handled in Step 2's `SqliteConnectionFactory`.

### Step 2: WAL mode, busy timeout, and the initial migration

Red test, appended to `GastronomyAppDbContextTest.cs`:

```csharp
[Test]
public async Task OpenConnection_OnFile_SetsWalModeAndBusyTimeout()
```

Uses a real temp file (`Path.Combine(Path.GetTempPath(), $"gastronomyapp-test-{Guid.NewGuid():N}.db")`,
deleted in teardown, matching backend rule 4's temp-dir allowance), opens it through
`SqliteConnectionFactory`, and asserts `PRAGMA journal_mode` returns `wal` and `PRAGMA busy_timeout`
returns `5000`. WAL mode does not apply to a pure `:memory:` connection (SQLite silently keeps such a
connection in its default `memory` journal mode since WAL requires a real file), which is exactly why
this one test uses a file instead of the `SqliteInMemoryFixture`, and why the fixture itself does not
try to assert WAL.

Implementation, `SqliteConnectionFactory.cs`:

```csharp
namespace GastronomyApp.Infrastructure;

public sealed class SqliteConnectionFactory
{
    public SqliteConnection Open(string dataSource)
    {
        var connection = new SqliteConnection($"Data Source={dataSource}");
        connection.Open();
        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode = WAL; PRAGMA busy_timeout = 5000;";
            pragma.ExecuteNonQuery();
        }
        return connection;
    }
}
```

Not `static`, per root rule 1 (no static methods). Registered as the thing the (out of scope) API
composition root constructs `DbContextOptions` from; this task's own tests construct it directly.

Then generate the migration:

```powershell
dotnet ef migrations add InitialCreate --project backend/GastronomyApp.Infrastructure --startup-project desktop/GastronomyApp.Desktop
```

Per backend rule 7, this migration is frozen the moment it ships: once applied to a real fire
department laptop, it is never edited, renamed, reordered, or deleted, only ever followed by a new
corrective migration. State this explicitly as a comment is forbidden (root rule 7), so state it here
in this document instead, and the review checklist for every later task that touches
`GastronomyApp.Infrastructure` re-confirms no one edited `Migrations/<timestamp>_InitialCreate.cs`.

Add the `IsOutstandingMarker` generated column and its unique index via `migrationBuilder.Sql(...)`
appended to the generated `Up()` method (and a corresponding `DROP` in `Down()`), since EF's scaffolder
does not know about it.

### Step 3: `INumberAllocator`

Three red tests first, `NumberCounterAllocatorTest.cs`:

```csharp
[Test]
public async Task AllocateGlobalOrderNumberAsync_FirstCallForSession_Returns1()

[Test]
public async Task AllocateGlobalOrderNumberAsync_AfterNRolledBackTransactions_DoesNotSkipNumbers()

[Test]
public async Task AllocateGlobalOrderNumberAsync_SimulatedRestart_ContinuesFromPersistedValue()
```

The second test opens a transaction, allocates a number, rolls back without committing, opens a fresh
`GastronomyAppDbContext` against the same file-backed database, and asserts the next allocation
returns the same value the rolled-back attempt got, proving spec 4.2's "a number is allocated only by
a transaction that commits" holds and that a rollback consumes no number (see section 8 below for why
this is the chosen reading, stated explicitly since the spec states the rule but this task had to
decide the test's exact shape). The third test disposes the `DbContext` and connection entirely between
allocations against a temp file, opens a new one, and asserts continuity, proving spec 4.3's
"the first order after a restart takes the next value straight from the table."

Implementation, `NumberCounterAllocator.cs`, implementing `INumberAllocator`. Each `AllocateX`
method:

1. `SELECT NextValue FROM NumberCounter WHERE CounterKind = @kind AND EventSessionId = @sessionOrNull
   AND ProductionLocationId = @locationOrNull` inside the
   ambient transaction (never opens its own; the caller, `OrderAcceptanceTransaction` per Step 4, owns
   the transaction).
2. If no row exists, insert one with `NextValue = 1` and return `1`.
3. If a row exists, `UPDATE ... SET NextValue = NextValue + 1 WHERE <same composite key>` and return the
   pre-increment value.

### Step 4: `IOrderRepository`, and the acceptance transaction shell around `OrderAcceptanceService`

**Ownership, stated once so nothing below re-litigates it.** `GastronomyApp.Core.Services.
OrderAcceptanceService.AcceptAsync(OrderAcceptanceRequest, CancellationToken)` is the one and only
place that decides a global order number, a `LocationTicket` per distinct routed location, each
location's sequence number, every `OrderLine`, and the order's `TotalCents`. This task never
reimplements any of that. What this task owns is the transaction shell: opening `BEGIN IMMEDIATE`,
performing the same-transaction `ClientOrderId` lookup, invoking `OrderAcceptanceService.AcceptAsync`
exactly once inside that transaction, persisting what it decided through `IOrderRepository.AddAsync`,
and committing or rolling back. `OrderAcceptanceService` reaches the database only through
`IOrderRepository` and `INumberAllocator`, both implemented in this task; it never opens a connection
or a transaction itself.

Red tests first, `OrderRepositoryTest.cs` (over `IOrderRepository`'s two members directly) and
`OrderAcceptanceTransactionTest.cs` (over the composed shell plus `OrderAcceptanceService`):

```csharp
[Test]
public async Task AddAsync_NewOrder_PersistsOrderTicketsAndLines()

[Test]
public async Task FindByClientOrderIdAsync_UnknownId_ReturnsNull()

[Test]
public async Task AddAsync_TotalDisagreesWithLines_Throws()
```

```csharp
[Test]
public async Task AcceptAsync_NewClientOrderId_InsertsOrderTicketsAndLines()

[Test]
public async Task AcceptAsync_RepeatClientOrderId_ReturnsExistingOrderAndInsertsNothing()

[Test]
public async Task AcceptAsync_TwoParallelSubmissionsSameClientOrderId_ProduceExactlyOneOrder()

[Test]
public async Task AcceptAsync_MultipleLocations_AllocatesIndependentSequenceNumbers()
```

The four `OrderAcceptanceTransactionTest` methods are the same four scenarios this document's earlier
draft attributed to a repository-owned `AcceptAsync`; they survive verbatim as integration tests, only
their target moves from `OrderRepository` to `OrderAcceptanceTransaction` composed with the real
`OrderAcceptanceService`. The third is the concurrency proof required by spec 11.2 ("a hundred parallel
duplicates create exactly one order"): it fires two (this task's version; a higher-count variant is
acceptable but two concurrent callers against `BEGIN IMMEDIATE`'s single-writer serialization is enough
to prove no race, since SQLite's write lock makes every write transaction fully serial regardless of
caller count) concurrent `Task.WhenAll` calls with the same `ClientOrderId` from two separate
`GastronomyAppDbContext` instances against the same file-backed database, and asserts exactly one row
exists in `Orders` afterward and both calls returned the same `Order.Id`.

Implementation, `Repositories/OrderRepository.cs`, implementing `IOrderRepository`'s two members only:

- `FindByClientOrderIdAsync`: a plain `SELECT` by the unique `ClientOrderId` index from Step 1,
  including the order's lines and tickets (the caller needs the full aggregate back, not just the row).
- `AddAsync`: inserts the `Order` row, one `LocationTicket` per distinct `ProductionLocationId` already
  present on the order's lines, and every `OrderLine`, exactly as `OrderAcceptanceService` already
  built them onto the `Order` aggregate before calling this method. This is a pure persistence write:
  it does not allocate numbers (already set on the aggregate by the time it arrives here) and does not
  decide routing (already resolved). The one thing it does that is genuinely this task's own concern,
  not a re-decision of Core's: it recomputes `Sum(Quantity * UnitPriceCentsSnapshot)` across the
  aggregate's lines and throws if it disagrees with `Order.TotalCents` before writing, as the storage-
  layer assertion spec 2.9 calls for ("asserted in the acceptance transaction and covered by a test").
  This is a defensive check on what `OrderAcceptanceService` already computed, not a second computation
  of the figure: if it ever fires, `OrderAcceptanceService` has a bug, and this assertion is what turns
  that bug into a loud test failure instead of a silently wrong total reaching the database.

Implementation, `Repositories/OrderAcceptanceTransaction.cs`, the shell:

1. Open the underlying `SqliteConnection` (via `SqliteConnectionFactory`, not `:memory:` for
   production use) and begin a transaction with `BEGIN IMMEDIATE` explicitly (EF Core's
   `Database.BeginTransactionAsync()` defaults to `BEGIN DEFERRED` on the SQLite provider, which does
   not take the write lock until the first write statement runs and therefore does not serialize
   readers the way spec 4.1 step 1 requires; this task issues `BEGIN IMMEDIATE` as a raw
   `Database.ExecuteSqlRawAsync("BEGIN IMMEDIATE")` and manages commit/rollback itself rather than
   using the `IDbContextTransaction` wrapper, because that wrapper has no API to select `IMMEDIATE`).
2. Inside the transaction, call `IOrderRepository.FindByClientOrderIdAsync(request.ClientOrderId, ct)`.
   If found, commit (nothing to write) and return `OrderAcceptanceResult { Order = existingOrder,
   WasAlreadyAccepted = true }`. This is the same-transaction lookup spec 9.3 and 4.1 both require:
   because `BEGIN IMMEDIATE` already holds the write lock, no other writer can insert a matching row
   between this read and the next step, so there is no window for a duplicate to slip in even under the
   two-parallel-callers test above (the second caller blocks on `BEGIN IMMEDIATE` until the first
   commits, then its own lookup finds the first caller's row).
3. If not found, call `OrderAcceptanceService.AcceptAsync(request, ct)`, passing `IOrderRepository` and
   `INumberAllocator` (both this task's implementations) as the dependencies `OrderAcceptanceService`
   was constructed with. `OrderAcceptanceService` allocates the global order number and every location
   sequence number through `INumberAllocator`, builds the `Order` aggregate with its tickets and lines,
   and calls `IOrderRepository.AddAsync` itself to persist it, still inside this same ambient
   transaction (the transaction is opened before `AcceptAsync` is called and committed only after it
   returns, so every allocator call and the `AddAsync` write share the one `BEGIN IMMEDIATE` scope).
4. Commit, and return `OrderAcceptanceResult { Order = <the aggregate OrderAcceptanceService built>,
   WasAlreadyAccepted = false }`. On any `SqliteException` indicating the busy timeout was exceeded or a
   disk/IO failure, catch, roll back if the transaction is still open, and throw
   `InfrastructureException(InfrastructureFailureReason.DatabaseUnavailable, ...)` per section 2.3,
   never letting the raw `SqliteException` reach the caller (root rule 2).

Snapshot fields (`ItemNameSnapshot`, `UnitPriceCentsSnapshot`) are populated by `OrderAcceptanceService`
from a `CatalogItem` lookup it performs through a Core-side read port inside the same transaction
(reading current name/price at acceptance time, per spec 2.5: "editing a name or price never changes an
existing order... every `OrderLine` carries a snapshot" and spec 11.2's price scenario: "the accepted
total is computed from the backend's current prices"); this task supplies whatever `CatalogItem`-lookup
port `OrderAcceptanceService` requires as a repository implementation, alongside `IOrderRepository` and
`INumberAllocator`, but does not itself decide what gets snapshotted.

### Step 5: `Pbkdf2SecretHasher`

Red tests first, `Pbkdf2SecretHasherTest.cs`:

```csharp
[Test]
public void Hash_ThenVerify_SameSecret_ReturnsTrue()

[Test]
public void Verify_DifferentSecret_ReturnsFalse()

[Test]
public void Hash_TwoCallsSameSecret_ProducesDifferentSaltAndDifferentHash()

[Test]
public void Verify_HonoursStoredIterationCountEvenIfDefaultChanges()
```

Not an integration test in the SQLite sense (no database), but it lives in
`GastronomyApp.Infrastructure.Tests` because it is the security primitive both stores below depend on
and root rule 5 / backend rule 6 require it be exactly this: `System.Security.Cryptography.Rfc2898DeriveBytes`
built in to the BCL, never a third-party crypto package.

Implementation, `Security/Pbkdf2SecretHasher.cs`:

```csharp
namespace GastronomyApp.Infrastructure.Security;

public sealed class Pbkdf2SecretHasher
{
    private const int DefaultIterations = 210_000;
    private const int SaltLengthBytes = 16;
    private const int HashLengthBytes = 32;
    private const string AlgorithmName = "PBKDF2-HMAC-SHA512";

    public HashedSecret Hash(string secret)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLengthBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(secret, salt, DefaultIterations, HashAlgorithmName.SHA512, HashLengthBytes);
        return new HashedSecret(hash, salt, DefaultIterations, AlgorithmName);
    }

    public bool Verify(string secret, byte[] storedHash, byte[] storedSalt, int storedIterations, string storedAlgorithm)
    {
        if (storedAlgorithm != AlgorithmName)
        {
            return false;
        }
        var computed = Rfc2898DeriveBytes.Pbkdf2(secret, storedSalt, storedIterations, HashAlgorithmName.SHA512, storedHash.Length);
        return CryptographicOperations.FixedTimeEquals(computed, storedHash);
    }
}

public sealed record HashedSecret(byte[] Hash, byte[] Salt, int Iterations, string Algorithm);
```

`FixedTimeEquals` guards against a timing side channel on the comparison, not required by the spec
explicitly but a direct consequence of backend rule 6 ("Device tokens are treated as credentials"; the
same reasoning applies to the invitation secrets, which the spec explicitly hashes the same way).

### Step 6: `IDeviceTokenStore`

Red tests first, `DeviceTokenStoreTest.cs`:

```csharp
[Test]
public async Task IssueAsync_ThenVerifyAsync_WithReturnedSecret_IsValid()

[Test]
public async Task VerifyAsync_WrongSecret_IsInvalid()

[Test]
public async Task VerifyAsync_RevokedDevice_IsInvalid()

[Test]
public async Task VerifyAsync_UnknownTokenLookupId_IsInvalid()

[Test]
public async Task IssueAsync_NewToken_NeverPersistsPlaintextSecretAnywhere()
```

The last test asserts (by inspecting the raw row via a direct `SqliteCommand` against the
`GastronomyAppDbContext`'s connection, bypassing EF's mapped shape) that no column contains the
plaintext secret substring, guarding backend rule 6's "never stored in plaintext" at the storage layer
specifically, since a unit test on the hasher alone cannot prove the repository did not also write the
plaintext into some other column by mistake.

Implementation, `Repositories/DeviceTokenStore.cs`, implementing `IDeviceTokenStore`:

- `IssueAsync`: generates a `TokenLookupId` (assumed: 32 random hex characters, matching the type
  column's length in spec 2.8's `Device` table) and a random secret (assumed: the part after the dot in
  spec 5.1's `Authorization: Bearer <TokenLookupId>.<secret>`, a separately generated high-entropy
  random string, not derived from `TokenLookupId`), hashes the secret with `Pbkdf2SecretHasher`, inserts
  the `Device` row, and returns `IssuedDeviceToken(device, plaintextToken)` per section 2.2, where
  `plaintextToken` is `$"{tokenLookupId}.{secret}"`. The plaintext token is returned to the caller
  exactly once through this record and is never written back to any column.
- `VerifyAsync(tokenLookupId, secret)`: looks up the one `Device` row by `TokenLookupId` (spec 5.1: "the
  backend splits on the dot, loads the one device row by `TokenLookupId`, and verifies the secret with
  PBKDF2 using that row's stored salt, iteration count, and algorithm"). If no row, or
  `RevokedAtUtc != null`, or `Pbkdf2SecretHasher.Verify` fails, returns `DeviceVerificationResult(false,
  null)`. On success, updates `LastSeenAtUtc` in the same call (spec 2.8 has the column; nothing else
  in this task's scope writes it) and returns `DeviceVerificationResult(true, device)`.
- `RevokeAsync`: sets `RevokedAtUtc = DateTime.UtcNow`. Per backend rule 6 ("revoking a device
  invalidates its token immediately"), this alone is sufficient since every `VerifyAsync` call checks
  it; no separate token-deletion step exists, so a revoked row's hash stays in the table as history
  (spec 2.8's admin diagnostic use of `UserAgentSnapshot` implies rows are kept, not deleted, and
  nothing in spec 2.8 says a revoked `Device` row is removed).

Never logs the secret or the token: this repository takes no `ILogger` dependency at all in this task's
scope, so there is no code path that could log it by accident (a later task wiring logging into
`GastronomyApp.Api` must not pass raw request bodies containing `Authorization` headers to a logger
either, but that is out of this task's scope to enforce).

### Step 7: `IEnrolmentInvitationStore`

Red tests first, `EnrolmentInvitationStoreTest.cs`:

```csharp
[Test]
public async Task CreateAsync_ThenRedeemAsync_WithReturnedQrCode_Redeems()

[Test]
public async Task CreateAsync_ThenRedeemAsync_WithReturnedSixDigitCode_Redeems()

[Test]
public async Task CreateAsync_Twice_ConsumesTheFirstOutstandingInvitation()

[Test]
public async Task RedeemAsync_WrongSixDigitCode_TenTimes_ExhaustsAttempts()

[Test]
public async Task RedeemAsync_WrongSixDigitCode_TenthAttempt_QrCodeStillVerifies()

[Test]
public async Task RedeemAsync_ExpiredInvitation_IsRejected()

[Test]
public async Task CreateAsync_TwoConcurrentCallers_LeaveExactlyOneOutstandingInvitation()
```

The last test is spec 11.2's "two invitations created concurrently leave exactly one outstanding":
fires two `CreateAsync` calls concurrently via `Task.WhenAll` against the same file-backed database and
asserts exactly one row in `EnrolmentInvitations` has `ConsumedAtUtc == null && ExpiresAtUtc >
DateTime.UtcNow` afterward, relying on the `IsOutstandingMarker` unique index from Step 1 to be what
actually enforces this under concurrency (the repository code below also expresses the "consume
whichever one was outstanding in the same transaction" rule explicitly; the two together are
belt-and-braces, per spec 2.8: "Without both halves the invariant is an assertion rather than a fact").

Implementation, `Repositories/EnrolmentInvitationStore.cs`, implementing `IEnrolmentInvitationStore`:

- `CreateAsync`: opens a `BEGIN IMMEDIATE` transaction (same reasoning as Step 4). `UPDATE
  EnrolmentInvitations SET ConsumedAtUtc = @now WHERE ConsumedAtUtc IS NULL AND ExpiresAtUtc >
  @now AND ConsumedByDeviceId IS NULL` first (consuming whatever was outstanding, without setting
  `ConsumedByDeviceId`, so a later query can still distinguish "consumed by being superseded" from
  "consumed by redemption" if a caller cares; this task does not surface that distinction anywhere else
  since the spec does not ask for it). Then generates a random QR code value and six-digit code
  (assumed: six-digit code is a random value `000000`-`999999` formatted with leading zeros), hashes
  both with `Pbkdf2SecretHasher`, inserts the new `EnrolmentInvitation` row with `ExpiresAtUtc =
  DateTime.UtcNow.AddMinutes(5)`, commits, and returns `EnrolmentInvitationCreated` carrying the
  plaintext values (returned once, per spec 2.8: "returned once, in the response that created the
  invitation... never written to the database and never fetchable again").
- `RedeemAsync`: loads the single outstanding row (`ConsumedAtUtc IS NULL AND ExpiresAtUtc >
  DateTime.UtcNow`, using the same `IsOutstandingMarker`-backed lookup). If none, returns
  `EnrolmentRedemptionResult(EnrolmentRedemptionOutcome.CodeExpired, null, null)`. If `request.Code` is
  supplied, verifies against `QrCodeHash`/`QrCodeSalt`; if `request.SixDigitCode` is supplied, first
  checks `FailedSixDigitAttempts < 10` (spec 2.8: "ten wrong six digit codes stop the six digits being
  accepted"), returning `SixDigitAttemptsExhausted` if not, then verifies against
  `SixDigitHash`/`SixDigitSalt`, and on a wrong six-digit code increments `FailedSixDigitAttempts` and
  returns `CodeInvalid` **without** touching `QrCodeHash` or its own expiry, so the QR code stays valid
  per spec 2.8's explicit invariant (proven by
  `RedeemAsync_WrongSixDigitCode_TenthAttempt_QrCodeStillVerifies`). On a verified code, opens (or
  reuses, if `CreateAsync`-style transaction management is chosen) a `BEGIN IMMEDIATE` transaction that
  atomically: resolves the `ServerPerson` (existing row if `invitation.ServerPersonId` is set, per
  `request.Name` written onto it; a new row created from `request.Name` otherwise), creates the
  `Device` row via `IDeviceTokenStore.IssueAsync` (this store depends on `IDeviceTokenStore` rather
  than duplicating the hashing/insert logic, root rule 6), which now returns `IssuedDeviceToken`; this
  store reads `.Device` off it for `EnrolmentRedemptionResult`, sets `invitation.ConsumedAtUtc` and
  `invitation.ConsumedByDeviceId`, and commits. Returns `EnrolmentRedemptionResult(Redeemed, device,
  serverPerson)`.

`Device.Language` is set per spec 2.8: German unless `request.AcceptLanguageHeader` asks for English
first. No Core class exists for this parsing (T002 declares no `AcceptLanguageParser`), so this task
adds a private method inside `EnrolmentInvitationStore` for it, since it is infrastructure-adjacent
(reading one HTTP header value) and not a domain rule.

### Step 8: Database-unavailable behaviour

Red test, appended to `OrderAcceptanceTransactionTest.cs` or a new `DatabaseUnavailableTest.cs`:

```csharp
[Test]
public async Task AcceptAsync_ReadOnlyDataDirectory_ThrowsInfrastructureExceptionDatabaseUnavailable()

[Test]
public async Task AcceptAsync_ConcurrentWriterHoldsLockPastBusyTimeout_ThrowsInfrastructureExceptionDatabaseUnavailable()
```

The first test creates a temp file, opens it once to run migrations, then marks the file read-only via
`File.SetAttributes(path, FileAttributes.ReadOnly)` (Windows-appropriate, matching this repository's
target platform per spec 10.2's "Version 1 targets Windows"; the test removes the read-only attribute
in teardown before deleting the temp file, or deletion fails), attempts `OrderAcceptanceTransaction.
AcceptAsync`, and asserts an
`InfrastructureException` with `Reason == DatabaseUnavailable` is thrown, never a raw
`SqliteException`. The second test holds an open uncommitted `BEGIN IMMEDIATE` transaction from one
connection against a temp file, and from a second connection with `busy_timeout` set very low (this
test overrides it to a small value like 200ms specifically to keep the test fast, rather than waiting
the real 5 seconds; production code still uses 5000ms per Step 2) attempts a write and asserts the same
mapped exception.

Implementation: this step is mostly already covered by the `catch` blocks written in Steps 4, 6, and 7;
this step's job is to make sure the catch clause's `SqliteException` filtering is broad enough to catch
the specific `SqliteErrorCode` values these two scenarios produce (`SQLITE_BUSY` = 5, `SQLITE_READONLY`
= 8, `SQLITE_IOERR` = 10 and its extended codes), verified by these two tests rather than assumed.

### Step 9: Full verification pass

```powershell
dotnet build GastronomyApp.slnx
dotnet test GastronomyApp.slnx --filter "FullyQualifiedName~GastronomyApp.Infrastructure"
```

Quote both outputs in full before considering this task done, per root rule 5.

## 5. Constraints restated

- No code comments beyond a short non-obvious why (root rule 7); the reasoning in this document is not
  a substitute for that rule inside the actual `.cs` files, it explains the design here instead.
- No static methods or properties (backend rule 1); `SqliteConnectionFactory`, `Pbkdf2SecretHasher`,
  and every repository are instance classes with injected dependencies.
- No empty catch blocks (backend rule 3 / root rule 2); every catch in this task either rethrows as
  `InfrastructureException` or is not written at all.
- No test touches the developer's real database or filesystem (backend rule 4); every test uses
  `Data Source=:memory:` or a `Path.GetTempPath()` file deleted in teardown.
- BCL PBKDF2 only, no third-party crypto package (backend rule 5, backend rule 6).
- The initial migration is frozen the moment this task ships it (backend rule 7); do not edit it in a
  later step of this same task once `dotnet ef migrations add` has produced it and tests pass against
  it, generate a corrective migration instead if a mistake is found after that point.
- No positional tuple access anywhere (backend rule 8); every multi-value return in this task is
  already a named `record` (T002's `OrderAcceptanceResult`, and this task's own `IssuedDeviceToken`,
  `DeviceVerificationResult`, `EnrolmentRedemptionResult`, `HashedSecret`, `EnrolmentInvitationCreated`).
- Never the em-dash character, never a hyphen substituting for one, anywhere in this document or in any
  file this task writes.
- No trademarked words in file names or identifiers.
- NUnit + FakeItEasy only (T001's pinned choice); this task's tests are integration tests against a
  real `GastronomyAppDbContext`, so FakeItEasy is not used inside `GastronomyApp.Infrastructure.Tests`
  itself (there is nothing to fake at this layer, every dependency is the real SQLite provider), but the
  package reference stays because T001 added it to every test project uniformly.

## 6. Verification

1. `dotnet build GastronomyApp.slnx`: zero warnings, zero errors, across every project including
   `GastronomyApp.Infrastructure` and `GastronomyApp.Infrastructure.Tests`.
2. `dotnet test GastronomyApp.slnx --filter "FullyQualifiedName~GastronomyApp.Infrastructure"`: every
   test listed in section 4 passes, reported with the real `Passed:`/`Failed:` totals quoted.
3. For each step in section 4, the red output was captured and quoted before the corresponding
   production code was written (TDD gate, root rule 3); this is a process requirement on whoever
   executes this task, not a single command to run at the end.
4. Manual confirmation that `Migrations/<timestamp>_InitialCreate.cs` was generated exactly once by
   `dotnet ef migrations add` and not hand-edited afterward, other than the `IsOutstandingMarker` raw
   SQL addition made before the first test run against it (adding to the same not-yet-shipped migration
   during this task is not the "shipped and frozen" case backend rule 7 forbids editing; nothing in
   this task's git history, since this task performs no git operations, ships this migration to a real
   laptop).

## 7. Section 11.2 scenarios covered by this task

Per the spec's own list, this task is the home for: Numbering (all six scenarios), Idempotency (all
three scenarios), the persistence half of Order acceptance (split across locations, snapshot of names
and prices, proven as integration tests over `OrderAcceptanceTransaction` composed with T002's real
`OrderAcceptanceService`; the routing-resolution decision itself is `OrderAcceptanceService`'s and
`OrderRoutingResolver`'s, T002's, and out of this task's scope), Enrolment
(the concurrency, consumption, expiry, and identity-preservation scenarios; the rate-limiting half
belongs to the API task), and the token-verification and wrong-secret-rejection halves of
Authentication (the address-based admin/station routing half of that row belongs to the API task).
Storage failures' "a busy database waits rather than failing" and "a write failure returns 503 with a
message key" are this task's Step 8 (the 503 mapping itself, HTTP-shaped, is the API task's job; this
task stops at producing the correctly typed exception). "A read-only database directory refuses to
start with a stated reason" is a desktop-host startup check (spec 10.2's writability check on launch)
and is out of this task's scope, though Step 8's read-only-file test proves the underlying repository
behaviour that check would rely on.

## 8. Spec ambiguity and the chosen reading

**Whether a rolled-back transaction consumes a counter number.** Spec section 4.2 states "a number is
allocated only by a transaction that commits" and "if anything in the acceptance path fails, the whole
transaction rolls back and the counter rolls back with it," which is unambiguous on its own: a rollback
undoes the `UPDATE NextValue = NextValue + 1` along with everything else in the transaction, exactly
like any other write in SQLite. There is no real ambiguity in the spec text itself, but the phrase "no
gaps" in spec 11.2's own scenario list ("a rolled back transaction consuming no number") could be
misread as requiring the counter's `NextValue` to specifically skip back over the attempted value even
though nothing separately tracks "attempted but not committed" allocations. This task's chosen reading,
made explicit because a wrong implementation here would silently reintroduce the exact failure mode
spec 4.2 is written against: rollback is simple transactional rollback with no special-casing, `NextValue`
reverts to whatever it was before the failed transaction began (via ordinary SQLite MVCC/rollback-journal
behaviour, not application code), and the very next successful transaction gets the value the failed one
would have gotten. `NumberCounterAllocatorTest.AllocateGlobalOrderNumberAsync_AfterNRolledBackTransactions_DoesNotSkipNumbers`
is the test that pins this reading down, so a future change that tried to "reserve" a number outside a
transaction (which spec 4.2 explicitly forbids: "there is no separate 'get a number' call that can
succeed while the order fails") would fail it immediately.

**The `EnrolmentInvitation` outstanding-uniqueness index.** Spec 2.8 states the invariant ("a partial
unique index over the outstanding condition... permits one such row and rejects a second") but does not
give the exact SQLite DDL, and a naive `HasFilter("ConsumedAtUtc IS NULL")` partial unique index on a
column that is not itself part of the uniqueness (there being no natural single column whose value must
be unique among outstanding rows, since `Id` is already unique and `ExpiresAtUtc` values can coincide)
does not actually enforce singleton-ness in SQLite, because SQLite's `UNIQUE` constraint treats every
NULL as distinct from every other NULL, so a partial index filtered to rows matching a condition still
allows unlimited rows if the indexed column itself is NULL or non-comparable across those rows. Section
4's Step 1 resolves this with a generated column (`IsOutstandingMarker`) that evaluates to the same
non-null constant (`1`) for every currently-outstanding row and to `NULL` otherwise, with a plain
`UNIQUE` constraint on that generated column: now every outstanding row collides on the same non-null
value and SQLite rejects the second insert, while every non-outstanding row is NULL and NULLs are
exempt from the uniqueness check by SQLite's default `UNIQUE` semantics, matching the spec's stated
intent exactly. This is called out explicitly because a reviewer reading only the Fluent API surface
might assume a `HasFilter(...).IsUnique()` call alone was sufficient, and it is not.
