# T002: Core domain (vertical slice)

## 1. Objective

Build the pure domain of `GastronomyApp.Core` for the vertical slice: a server places an order on a
phone, it is split into per-location tickets, slips are dispatched, print state comes back. This task
produces entities, enums, ports (interfaces later implemented by `GastronomyApp.Infrastructure` and
`GastronomyApp.Api`), and the domain services / use cases that make that slice reasoning possible,
test-first throughout.

This is a domain-only task. No EF Core, no SQLite, no HTTP, no SignalR, no ESC/POS bytes, no printer
transports, no worker loop, no localization resource content, nothing frontend or desktop. Every type
in this task lives under `backend/GastronomyApp.Core`, has no framework dependency, and is proven by
`backend/GastronomyApp.Core.Tests`.

Every public signature in this document is exact and binding. Other tasks (routing consumers,
Infrastructure repository implementations, the API layer, the printing worker) build against these
signatures. Do not rename, reshape, or add optional parameters without updating this document; there is
none of that judgment room in this task, the signatures below are the contract.

Read first, in full: `CLAUDE.md` (repository root), `backend/CLAUDE.md`. Read `docs/spec.md` sections
2, 3, 4, 7.6, 9, and 11.1. Skim `docs/tasks/T001-scaffolding.md` to know the starting state: an empty
`GastronomyApp.Core` project (no source files) and `GastronomyApp.Core.Tests` (NUnit + FakeItEasy,
`ScaffoldingSmokeTest.cs` present, `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` both on).

## 2. File layout after completion

```
backend/
  GastronomyApp.Core/
    GastronomyApp.Core.csproj                 (unchanged from T001)
    Entities/
      EventSession.cs
      ProductionLocation.cs
      CatalogItem.cs
      ItemLocationAssignment.cs
      TableSuggestion.cs
      ServerPerson.cs
      Device.cs
      EnrolmentInvitation.cs
      Order.cs
      OrderLine.cs
      LocationTicket.cs
      PrintJob.cs
      PrintAttempt.cs
      NumberCounter.cs
    Enums/
      OrderStatus.cs
      LocationTicketStatus.cs
      PrintJobStatus.cs
      PrintJobKind.cs
      PrintFailureReason.cs
      PrintAttemptOutcome.cs
      PrintAttemptPhase.cs
      TransportKind.cs
      NumberCounterKind.cs
    Results/
      OrderAcceptanceResult.cs
      OrderValidationFailure.cs
      RoutingDecision.cs
      GiveUpWindowEvaluation.cs
      PrintOutcomeMapping.cs
      TicketAcknowledgeDecision.cs
    Ports/
      IClock.cs
      ICatalogItemRepository.cs
      IProductionLocationRepository.cs
      IOrderRepository.cs
      INumberAllocator.cs
      IPrinterStatusReader.cs
    Services/
      OrderRoutingResolver.cs
      OrderStatusCalculator.cs
      OrderTotalCalculator.cs
      OrderAcceptanceService.cs
      TicketStateMachine.cs
      PrintJobStateMachine.cs
      RetryPolicy.cs
      GiveUpWindowCalculator.cs
      TicketAcknowledgePolicy.cs
      Never.cs
  GastronomyApp.Core.Tests/
    GastronomyApp.Core.Tests.csproj            (unchanged from T001)
    ScaffoldingSmokeTest.cs                    (deleted in step 3 below)
    Services/
      OrderRoutingResolverTest.cs
      OrderStatusCalculatorTest.cs
      OrderTotalCalculatorTest.cs
      OrderAcceptanceServiceTest.cs
      TicketStateMachineTest.cs
      PrintJobStateMachineTest.cs
      RetryPolicyTest.cs
      GiveUpWindowCalculatorTest.cs
      TicketAcknowledgePolicyTest.cs
```

Every fixture is named `<ClassUnderTest>Test`, one fixture per production class, one file per fixture,
per `backend/CLAUDE.md`. Method names are `MethodName_State_Expected`.

## 3. Constraints restated

These bind every step below without exception.

* **No code comments.** Names and structure carry the meaning. The one allowed comment is a short
  non-obvious *why* the code cannot express; nothing in this task needs one.
* **No static methods or properties.** The framework metadata exception does not apply here; nothing in
  Core is framework code. Where a stateless pure function is wanted (for example a switch-based mapper),
  it is an instance method on a class with no fields, constructed by its caller or by DI later.
* **Records for every multi-value return.** No positional tuple is ever returned or read by position. A
  method that would naturally return more than one value returns a named `record` (see `Results/`
  above), read by property name.
* **TDD is mandatory, red first, every single step.** For every class listed below: write the test
  fixture (or the next test method in it) before the production class or method exists, run
  `dotnet test` and quote the actual failing output (compiler error counts as a red run when the type
  does not exist yet; once it compiles, a red run is an assertion failure), only then write or extend
  the production code, then run again and quote the green output. A step in this document that is
  followed without a quoted red run is not complete, regardless of whether the final code is correct.
* **No new NuGet packages.** NUnit, NUnit3TestAdapter, Microsoft.NET.Test.Sdk, and FakeItEasy are
  already referenced by `GastronomyApp.Core.Tests` per T001; nothing else is needed for this task, and
  nothing else may be added. If you believe a package is genuinely required, stop and report the gap.
* **`GastronomyApp.Core.csproj` stays exactly as T001 left it**, i.e. no explicit `TargetFramework`,
  `Nullable`, or `ImplicitUsings`, no `PackageReference` at all. Do not add one.
* **`dotnet_style_require_accessibility_modifiers = always:error`** from the root `.editorconfig` means
  every member, including on `record` types, states its accessibility explicitly.
* **Exhaustive switches, total, no swallowing default.** Every `switch` expression over one of this
  task's enums must cover every declared case explicitly. Where the compiler cannot itself prove
  exhaustiveness (a `switch` statement, or a `switch` expression assigned where a silent widening could
  hide a missing arm), the discipline is: no `default` arm that returns a fallback value or does
  nothing; instead a `default` arm that calls `Never.OfType<T>(value)` (signature below), which throws.
  This makes an unhandled new enum member a runtime failure with a message naming the offending value
  the first time it is hit, and, combined with the enum-exhaustiveness tests required in section 5,
  a build-time failure of `dotnet test` the moment a new case is added without updating the switch. A
  `switch` *expression* whose arms already cover every enum member needs no `default` arm at all: the
  compiler already refuses to compile it non-exhaustively, and adding a swallowing `default` there would
  defeat that protection, so do not add one.

## 4. Enums, entities, and results (drive these test-first via the services that use them)

Enums and entities have no behaviour of their own, so they are not driven by a red test of their own
existence; they are written as the exact shape needed by the first service test that references them
(the routing resolver test, then the status calculator test, then acceptance, then the state machines,
in the order given in section 5). Declare the full shape the first time a type is touched, matching the
tables below exactly, rather than growing it field by field across services; the fields are known now
and partial types create silent gaps other tasks would build against. This section is the reference for
what each type must contain; section 5 is the ordered TDD steps.

### 4.1 Enums

```csharp
namespace GastronomyApp.Core.Enums;

public enum OrderStatus
{
    Accepted,
    Printing,
    Printed,
    NeedsAttention,
}

public enum LocationTicketStatus
{
    Queued,
    Blocked,
    Printing,
    Printed,
    PrintedOnTestPrinter,
    Unknown,
    Failed,
    HandledOnPaper,
}

public enum PrintJobStatus
{
    Queued,
    PreflightCheck,
    Blocked,
    Sending,
    AwaitingEcho,
    Confirmed,
    Unknown,
    Failed,
    ResolvedPrinted,
    ResolvedMissing,
}

public enum PrintJobKind
{
    Initial,
    Reprint,
    Test,
}

public enum PrintFailureReason
{
    PaperEnd,
    CoverOpen,
    Unreachable,
    Timeout,
    SocketDropped,
    PrinterError,
    StationDisabled,
    StationFaulty,
    TicketResolvedByHuman,
}

public enum PrintAttemptOutcome
{
    Confirmed,
    Blocked,
    Unreachable,
    SocketDropped,
    Timeout,
    PrinterError,
}

public enum PrintAttemptPhase
{
    Connecting,
    PreflightCheck,
    Sending,
    AwaitingEcho,
}

public enum TransportKind
{
    Network,
    Agent,
    Mock,
}

public enum NumberCounterKind
{
    GlobalOrder,
    LocationSequence,
    PrinterProcessId,
}
```

`PrintFailureReason.TicketResolvedByHuman` has no client-facing message key per spec 2.11; that is a
localization-layer fact and this task only needs the enum member to exist so `PrintJobStateMachine` can
produce it.

### 4.2 Entities

Every entity is a `sealed class` (mutable, EF-friendly shape; Infrastructure will map these directly),
not a `record`, because entities have identity, not value equality, and because a future EF Core mapping
needs settable properties with a parameterless constructor path. `record` is reserved for the
multi-value return types in `Results/` per the no-positional-tuples rule, which is a different concern
(structural equality on a computed answer) from entity identity. Every entity property is `public` with
a `get; set;` unless stated otherwise. `Guid` ids default to `Guid.NewGuid()` only for entities the
domain itself creates during acceptance (`Order`, `OrderLine`, `LocationTicket`); everything created via
admin endpoints in later tasks gets its id assigned by the caller, so no entity constructor generates an
id itself except where section 5 says the acceptance service must.

```csharp
namespace GastronomyApp.Core.Entities;

public sealed class EventSession
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public required bool IsPractice { get; set; }
    public required DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public required bool IsActive { get; set; }
}

public sealed class ProductionLocation
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public required string StationAccessKey { get; set; }
    public required string SlipLanguage { get; set; }
    public required int SortOrder { get; set; }
    public required bool IsActive { get; set; }
}

public sealed class CatalogItem
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public required string CategoryName { get; set; }
    public required int PriceCents { get; set; }
    public required int SortOrder { get; set; }
    public required bool IsActive { get; set; }
    public required bool IsAvailable { get; set; }
}

public sealed class ItemLocationAssignment
{
    public required Guid Id { get; set; }
    public required Guid CatalogItemId { get; set; }
    public required Guid ProductionLocationId { get; set; }
}

public sealed class TableSuggestion
{
    public required Guid Id { get; set; }
    public required string Label { get; set; }
    public required int SortOrder { get; set; }
}

public sealed class ServerPerson
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public required bool IsActive { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
}

public sealed class Device
{
    public required Guid Id { get; set; }
    public required Guid ServerPersonId { get; set; }
    public required string Language { get; set; }
    public required byte[] TokenHash { get; set; }
    public required byte[] TokenSalt { get; set; }
    public required int TokenIterations { get; set; }
    public required string TokenAlgorithm { get; set; }
    public required string TokenLookupId { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public required DateTime LastSeenAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public required string UserAgentSnapshot { get; set; }
}

public sealed class EnrolmentInvitation
{
    public required Guid Id { get; set; }
    public Guid? ServerPersonId { get; set; }
    public required byte[] QrCodeHash { get; set; }
    public required byte[] QrCodeSalt { get; set; }
    public required byte[] SixDigitHash { get; set; }
    public required byte[] SixDigitSalt { get; set; }
    public required int CodeIterations { get; set; }
    public required string CodeAlgorithm { get; set; }
    public required int FailedSixDigitAttempts { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public required DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public Guid? ConsumedByDeviceId { get; set; }
}

public sealed class Order
{
    public required Guid Id { get; set; }
    public required Guid EventSessionId { get; set; }
    public required Guid ClientOrderId { get; set; }
    public required int GlobalOrderNumber { get; set; }
    public required Guid ServerPersonId { get; set; }
    public required Guid DeviceId { get; set; }
    public required string TableLabel { get; set; }
    public string? Note { get; set; }
    public required int TotalCents { get; set; }
    public required OrderStatus Status { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public List<OrderLine> Lines { get; set; } = [];
    public List<LocationTicket> Tickets { get; set; } = [];
}

public sealed class OrderLine
{
    public required Guid Id { get; set; }
    public required Guid OrderId { get; set; }
    public required Guid LocationTicketId { get; set; }
    public required Guid CatalogItemId { get; set; }
    public Guid? ChosenProductionLocationId { get; set; }
    public required string ItemNameSnapshot { get; set; }
    public required int UnitPriceCentsSnapshot { get; set; }
    public required int Quantity { get; set; }
    public string? Note { get; set; }
}

public sealed class LocationTicket
{
    public required Guid Id { get; set; }
    public required Guid OrderId { get; set; }
    public required Guid ProductionLocationId { get; set; }
    public required int LocationSequenceNumber { get; set; }
    public required LocationTicketStatus Status { get; set; }
    public required int ReprintCount { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolutionNote { get; set; }
}

public sealed class PrintJob
{
    public required Guid Id { get; set; }
    public Guid? LocationTicketId { get; set; }
    public required Guid ProductionLocationId { get; set; }
    public required PrintJobKind Kind { get; set; }
    public required PrintJobStatus Status { get; set; }
    public int? ProcessId { get; set; }
    public PrintFailureReason? FailureReason { get; set; }
    public Guid? RequestedByDeviceId { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public sealed class PrintAttempt
{
    public required Guid Id { get; set; }
    public required Guid PrintJobId { get; set; }
    public required int AttemptNumber { get; set; }
    public required PrintAttemptOutcome Outcome { get; set; }
    public required PrintAttemptPhase Phase { get; set; }
    public required int BytesWritten { get; set; }
    public required string TransportDetail { get; set; }
    public required string PrinterStatusSnapshotJson { get; set; }
    public required DateTime StartedAtUtc { get; set; }
    public required DateTime EndedAtUtc { get; set; }
}

public sealed class NumberCounter
{
    public required NumberCounterKind CounterKind { get; set; }
    public Guid? EventSessionId { get; set; }
    public Guid? ProductionLocationId { get; set; }
    public string? PrinterEndpointKey { get; set; }
    public required int NextValue { get; set; }
}
```

`PrinterConfiguration` and `PrinterStatus` are out of scope for this task (see section 8); they belong
to the printing configuration slice, not the order-to-ticket vertical slice, and no service in this task
needs them as entities, only through `IPrinterStatusReader` (section 6).

## 5. Ordered steps

Work through these in order. Each step names the test file to write first, the red run to quote, the
production file(s) it drives, and the green run to quote. Do not start a later step before the earlier
one's tests are green; a service later in this list depends on an earlier one's public signature.

### Step 0: Preconditions

Confirm `dotnet build GastronomyApp.slnx` and `dotnet test GastronomyApp.slnx` both succeed with the
scaffolding from T001 before touching anything, and quote both outputs. If either fails, stop and report
the gap; T002 assumes a working T001.

### Step 1: `IClock`

Every time-dependent service in this task takes an injected clock rather than reading `DateTime.UtcNow`
directly, so tests control time exactly. Write it first because `GiveUpWindowCalculator` and
`OrderAcceptanceService` both need it from their first test.

`backend/GastronomyApp.Core/Ports/IClock.cs`:

```csharp
namespace GastronomyApp.Core.Ports;

public interface IClock
{
    DateTime UtcNow { get; }
}
```

No test fixture for this file: it is a single-property interface with no logic, faked with
`A.Fake<IClock>()` everywhere else. Proceed to step 2.

### Step 2: `OrderTotalCalculator`

Write `backend/GastronomyApp.Core.Tests/Services/OrderTotalCalculatorTest.cs` first, covering: a single
line, multiple lines, a zero-priced line (tap water), a large quantity (99), and that the calculated
total equals the sum of `Quantity * UnitPriceCentsSnapshot` across every line regardless of which
`LocationTicketId` each line belongs to (the total is order-wide, not per-ticket). Run `dotnet test
--filter FullyQualifiedName~OrderTotalCalculatorTest`, quote the compiler-error red run (the class does
not exist yet).

Then write `backend/GastronomyApp.Core/Services/OrderTotalCalculator.cs`:

```csharp
namespace GastronomyApp.Core.Services;

public sealed class OrderTotalCalculator
{
    public int CalculateTotalCents(IReadOnlyCollection<OrderLine> lines)
    {
    }
}
```

(`OrderLine` from `GastronomyApp.Core.Entities`, via `using`.) Re-run the filtered test, quote the green
output.

### Step 3: Delete the scaffolding smoke test

Delete `backend/GastronomyApp.Core.Tests/ScaffoldingSmokeTest.cs` now that a real fixture exists, per
T001's own instruction. Run `dotnet test GastronomyApp.slnx --filter
FullyQualifiedName~GastronomyApp.Core` and quote the output showing only real fixtures remain.

### Step 4: `OrderRoutingResolver`

Write `backend/GastronomyApp.Core.Tests/Services/OrderRoutingResolverTest.cs` first. Cover, per spec
2.6 and the 11.1 table: one active candidate routes with no chosen location and no station control
needed; more than one candidate with no `chosenProductionLocationId` supplied is rejected (returns a
failure result, does not throw); more than one candidate with a `chosenProductionLocationId` naming a
location not assigned to the item is rejected; more than one candidate with a valid, still-active chosen
location routes there and echoes it; a chosen location that is no longer active falls back to the lowest
`SortOrder` active candidate, and the result still records the original `ChosenProductionLocationId` the
server picked (per spec 2.6, the slip has to show the intended station); the candidate set is never
empty for an orderable item, asserted by constructing every test case from a fixture with at least one
active assignment (this resolver is never asked to handle a genuinely empty candidate set, because that
shape cannot occur by the invariants in spec 2.4/2.5; do not write a defensive "no candidates" branch
that swallows the impossible case, throw via `Never`-style exhaustiveness is not applicable here since
this is not a switch, so instead let an empty candidate set be a documented precondition violation:
throw `InvalidOperationException` naming the item, and cover that with one test as an explicit
programmer-error case, not a normal result).

Quote the red compiler-error run, then write:

`backend/GastronomyApp.Core/Results/RoutingDecision.cs`:

```csharp
namespace GastronomyApp.Core.Results;

public sealed record RoutingDecision
{
    public required Guid ResolvedProductionLocationId { get; init; }
    public required Guid? ChosenProductionLocationId { get; init; }
    public required bool FellBackFromStaleChoice { get; init; }
}

public sealed record RoutingFailure
{
    public required RoutingFailureReason Reason { get; init; }
}

public enum RoutingFailureReason
{
    StationRequired,
    StationNotAssignedToItem,
}
```

`backend/GastronomyApp.Core/Services/OrderRoutingResolver.cs`:

```csharp
namespace GastronomyApp.Core.Services;

public sealed class OrderRoutingResolver
{
    public Result<RoutingDecision, RoutingFailure> Resolve(
        Guid catalogItemId,
        IReadOnlyCollection<ItemLocationAssignment> assignments,
        IReadOnlyCollection<ProductionLocation> activeLocations,
        Guid? chosenProductionLocationId)
    {
    }
}
```

This introduces the shared `Result<TValue, TFailure>` shape used by every validating service in this
task (routing, acceptance). Define it once, in `backend/GastronomyApp.Core/Results/Result.cs`, before
writing `OrderRoutingResolver`'s body:

```csharp
namespace GastronomyApp.Core.Results;

public sealed class Result<TValue, TFailure>
{
    private Result(bool isSuccess, TValue? value, TFailure? failure)
    {
    }

    public bool IsSuccess { get; }

    public TValue Value { get; }

    public TFailure Failure { get; }

    public static Result<TValue, TFailure> Success(TValue value)
    {
    }

    public static Result<TValue, TFailure> Failed(TFailure failure)
    {
    }
}
```

`Value` throws `InvalidOperationException` when read on a failed result and `Failure` throws when read
on a successful one; cover both accessors with tests in `ResultTest.cs`
(`backend/GastronomyApp.Core.Tests/Results/ResultTest.cs`), written before this class, following the
same red-first rule. This type is used by every later step that needs a validated-or-rejected outcome,
so its own tests come first, ahead of `OrderRoutingResolverTest.cs` in execution order even though it is
introduced inside this step: write `ResultTest.cs`, red, green, then `OrderRoutingResolverTest.cs`, red,
green.

Re-run the filtered test for `OrderRoutingResolverTest`, quote the green output.

### Step 5: `OrderStatusCalculator`

Write `backend/GastronomyApp.Core.Tests/Services/OrderStatusCalculatorTest.cs` first. This is the
exhaustive test 11.1 names: cover the five-row priority table in spec 3.1 for every combination of
ticket states across a small order (use orders of 1, 2, and 3 tickets so the row-1-through-row-5
interactions are actually exercised, not just single-ticket orders), specifically proving:

* Any ticket `Unknown`, `Failed`, or `Blocked` (mixed with any other states) yields `NeedsAttention`.
* Any ticket `PrintedOnTestPrinter` on a non-practice order (with every other ticket in a state that
  would otherwise satisfy row 3) yields `NeedsAttention`; the same combination on a practice order does
  not hit row 2 and falls through to row 3 if the rest qualify.
* Every ticket in `{Printed, HandledOnPaper, PrintedOnTestPrinter}` (practice order for the
  `PrintedOnTestPrinter` member) yields `Printed`.
* Any ticket `Printing` with none of rows 1 or 2 matching yields `Printing`.
* Otherwise (at least one `Queued`, none matching rows 1, 2, or 4) yields `Accepted`.
* A generative/combinatorial test over every `LocationTicketStatus` combination for orders of size 1 and
  2 (all 8^1 and 8^2 combinations, both practice and non-practice for size 1) that asserts the result is
  never anything other than one of the four `OrderStatus` values and always matches the first matching
  row when the rows are evaluated by hand for that combination, so "no combination falls through" is
  actually machine-checked rather than asserted by inspection.

Quote the red run, then write:

`backend/GastronomyApp.Core/Services/OrderStatusCalculator.cs`:

```csharp
namespace GastronomyApp.Core.Services;

public sealed class OrderStatusCalculator
{
    public OrderStatus Calculate(IReadOnlyCollection<LocationTicketStatus> ticketStatuses, bool isPracticeSession)
    {
    }
}
```

Takes ticket statuses rather than `LocationTicket` entities because the calculator has no use for the
rest of the ticket, and a caller building the projection from a query result should not have to
materialize full entities just to call this. Re-run, quote green.

### Step 6: `OrderAcceptanceService` and its ports

This is the use case for spec 5.4 / 9.3 / 4.1: validate, resolve idempotency, allocate numbers, route,
create tickets. Because numbering allocation "happens inside the acceptance transaction" per the brief,
this service does not open a transaction itself (that is Infrastructure's job, wrapping this service's
repository calls in a database transaction later); instead its ports are shaped so that every write the
use case needs (idempotency check, number allocation, ticket/order persistence) happens through calls
this service makes in one pass, so an EF implementation of the ports can wrap the whole call in one
`BEGIN IMMEDIATE` and every allocation therefore participates in it. This section defines the ports
first, because the service constructor takes them.

`backend/GastronomyApp.Core/Ports/ICatalogItemRepository.cs`:

```csharp
namespace GastronomyApp.Core.Ports;

public interface ICatalogItemRepository
{
    Task<CatalogItem?> FindByIdAsync(Guid catalogItemId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ItemLocationAssignment>> FindAssignmentsAsync(Guid catalogItemId, CancellationToken cancellationToken);
}
```

`backend/GastronomyApp.Core/Ports/IProductionLocationRepository.cs`:

```csharp
namespace GastronomyApp.Core.Ports;

public interface IProductionLocationRepository
{
    Task<IReadOnlyCollection<ProductionLocation>> FindActiveAsync(CancellationToken cancellationToken);
}
```

`backend/GastronomyApp.Core/Ports/INumberAllocator.cs`:

```csharp
namespace GastronomyApp.Core.Ports;

public interface INumberAllocator
{
    Task<int> AllocateGlobalOrderNumberAsync(Guid eventSessionId, CancellationToken cancellationToken);

    Task<int> AllocateLocationSequenceNumberAsync(Guid eventSessionId, Guid productionLocationId, CancellationToken cancellationToken);
}
```

This is the port shape that makes allocation-inside-the-acceptance-transaction possible: the
Infrastructure implementation opens the `BEGIN IMMEDIATE` around the whole `OrderAcceptanceService` call
(the caller in `GastronomyApp.Api` owns transaction scoping, injecting an `INumberAllocator` and
`IOrderRepository` implementation that share the same open `DbContext`/transaction), so every allocation
this service triggers commits or rolls back atomically with the order insert. This task does not
implement that; it only has to shape the port so it is possible, which passing `CancellationToken`
through and keeping allocation as separate awaited calls (rather than a single "allocate everything"
call that would hide the per-ticket loop from the transaction) achieves.

`backend/GastronomyApp.Core/Ports/IOrderRepository.cs`:

```csharp
namespace GastronomyApp.Core.Ports;

public interface IOrderRepository
{
    Task<Order?> FindByClientOrderIdAsync(Guid clientOrderId, CancellationToken cancellationToken);

    Task AddAsync(Order order, CancellationToken cancellationToken);
}
```

Idempotency contract for this method pair, binding for every implementation: the use case looks the
order up by `ClientOrderId` first; if found, it returns that existing order and calls `AddAsync` for
nothing. The uniqueness that makes a *concurrent* duplicate submission safe is a database unique index
on `ClientOrderId`, which is Infrastructure's job (T001's `Directory.Packages.props` already carries EF
Core); this port and this use case define the *behavioural* contract (same id in, same order out, no
second order created) that the unique index enforces at the storage layer. `AddAsync` in a real
implementation is expected to throw a storage-specific "unique constraint violated" exception on a race
that `FindByClientOrderIdAsync` did not catch in time; catching that and re-resolving to the winning row
is Infrastructure's responsibility, not this service's, and is out of scope here (see section 8).

Results and requests for the use case:

`backend/GastronomyApp.Core/Results/OrderAcceptanceResult.cs`:

```csharp
namespace GastronomyApp.Core.Results;

public sealed record OrderAcceptanceResult
{
    public required Order Order { get; init; }
    public required bool WasAlreadyAccepted { get; init; }
}

public sealed record OrderValidationFailure
{
    public required OrderValidationFailureReason Reason { get; init; }
    public Guid? OffendingCatalogItemId { get; init; }
}

public enum OrderValidationFailureReason
{
    NoLines,
    QuantityOutOfRange,
    TableLabelMissing,
    TableLabelTooLong,
    UnknownCatalogItemId,
    StationRequired,
    StationNotAssignedToItem,
}
```

`OrderValidationFailureReason` maps 1:1 to spec 5.4's 400/422 split (`NoLines`, `QuantityOutOfRange`,
`TableLabelMissing`, `TableLabelTooLong` are the 400 cases; `UnknownCatalogItemId`,
`StationRequired`, `StationNotAssignedToItem` are the 422 cases). This service returns the reason as a
domain value; mapping a reason to an HTTP status code and a message key is the API layer's job in a
later task, per the brief's "422 semantics expressed as domain results" instruction and per this
document's out-of-scope list (no message keys, no HTTP, in this task).

`backend/GastronomyApp.Core/Services/OrderAcceptanceService.cs` request shape (a nested record, not a
positional tuple, not a bag of primitive parameters, because the request has more than one value and
more than one call site will construct it: the API layer building it from a deserialized request body,
and tests building it directly):

```csharp
namespace GastronomyApp.Core.Services;

public sealed record OrderAcceptanceLineRequest
{
    public required Guid CatalogItemId { get; init; }
    public required int Quantity { get; init; }
    public string? Note { get; init; }
    public Guid? ProductionLocationId { get; init; }
}

public sealed record OrderAcceptanceRequest
{
    public required Guid ClientOrderId { get; init; }
    public required Guid EventSessionId { get; init; }
    public required Guid ServerPersonId { get; init; }
    public required Guid DeviceId { get; init; }
    public required string TableLabel { get; init; }
    public string? Note { get; init; }
    public required IReadOnlyList<OrderAcceptanceLineRequest> Lines { get; init; }
}

public sealed class OrderAcceptanceService
{
    public OrderAcceptanceService(
        IOrderRepository orderRepository,
        ICatalogItemRepository catalogItemRepository,
        IProductionLocationRepository productionLocationRepository,
        INumberAllocator numberAllocator,
        OrderRoutingResolver routingResolver,
        OrderTotalCalculator totalCalculator,
        IClock clock)
    {
    }

    public async Task<Result<OrderAcceptanceResult, OrderValidationFailure>> AcceptAsync(
        OrderAcceptanceRequest request,
        CancellationToken cancellationToken)
    {
    }
}
```

Note `expectedTotalCents` is deliberately absent from `OrderAcceptanceRequest` and from every other type
this service touches: per spec 5.4 it is a comparison value the API layer reads once, compares against
`OrderAcceptanceResult.Order.TotalCents`, and echoes back in the HTTP response; it is never stored, never
influences acceptance, and is never passed into Core. Do not add a parameter for it anywhere in this
service. A test asserting that no such parameter exists is not meaningful in C# (the compiler already
enforces it once the signature above is implemented as written), so this is enforced by code review of
the signature rather than a test; note this explicitly if asked, do not invent a parameter "for
completeness".

Write `backend/GastronomyApp.Core.Tests/Services/OrderAcceptanceServiceTest.cs` first, with
`A.Fake<IOrderRepository>()`, `A.Fake<ICatalogItemRepository>()`,
`A.Fake<IProductionLocationRepository>()`, `A.Fake<INumberAllocator>()`, a real
`OrderRoutingResolver`, a real `OrderTotalCalculator`, and `A.Fake<IClock>()`. Cover, one test method per
bullet:

* Empty `Lines` returns `Failed` with `NoLines`, allocates nothing (assert `numberAllocator` was never
  called), and does not call `orderRepository.AddAsync`.
* A quantity of 0 or 100 returns `Failed` with `QuantityOutOfRange`; 1 and 99 are accepted by this check
  (combine with a minimal valid order in each case).
* A missing, empty, or over-40-character `TableLabel` returns `Failed` with the matching reason; a
  41-character label fails, a 40-character label does not fail this check.
* An unknown `CatalogItemId` (repository returns null) returns `Failed` with `UnknownCatalogItemId`
  naming the offending id in `OffendingCatalogItemId`, and validation stops at the first unknown item
  found (assert only the failing item, not every item, was looked up, to prove short-circuiting, or
  document and test whichever choice you implement, but the choice must be deliberate and tested, not
  incidental).
* A line whose item has more than one active candidate and no `ProductionLocationId` returns `Failed`
  with `StationRequired`.
* A line naming a `ProductionLocationId` not assigned to that item returns `Failed` with
  `StationNotAssignedToItem`.
* A sold-out item (`IsAvailable == false`) is still accepted (spec 5.4: sold out is never a rejection
  reason). Use a fake `ICatalogItemRepository` returning a `CatalogItem` with `IsAvailable = false` and
  assert the call succeeds.
* A deactivated item passed by id still resolves through `FindByIdAsync` in this service's model (the
  service trusts what the repository returns; a deactivated item simply is not returned by a real
  catalog fetch upstream on the phone, but if the repository *is* asked for one, per spec 5.4 "only an
  item id that does not exist at all is a 422", it must still be accepted) - write this as an explicit
  test with `IsActive = false` on the returned `CatalogItem`, asserting success, so the distinction from
  `UnknownCatalogItemId` (repository returns null) is pinned down.
* A first-time `ClientOrderId` (repository returns null from `FindByClientOrderIdAsync`) with a valid
  request calls `numberAllocator.AllocateGlobalOrderNumberAsync` once, calls
  `AllocateLocationSequenceNumberAsync` once per distinct resolved production location (not once per
  line: two lines routed to the same location share one ticket and one sequence number), calls
  `orderRepository.AddAsync` exactly once with an `Order` whose `Lines.Count` matches the request,
  `Tickets.Count` matches the distinct location count, `TotalCents` equals the recomputed total from
  `OrderTotalCalculator`, `Status == OrderStatus.Accepted`, and returns `Success` with
  `WasAlreadyAccepted == false`.
* A `ClientOrderId` already known (repository returns an existing `Order` from
  `FindByClientOrderIdAsync`) returns `Success` with that same `Order` instance,
  `WasAlreadyAccepted == true`, and never calls `numberAllocator` or `orderRepository.AddAsync` (spec
  5.4's 200 case: no second order, no second numbers).
* Two lines for two different catalog items routed to the same single-candidate location produce one
  `LocationTicket` holding both lines, with each `OrderLine.LocationTicketId` pointing at it.
* Two lines routed to two different locations produce two tickets, each with its own allocated
  `LocationSequenceNumber`, and the order's `TotalCents` sums across both.
* A line whose chosen location fell back per `OrderRoutingResolver` (stale choice) still creates its
  ticket at the fallback location, and the resulting `OrderLine.ChosenProductionLocationId` still holds
  the server's original choice, not the fallback (spec 2.6: the slip shows the intended station).
* `Order.Id`, every `OrderLine.Id`, and every `LocationTicket.Id` are non-empty `Guid`s and distinct from
  each other across the whole created graph (this service is the one place in this task allowed to call
  `Guid.NewGuid()`, per section 4.2).
* `Order.CreatedAtUtc` and every `LocationTicket.CreatedAtUtc` equal the fake `IClock.UtcNow` value set
  up for that test.
* Every `LocationTicket.Status` starts `Queued` and every `LocationTicket.ReprintCount` starts 0.
* `ItemNameSnapshot` and `UnitPriceCentsSnapshot` on each created line equal the `CatalogItem.Name` and
  `PriceCents` the fake repository returned for that line at the moment of acceptance, proving the
  snapshot is taken from the current catalog row rather than copied from the request.

Quote the red run (most of these will be compiler errors on the first pass since none of the production
types exist), then implement `Result<TValue, TFailure>` dependencies already exist from step 4, so write
`OrderAcceptanceService.cs` and its request/result records as specified above. Re-run filtered to
`OrderAcceptanceServiceTest`, quote green.

### Step 7: `TicketStateMachine`

Write `backend/GastronomyApp.Core.Tests/Services/TicketStateMachineTest.cs` first. Enumerate every
transition drawn in spec 3.2's diagram as an allowed case (one test method per arrow, or a
`TestCaseSource` table covering all of them, either is acceptable as long as every arrow is a distinct
assertable case) and, separately, an exhaustive test proving every `(from, to)` pair over all 8x8
`LocationTicketStatus` combinations that is *not* one of the drawn arrows is refused (returns `false` /
a rejection, does not throw), per 11.1's "every transition not listed is refused". Represent a
transition as a value the state machine can check without side effects, since the actual cause data
(give-up window expiry, human answer, etc.) is decided by other services (`GiveUpWindowCalculator`,
`RetryPolicy`) that this task also builds; this class's job is purely "is `from -> to` a legal edge in
the diagram", which the acceptance/print services above and the printing worker in a later task consult
before writing a new status.

Quote red, then write:

`backend/GastronomyApp.Core/Services/TicketStateMachine.cs`:

```csharp
namespace GastronomyApp.Core.Services;

public sealed class TicketStateMachine
{
    public bool CanTransition(LocationTicketStatus from, LocationTicketStatus to)
    {
    }
}
```

Implement `CanTransition` as a `switch` expression on `(from, to)` matching exactly the edges drawn in
spec 3.2 (fifteen edges, counting `Failed -> Queued`, `Printed -> Queued`, and
`PrintedOnTestPrinter -> Queued` as three of them, and the terminal `[*]` arrows out of `Printed` and
`HandledOnPaper` as meaning no outgoing edges from those states rather than a case in this switch), with
a final `_ => false` arm (this one case is the deliberate exception to "no swallowing default": the
method's whole contract is "true for a listed edge, false otherwise", so `false` is not a swallowed
value but the specified answer; do not use `Never` here). Re-run, quote green.

### Step 8: `PrintJobStateMachine`

Write `backend/GastronomyApp.Core.Tests/Services/PrintJobStateMachineTest.cs` first, same shape as step
7 but over spec 3.3's diagram and `PrintJobStatus`. Additionally and specifically (11.1's callout): a
parameterized test proving that for every `PrintAttemptOutcome` with `BytesWritten > 0`, the resulting
job status per the mapping in step 9 (`RetryPolicy`) is never one this state machine calls
`CanTransition` back into `Queued` or `PreflightCheck` for, i.e. bytes-written outcomes never reach a
state this machine allows to transition back to a retryable state directly; write this as a test that
composes `PrintJobStateMachine.CanTransition` with the outcome-to-status mapping table from step 9 (you
may need to write step 9's `RetryPolicy` type shape first to compile this test, in which case do so, but
still keep `RetryPolicyTest.cs` as its own red-then-green step 9 before relying on its behaviour here;
this cross-check test belongs in `PrintJobStateMachineTest.cs` because it is a property of the state
machine, not of the policy).

Quote red, then write:

`backend/GastronomyApp.Core/Services/PrintJobStateMachine.cs`:

```csharp
namespace GastronomyApp.Core.Services;

public sealed class PrintJobStateMachine
{
    public bool CanTransition(PrintJobStatus from, PrintJobStatus to)
    {
    }
}
```

Same `switch` expression shape as `TicketStateMachine`, edges taken from spec 3.3's diagram (fourteen
edges including the two separately-drawn `SocketDropped` arrows from `Sending`, which are the same
`(Sending, Queued)` and `(Sending, Unknown)` pairs as any other cause reaching those states, since this
machine is state-to-state and `PrintAttempt.Phase` is what distinguishes the wire event, not this
switch). Re-run, quote green.

### Step 9: `RetryPolicy`

Write `backend/GastronomyApp.Core.Tests/Services/RetryPolicyTest.cs` first. One test method per row of
spec 7.6's table (nine rows, both `Confirmed` rows, both `SocketDropped` rows, both `Timeout` rows, as
11.1 requires), each asserting the full `PrintOutcomeMapping` returned: `JobStatus`, `TicketStatus`,
`ShouldRetryAutomatically`, and (only for the two `Confirmed` rows) that `TransportKind.Mock` maps
`TicketStatus` to `PrintedOnTestPrinter` while `TransportKind.Network` and `TransportKind.Agent` both
map it to `Printed`. Also test that `PrintFailureReason` is populated correctly for every row that
reaches `Blocked` or `Failed`-bound status (`Blocked` outcome to `PrintFailureReason.PrinterError` is
wrong; re-read spec 7.6: `Blocked` outcome maps to job/ticket `Blocked` with no failure reason yet, the
`FailureReason` is assigned later when the give-up window or outer bound actually fails the ticket, so
`PrintOutcomeMapping` for a `Blocked`-status row carries `FailureReason: null` and only a row whose
`ShouldRetryAutomatically` is false and whose resulting status is terminal for this attempt carries a
reason; get this right against the table, not against the summary prose in 3.5, since 7.6 states it
binds when they disagree).

Quote red, then write:

`backend/GastronomyApp.Core/Results/PrintOutcomeMapping.cs`:

```csharp
namespace GastronomyApp.Core.Results;

public sealed record PrintOutcomeMapping
{
    public required PrintJobStatus JobStatus { get; init; }
    public required LocationTicketStatus TicketStatus { get; init; }
    public required bool ShouldRetryAutomatically { get; init; }
}
```

`backend/GastronomyApp.Core/Services/RetryPolicy.cs`:

```csharp
namespace GastronomyApp.Core.Services;

public sealed class RetryPolicy
{
    public PrintOutcomeMapping Map(PrintAttemptOutcome outcome, TransportKind transportKind, int bytesWritten)
    {
    }
}
```

Implement `Map` as a `switch` expression over `(outcome, bytesWritten > 0, transportKind)` (or an
equivalent total decomposition), covering every row of the 7.6 table exactly, with no `default` arm
possible to write since every `(PrintAttemptOutcome, bool, TransportKind)` combination that the table
does not explicitly give (for example `Confirmed` with zero bytes, which cannot occur under the
hardware's own contract) still needs a decision: use `Never.OfType<PrintOutcomeMapping>(outcome)` for
combinations the table declares impossible, and cover that with a test asserting it throws for
`(Confirmed, bytesWritten: 0, any transport)`, since the spec never describes a confirmed job with zero
bytes and inventing a silent answer for it would be worse than a loud one. Re-run, quote green.

### Step 10: `Never`

`RetryPolicy` above already needs this; write it now if not written already, with its own tiny
red-first test (`NeverTest.cs`) asserting it throws `InvalidOperationException` naming the unhandled
value's `ToString()` in the message.

`backend/GastronomyApp.Core/Services/Never.cs`:

```csharp
namespace GastronomyApp.Core.Services;

public sealed class Never
{
    public TResult OfType<TResult>(object unexpectedValue)
    {
    }
}
```

Every call site (`Never.OfType<Whatever>(value)`, called through an instance since statics are
forbidden) throws `InvalidOperationException($"Unhandled value: {unexpectedValue}")`. Every other
service in this task that needs an unreachable-default arm in a `switch` statement (as opposed to a
`switch` expression already proven exhaustive by the compiler) constructs a `Never` instance for that
purpose; since `Never` has no fields, callers may construct a fresh one at the call site
(`new Never().OfType<...>(...)`) rather than needing it injected.

### Step 11: `GiveUpWindowCalculator`

Write `backend/GastronomyApp.Core.Tests/Services/GiveUpWindowCalculatorTest.cs` first. Cover, per 11.1's
`GiveUpWindow` row and spec 3.2's worked example exactly (reuse its numbers: paper out at minute 0,
roll in at minute 18, unreachable from then, outer bound at minute 20 with the give-up stopwatch at 3
unsuspended minutes accumulated, never reaching 5):

* A ticket with no suspension periods and no attempts reaches `Failed` (give-up) exactly at
  `CreatedAtUtc + 5 minutes`, and is not yet `Failed` a moment before that.
* A ticket whose entire life so far was inside one suspension period (paper out from minute 0, still
  out at "now") has zero accumulated unsuspended time and is not given-up regardless of wall-clock
  elapsed time, but the outer bound (20 minutes, unsuspended by anything, per 3.2) still fires at minute
  20 even while still inside that suspension.
* The exact worked example: suspended minutes 0-18, unsuspended (unreachable, not a suspending cause)
  minutes 18 onward; at minute 20 the outer bound has fired (the ticket is `Failed`) with only 2 minutes
  of unsuspended time accumulated, and the give-up window (which would have needed 5 unsuspended
  minutes, reached at minute 23 of wall clock) never gets the chance to fire on its own.
* Suspension resumes without resetting: two separate suspension periods (for example paper out minutes
  0-2, and again minutes 10-12) with unsuspended time in between and after are summed, not reset to zero
  by the second suspension starting.
* Each of the four suspending conditions from spec 3.2's table (`IsPaperEnd`, `IsCoverOpen`,
  `PrinterConfiguration.IsEnabled == false`, and the mock's `IsInErrorState`) independently suspends the
  clock; test each as its own case using the input shape below.
* A `Blocked` ticket whose cause is `PrinterError` (not one of the four suspending conditions, per
  3.2's explicit callout that a mechanical error is not suspending) is not suspended and reaches
  give-up at 5 unsuspended minutes exactly like an ordinary unreachable ticket.
* The outer bound (20 minutes) fires under every one of the above causes, including a ticket that was
  suspended for its whole life.
* The outer bound never fires while the ticket's current status is `Printing` (per 3.2's explicit
  exception): a ticket 25 minutes old but currently `Printing` is not given up by this calculator; the
  caller (the printing worker, a later task) is what bounds `Printing` via `JobTimeoutSeconds`, not this
  calculator.

Quote red, then design the input/output shape and implement. Because "accumulated unsuspended time" is
inherently a history (a ticket's status and its suspending-condition state both change over its life,
possibly several times), this calculator does not take a single snapshot; it takes an ordered list of
the periods during which the ticket's clock was suspended, which the caller (a later printing-worker
task, reading `PrinterStatus` history and `PrinterConfiguration.IsEnabled` history) is responsible for
reconstructing. This task defines the shape:

`backend/GastronomyApp.Core/Results/GiveUpWindowEvaluation.cs`:

```csharp
namespace GastronomyApp.Core.Results;

public sealed record SuspensionPeriod
{
    public required DateTime StartedAtUtc { get; init; }
    public DateTime? EndedAtUtc { get; init; }
}

public sealed record GiveUpWindowEvaluation
{
    public required bool HasReachedGiveUpWindow { get; init; }
    public required bool HasReachedOuterBound { get; init; }
    public required TimeSpan AccumulatedUnsuspendedTime { get; init; }
}
```

`SuspensionPeriod.EndedAtUtc` is null for a suspension still in force at the moment being evaluated.

`backend/GastronomyApp.Core/Services/GiveUpWindowCalculator.cs`:

```csharp
namespace GastronomyApp.Core.Services;

public sealed class GiveUpWindowCalculator
{
    public static readonly TimeSpan GiveUpWindow = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan OuterBound = TimeSpan.FromMinutes(20);

    public GiveUpWindowEvaluation Evaluate(
        DateTime ticketCreatedAtUtc,
        DateTime evaluatedAtUtc,
        LocationTicketStatus currentStatus,
        IReadOnlyCollection<SuspensionPeriod> suspensionPeriods)
    {
    }
}
```

`public static readonly` fields are data, not methods or properties, so they do not fall under the
no-statics rule (which names methods and properties); they are the two named constants from spec 3.2
and belong on the class that owns the calculation rather than as magic numbers duplicated at every call
site. If a stricter reading is wanted instead, ask before implementing; this document's default
position is that the no-statics rule targets behaviour, not named constants, and a `public static
readonly TimeSpan` is the idiomatic C# constant shape here since `const` cannot hold a `TimeSpan`.

`Evaluate` computes accumulated unsuspended time as `(evaluatedAtUtc - ticketCreatedAtUtc)` minus the
sum of every suspension period's duration (clamping each period's effective end to `evaluatedAtUtc` when
`EndedAtUtc` is null or later than `evaluatedAtUtc`, and clamping each period's effective start to no
earlier than `ticketCreatedAtUtc`), sets `HasReachedGiveUpWindow` when that accumulated time is at least
`GiveUpWindow`, sets `HasReachedOuterBound` when `currentStatus != LocationTicketStatus.Printing` and
`evaluatedAtUtc - ticketCreatedAtUtc >= OuterBound`. Re-run, quote green.

### Step 12: `TicketAcknowledgePolicy`

Write `backend/GastronomyApp.Core.Tests/Services/TicketAcknowledgePolicyTest.cs` first. Cover the exact
boolean expression from spec 5.6, one case per clause per 11.1: each `LocationTicketStatus` in
`{Failed, Unknown, Blocked}` always allows it regardless of station condition; each of the six printer
conditions (`IsFaulty`, `!IsOnline`, `IsPaperEnd`, `IsCoverOpen`, `IsInErrorState`,
`!PrinterConfiguration.IsEnabled` represented here as a single `IsEnabled` flag on the input, see below)
independently allows it for a ticket in `Queued`; a `Queued` ticket at a fully healthy station is
refused; a `Printing` ticket is refused under every one of the six conditions in turn, and refused when
healthy too (11.1: "a `Printing` ticket refused under every one of the six printer conditions in turn").

Quote red, then write:

`backend/GastronomyApp.Core/Results/TicketAcknowledgeDecision.cs`:

```csharp
namespace GastronomyApp.Core.Results;

public sealed record StationPrintability
{
    public required bool IsFaulty { get; init; }
    public required bool IsOnline { get; init; }
    public required bool IsPaperEnd { get; init; }
    public required bool IsCoverOpen { get; init; }
    public required bool IsInErrorState { get; init; }
    public required bool IsEnabled { get; init; }
}
```

`backend/GastronomyApp.Core/Services/TicketAcknowledgePolicy.cs`:

```csharp
namespace GastronomyApp.Core.Services;

public sealed class TicketAcknowledgePolicy
{
    public bool CanAcknowledge(LocationTicketStatus ticketStatus, StationPrintability station)
    {
    }
}
```

Implement exactly the expression from spec 5.6: `ticketStatus is Failed or Unknown or Blocked`, or
(`stationCannotPrintRightNow && ticketStatus is not Printing`), where
`stationCannotPrintRightNow = station.IsFaulty || !station.IsOnline || station.IsPaperEnd ||
station.IsCoverOpen || station.IsInErrorState || !station.IsEnabled`. Re-run, quote green.

### Step 13: `IPrinterStatusReader`

This is the printer-status/dispatch port surface Core needs, kept to exactly what
`TicketAcknowledgePolicy`'s callers and a later printing-worker task need to reason about outcomes, per
the brief's explicit boundary ("ONLY as far as Core needs to reason about outcomes; the transports live
elsewhere"). No test fixture for this file, same reasoning as `IClock`: it is a read port with no logic
of its own to prove here.

`backend/GastronomyApp.Core/Ports/IPrinterStatusReader.cs`:

```csharp
namespace GastronomyApp.Core.Ports;

public interface IPrinterStatusReader
{
    Task<StationPrintability?> GetCurrentAsync(Guid productionLocationId, CancellationToken cancellationToken);
}
```

Returns null when no status has ever been recorded for that location (a station never yet connected).
`StationPrintability` is the same record from step 12, reused rather than duplicated, because it is
already exactly the shape a caller needs to feed `TicketAcknowledgePolicy.CanAcknowledge`; this is the
single-source-of-truth rule from `CLAUDE.md` rule 6 applied to this port's return type.

### Step 14: Full suite, zero warnings

Run `dotnet build GastronomyApp.slnx` and quote `0 Warning(s)`, `0 Error(s)`. Run `dotnet test
GastronomyApp.slnx --filter FullyQualifiedName~GastronomyApp.Core` and quote every fixture passing with
a final summary line. This is the task's verification bar; do not consider the task complete without
both quoted outputs from an actual run.

## 6. Interfaces this task defines (summary, exact names)

For the consistency review across the six parallel tasks: every Core-owned public interface, and the
concrete public service/entity/result types other tasks will construct or consume, live in these exact
namespaces and have these exact names.

Ports (`GastronomyApp.Core.Ports`): `IClock`, `ICatalogItemRepository`, `IProductionLocationRepository`,
`IOrderRepository`, `INumberAllocator`, `IPrinterStatusReader`.

Services (`GastronomyApp.Core.Services`, all `sealed class`, all constructor-injected, none static):
`OrderRoutingResolver`, `OrderStatusCalculator`, `OrderTotalCalculator`, `OrderAcceptanceService`,
`TicketStateMachine`, `PrintJobStateMachine`, `RetryPolicy`, `GiveUpWindowCalculator`,
`TicketAcknowledgePolicy`, `Never`.

Entities (`GastronomyApp.Core.Entities`, all `sealed class`): `EventSession`, `ProductionLocation`,
`CatalogItem`, `ItemLocationAssignment`, `TableSuggestion`, `ServerPerson`, `Device`,
`EnrolmentInvitation`, `Order`, `OrderLine`, `LocationTicket`, `PrintJob`, `PrintAttempt`,
`NumberCounter`.

Enums (`GastronomyApp.Core.Enums`): `OrderStatus`, `LocationTicketStatus`, `PrintJobStatus`,
`PrintJobKind`, `PrintFailureReason`, `PrintAttemptOutcome`, `PrintAttemptPhase`, `TransportKind`,
`NumberCounterKind`.

Results (`GastronomyApp.Core.Results`, all `sealed record`): `Result<TValue, TFailure>`,
`RoutingDecision`, `RoutingFailure` (+ `RoutingFailureReason`), `OrderAcceptanceResult`,
`OrderValidationFailure` (+ `OrderValidationFailureReason`), `OrderAcceptanceLineRequest`,
`OrderAcceptanceRequest`, `PrintOutcomeMapping`, `SuspensionPeriod`, `GiveUpWindowEvaluation`,
`StationPrintability`.

Any task that needs to construct an `OrderAcceptanceRequest`, call `OrderAcceptanceService.AcceptAsync`,
implement one of the five repository/allocator/status ports, or consult
`TicketAcknowledgePolicy.CanAcknowledge` should use these exact names and shapes; do not introduce a
parallel or renamed version of any of them.

## 7. Verification

```powershell
dotnet build GastronomyApp.slnx
dotnet test GastronomyApp.slnx --filter FullyQualifiedName~GastronomyApp.Core
```

Success looks like: build with `0 Warning(s)` and `0 Error(s)` across every project including Core and
Core.Tests, and the filtered test run reporting one passed test per test method described in section 5
(no skipped, no failed), with a final summary line. Quote both real outputs; do not claim completion
without having run them.

## 8. Out of scope

Explicitly not part of this task:

* EF Core, SQLite, any `DbContext`, any migration. `IOrderRepository`, `ICatalogItemRepository`,
  `IProductionLocationRepository`, `INumberAllocator`, and `IPrinterStatusReader` are interfaces only;
  no implementation is written here.
* HTTP, REST endpoints, request/response DTOs distinct from the `OrderAcceptanceRequest` /
  `OrderAcceptanceResult` shapes defined above, status code mapping, or the message-key rendering of
  `OrderValidationFailureReason` and `PrintFailureReason` into localized text. Core returns reasons and
  keys as enum values; resolving them into `Strings.de.resx` / `Strings.en.resx` text is a later task.
* SignalR, any hub, any event payload.
* ESC/POS byte rendering (`EscPosSlipRenderer` and the station card renderer from 11.1) and anything
  about the slip's physical layout.
* `IPrinterTransport` and any of its implementations (`NetworkPrinterTransport`, `AgentPrinterTransport`,
  `MockPrinterTransport`). This task's `IPrinterStatusReader` is a read-only status port only; dispatch
  (sending a job, claiming it, running the retry loop) is the printing-worker task's port surface, built
  on top of `PrintJobStateMachine` and `RetryPolicy` from this task but not itself part of it.
* `StationCircuitBreaker`, `ProcessIdAllocator`, `EnrolmentInvitationVerifier`, `DeviceTokenHasher`, and
  every entity/service that belongs to enrolment, device tokens, printer configuration
  (`PrinterConfiguration`, `PrinterStatus` as entities), or the admin/station HTTP surface. These are
  real, spec'd, and listed in 11.1, but they are outside the vertical slice this task builds (order
  placement, routing, ticket creation, print outcome/retry decisions) and belong to sibling tasks.
* Frontend (`frontend/src/core/*`) and desktop. This task is `backend/GastronomyApp.Core` only.
* Localization resx content of any kind.
* Git commands. The owner commits separately.
