# Cross-task consistency review: T002 through T007

Scope: the seams only. Every place one task document assumes what another defines. The spec itself is
not re-reviewed; it is quoted only where a seam turns on it. Architect rulings 1 to 10 are applied as
given and are not relitigated: where a document disagrees with a ruling, that is a finding against
that document.

Finding ids are `T00X-N` so a fix instruction can cite them.

---

## 1. Verdict

| Document | Findings | Can go to coding as-is? |
|---|---|---|
| T002 Core domain | 9 | **Almost. Fix T002-1 through T002-6 first (all additive, roughly one page of edits), then code.** It blocks all five siblings, so it goes first and its fixes are cheap. |
| T003 Infrastructure | 12 | No. Section 2 is written against a Core that does not exist; every port name, the entity namespace, the status enum name, and the whole of step 4's ownership must be corrected before a coder starts. |
| T004 Printing | 11 | No, but close. Renames plus two ownership statements (`IPrinterFleet`, the projection write). Its test intent survives unchanged, exactly as its own section 2 predicted. |
| T005 Api | 14 | **No. This is the document that needs real rewriting**, not reconciling. Rulings 1, 2, 3 and 4 all land on it, and its entire "assumed from T002" use-case surface does not exist anywhere in the six documents. |
| T006 Frontend | 11 | No. One hard correctness fix (`OrderStatus`, ruling 5), plus it must adopt four orphans nobody else picked up. |
| T007 Desktop | 9 | No, but the fixes are mechanical: rulings 3 and 7 applied verbatim, plus one port assignment. |

Order of work: T002 fixes, then T003 and T004 in parallel, then T005, with T006 and T007 startable
any time after T002 and T005's step 1 respectively.

---

## 2. Per-document findings

### T002 Core domain

**T002-1. `PrinterEndpointKeyBuilder` is absent from the file layout and from the section 6 summary.**
Ruling 8 puts it in Core as a non-static service. Two consumers already assume it:

- T003 section 2.1: "computed by a Core service (assumed name `PrinterEndpointKeyBuilder`) that this
  task's repositories call rather than reimplementing the join themselves."
- T004 section 2: "a non-static instance method on a small `PrinterEndpointKeyBuilder` class, per
  backend hard rule 1 forbidding static methods."

Fix: add `Services/PrinterEndpointKeyBuilder.cs` to T002's layout, to section 6's service list, and as
an ordered step with a red-first test covering spec 2.13's canonical form
(`TransportKind|Host|Port|AgentIdentifier`, empty string for unused parts, never parsed back apart).
Exact signature both consumers need:

```csharp
namespace GastronomyApp.Core.Services;

public sealed class PrinterEndpointKeyBuilder
{
    public string Build(TransportKind transportKind, string host, int? port, string agentIdentifier)
    {
    }
}
```

Place it before step 3 in T002's order, because T003's step 3 allocator needs it and T004's fleet
grouping needs it.

**T002-2. `ISlipTextProvider` is not cross-referenced, and T002's out-of-scope list reads as a ban on
it.** Ruling 9 keeps T004's Core port with the resx-backed Infrastructure implementation. T002 section
8 currently says "Localization resx content of any kind" is out of scope, which is true but reads to a
T002 coder as "Core owns no localization port", risking a rival `ILocalizer`. Fix: add one line to
T002 section 6 naming `GastronomyApp.Core.Localization.ISlipTextProvider` and `SlipStrings` as
**T004-introduced**, not to be created by T002 and not to be duplicated.

**T002-3. `PrintOutcomeMapping` has no `FailureReason` property, but T002's own step 9 prose requires
one.** Step 9 says: "`PrintOutcomeMapping` for a `Blocked`-status row carries `FailureReason: null`
and only a row whose `ShouldRetryAutomatically` is false and whose resulting status is terminal for
this attempt carries a reason." The declared record is:

```csharp
public sealed record PrintOutcomeMapping
{
    public required PrintJobStatus JobStatus { get; init; }
    public required LocationTicketStatus TicketStatus { get; init; }
    public required bool ShouldRetryAutomatically { get; init; }
}
```

T004's worker needs the reason to write `PrintJob.FailureReason` and to feed
`IPrintCallbacks.OnTicketStatusChangedAsync`. Fix: add
`public PrintFailureReason? FailureReason { get; init; }` to the record. Spec 7.6's table assigns no
reason to any of its ten rows (the reason is assigned later, by the give-up window, the outer bound or
the breaker), so every row maps it `null`; the property exists so the terminal writers have one place
to put it. Correct the step 9 prose to say exactly that.

**T002-4. `PrinterConfiguration` and `PrinterStatus` are declared out of scope, and no other document
defines them.** T002 section 4.2: "`PrinterConfiguration` and `PrinterStatus` are out of scope for this
task (see section 8)." But T003 section 2.1 lists both in its assumed entity set and section 3 gives
them `PrinterConfigurationConfiguration.cs`, `PrinterStatusConfiguration.cs` and DbSets, and T004
section 2 assumes both "produced by T002". Entities live in Core (backend project table), so nobody
else can own them. Fix: T002 adds both entities per spec 2.12, with the same `sealed class` shape as
its other entities, and removes them from its out-of-scope list. This is the only orphan that would
block T003's migration outright.

**T002-5. `INumberAllocator` has no process-id member.** T003 assumes
`AllocatePrinterProcessIdAsync(string printerEndpointKey, CancellationToken)`; T004 assumes an
endpoint-keyed allocation at spec 7.4 step 6; spec 2.13 makes `PrinterProcessId` one of the three
counter kinds and T002 already declares `NumberCounterKind.PrinterProcessId`. Fix: add the third
member to T002's `INumberAllocator`, exactly as T003 spells it. Without it the enum member T002
defines has no consumer path.

**T002-6. The order status projection ownership rule is not stated.** Ruling 4 requires it, and T002 is
the document that owns `OrderStatusCalculator`. Fix: add one sentence to T002 step 5: "Whoever changes
a ticket writes the order status projection in the same transaction through this calculator: T004's
worker on the print path, T005 on the HTTP paths (acceptance, resolve, acknowledge). The calculator
itself never writes." T004 and T005 then quote that sentence verbatim (see T004-4 and T005-4).

**T002-7. The printing port surface of spec 7.2 is orphaned.** T002 section 8 puts `IPrinterTransport`
and everything around it out of scope; T004 section 2 assumes `PrintDispatchOutcome`,
`PrinterTransportKind`, `IPrinterTransport`, `IPrinterSession`, `PrinterEndpoint`, `PrintPayload`,
`PrinterStatusSnapshot`, `PrintDispatchResult` all exist in Core "produced by T002". Resolution: T004
introduces them itself, in `GastronomyApp.Core.Printing`, as its own step 0 (it is the only consumer
and the only implementer). Add a line to T002 section 6 naming them as T004-introduced so T002's coder
does not create rivals. See T004-5 for the two of those eight that collide with types T002 does define.

**T002-8. Enrolment and device ports are orphaned.** T003 assumes `IDeviceTokenStore` and
`IEnrolmentInvitationStore` "in `GastronomyApp.Core.Ports`"; T005 assumes `IDeviceTokenStore` from
Infrastructure; T002 section 8 explicitly excludes both. No domain service consumes them, so Core does
not need to own them for the domain's sake. Resolution: T003 declares both interfaces in
`GastronomyApp.Core.Ports` as part of its own work and says so in its section 2 (changing "assumes
T002 declares" to "this task declares"). T005 consumes `IDeviceTokenStore` only.

**T002-9. `ProcessIdAllocator` and `AcceptLanguageParser` are assumed by T003 as Core classes and do
not exist.** Resolution below, in T003-11 and T003-12; the T002 action is only to leave them out
deliberately rather than by accident, which its section 8 already does for `ProcessIdAllocator`.

---

### T003 Infrastructure

**T003-1. Entity namespace.** T003 section 2.1: "one file per type under a `GastronomyApp.Core.Domain`
namespace". T002 section 6: "Entities (`GastronomyApp.Core.Entities`, all `sealed class`)". Fix: T003
uses `GastronomyApp.Core.Entities` throughout.

**T003-2. Ticket status enum name.** T003 section 2.1: "`LocationTicket.Status` is typed
`TicketStatus`". T002 section 4.1: `public enum LocationTicketStatus`. Fix: `LocationTicketStatus`
everywhere in T003. Same fix in T004 (T004-1).

**T003-3. Allocator port name and shape.** T003 section 2.2: `public interface
INumberCounterAllocator`. T002: `public interface INumberAllocator`. Fix: `INumberAllocator`, with the
three members being T002's two plus T002-5's addition. T003's implementation class name
`NumberCounterAllocator` may stay, since it names the storage concern.

**T003-4. `IOrderRepository` shape, and the acceptance ownership overlap.** This is the single worst
seam in the set. T003 section 2.2 assumes:

```csharp
Task<OrderAcceptanceResult> AcceptAsync(NewOrderRequest request, CancellationToken cancellationToken);
```

T002 section 6 defines:

```csharp
Task<Order?> FindByClientOrderIdAsync(Guid clientOrderId, CancellationToken cancellationToken);
Task AddAsync(Order order, CancellationToken cancellationToken);
```

Worse than the signature: T003's step 4 has `OrderRepository.AcceptAsync` allocating the global order
number, creating one `LocationTicket` per distinct location, allocating each location sequence number,
inserting every `OrderLine`, and recomputing `TotalCents`. Every one of those is `OrderAcceptanceService`'s
job in T002 (its step 6 test list asserts all of them). Two implementations of one use case is exactly
what root rule 6 forbids. Fix, per ruling 1 and ruling 2:

- `IOrderRepository` is T002's two-member port. T003 implements exactly those two members.
- T003 owns the transaction scope only: an `OrderAcceptanceTransaction` (or the `AddGastronomyAppInfrastructure`-registered
  decorator) that issues `BEGIN IMMEDIATE`, calls `OrderAcceptanceService.AcceptAsync` once, and
  commits or rolls back. T003's step 4 reasoning about `BEGIN IMMEDIATE` versus `BEGIN DEFERRED` and
  about the same-transaction idempotent lookup is correct and survives; only the body of what runs
  inside it moves to Core.
- T003's defensive `TotalCents` recomputation before insert (spec 2.9) stays in T003, as an assertion
  inside `AddAsync`, since it is a storage-layer invariant check and not a second producer of the
  figure.
- T003's four `OrderRepositoryTest` methods survive verbatim as integration tests over the composed
  service plus repository.

**T003-5. Request record names and one field name.** T003 assumes `NewOrderRequest` /
`NewOrderLineRequest` in `GastronomyApp.Core.Ports`; T002 defines `OrderAcceptanceRequest` /
`OrderAcceptanceLineRequest` in `GastronomyApp.Core.Services`. Field mismatch inside them: T003's
`NewOrderLineRequest(... Guid? ChosenProductionLocationId ...)` against T002's
`OrderAcceptanceLineRequest { Guid? ProductionLocationId }`. T002's spelling wins (it matches spec
5.4's request field `productionLocationId`). Note the entity field `OrderLine.ChosenProductionLocationId`
keeps its own name; the request field and the entity field are deliberately not the same word.

**T003-6. `OrderAcceptanceResult` declared twice, differently.** T003: `public record
OrderAcceptanceResult(Order Order, bool WasAlreadyAccepted);` in `GastronomyApp.Core.Ports`. T002:
`sealed record OrderAcceptanceResult` with `required` init properties in `GastronomyApp.Core.Results`.
T002's wins; T003 deletes its copy.

**T003-7. `InfrastructureException` is declared in the wrong namespace and attributed to the wrong
document.** T003 section 2.3 assumes Core declares it (`namespace GastronomyApp.Core;`). Ruling 1 makes
database unavailability a cross-cutting **infrastructure** exception. Fix: T003 declares
`InfrastructureException` and `InfrastructureFailureReason` itself, in `namespace
GastronomyApp.Infrastructure`, and drops the "if T002 named this type differently" fallback paragraph.
The rest of T003's section 2.3 (catch `SqliteException`, never swallow, original becomes `inner`) is
correct and binding.

**T003-8. The enum-versus-string conditional in section 2.1 is resolved.** T003: "If T002 instead
stored these as plain `string`, the mapping in step 4 below changes from an enum conversion to a plain
`string` column with a check constraint". Ruling 6: T002 defines enums, T003 uses
`HasConversion<string>()`, member names match the spec's state names. Fix: delete the conditional; the
assumption note is resolved, not open. T002's members do match spec 3.1/3.2/3.3 exactly, checked row by
row.

**T003-9. `IDeviceTokenStore.IssueAsync` cannot return the plaintext token.** T003 flags this against
itself: "the consistency review should confirm whether T002's actual `IDeviceTokenStore.IssueAsync`
signature returns the plaintext token alongside `Device`, since `Device` itself never carries it".
T005 needs it: spec 5.2's redeem response carries `deviceToken`. Fix: `Task<IssuedDeviceToken>
IssueAsync(...)` with `public sealed record IssuedDeviceToken(Device Device, string PlaintextToken);`,
a named record per backend rule 8.

**T003-10. `VerifyAsync` return type collides with T005's assumption.** T003:
`Task<DeviceVerificationResult> VerifyAsync(string tokenLookupId, string secret, CancellationToken)`.
T005 section 2: "`IDeviceTokenStore` port with `Task<Device?> VerifyAsync(...)`". T003's wins (it
distinguishes "unknown" from "revoked" for the caller); T005's auth handler adapts.

**T003-11. `ProcessIdAllocator` is assumed as a Core class and does not exist.** T003 step 3: "the
cycling arithmetic itself is `ProcessIdAllocator`'s unit-test territory per spec 11.1, assumed to be a
Core class this repository calls into". T002 section 8 explicitly excludes it. Fix: the wrap at 9999
lives in T003's `NumberCounterAllocator`, with T003's own
`AllocatePrinterProcessIdAsync_At9999_WrapsTo1AndPersists` test as its proof. Delete the assumed Core
class.

**T003-12. `AcceptLanguageParser` assumption.** T003 step 7 leaves it open ("if T002 already defines an
`AcceptLanguageParser` in Core, this task calls it"). It does not. Fix: T003 adds a private method, as
its own text already prefers.

Cosmetic but in a load-bearing paragraph: T003 step 1's `EnrolmentInvitationConfiguration` bullet
contains the typo "is not sufinstant on its own". The generated-column reasoning around it is sound and
is not a finding.

---

### T004 Printing

**T004-1. `TicketStatus` should be `LocationTicketStatus`** everywhere (section 2, the
`OrderStatusCalculator` call shape, `IPrintCallbacks.OnTicketStatusChangedAsync`). Same fix as T003-2.

**T004-2. `RetryPolicy` ownership is left as a fork; close it.** T004 step 6 test 22: "implemented as
the worker's own outcome-to-state mapping method (a private `MapOutcome` or an extracted `RetryPolicy`
class the worker calls...)". T002 owns `RetryPolicy` with
`PrintOutcomeMapping Map(PrintAttemptOutcome outcome, TransportKind transportKind, int bytesWritten)`.
Fix: T004 injects `RetryPolicy` into `PrinterWorker` and its test 22 asserts the worker applies the
mapping, not that it computes it. Delete the private-method fallback wording, and delete the section 7
ambiguity bullet that keeps the fork open.

**T004-3. The give-up window is computed in two places.** T004 tests 27 to 30 drive give-up and outer
bound behaviour from the worker; T002 owns `GiveUpWindowCalculator` with the exact same 5 minute and 20
minute constants (ruling 10 ratifies its `static readonly TimeSpan` fields as data). Fix: the worker
injects the calculator and is responsible only for reconstructing the `IReadOnlyCollection<SuspensionPeriod>`
from `PrinterStatus` and `PrinterConfiguration.IsEnabled` history, which is precisely the caller
responsibility T002 step 11 assigns. Tests 27 to 30 keep their names and become integration assertions.
`StationCircuitBreaker` is genuinely unowned by T002 (its section 8 excludes it) and stays T004's; say
so explicitly in T004 section 2 rather than leaving it implied.

**T004-4. State the projection ownership in T002's words.** T004 section 2 currently says the worker
"calls this after every ticket write that might change the order's projected status". Ruling 4 requires
both documents to state it identically. Fix: replace with the sentence from T002-6, naming the same
transaction.

**T004-5. Two Core types are duplicated under new names.** T004 assumes `PrinterTransportKind` and
`PrintDispatchOutcome`; T002 defines `TransportKind` (`Network`, `Agent`, `Mock`) and
`PrintAttemptOutcome` (`Confirmed`, `Blocked`, `Unreachable`, `SocketDropped`, `Timeout`,
`PrinterError`), which are the same two closed sets and are what `RetryPolicy.Map` takes. Fix: T004
uses T002's names and defines only the six types T002 does not have (`IPrinterTransport`,
`IPrinterSession`, `PrinterEndpoint`, `PrintPayload`, `PrinterStatusSnapshot`, `PrintDispatchResult`),
per T002-7.

**T004-6. `FailureReason` as "a closed set of strings (or an enum backing them)".** T002 defines
`PrintFailureReason` as an enum with exactly the nine members T004 lists. Fix: T004 takes the enum, and
`IPrintCallbacks.OnTicketStatusChangedAsync`'s `string? failureReason` parameter becomes
`PrintFailureReason? failureReason`. T005 renders it to the wire string in the SignalR payload.

**T004-7. Three spellings of the process id allocation.** T004 section 2 assumes
`AllocateNextAsync(CounterKind kind, Guid? eventSessionId, Guid? productionLocationId, string?
printerEndpointKey, ct)`; T004's own `IPrinterWorkerDataAccess` declares
`AllocateProcessIdAsync(string printerEndpointKey, ct)`; T003 declares
`AllocatePrinterProcessIdAsync(string printerEndpointKey, ct)`. Fix: the Core port member is T003's
name (per T002-5); `IPrinterWorkerDataAccess.AllocateProcessIdAsync` stays as the worker's narrow
delegate to it; delete the generic `AllocateNextAsync` assumption.

**T004-8. `IPrinterFleet` does not exist, and T005 depends on it heavily.** T004 produces a concrete
`PrinterFleet : IHostedService` with `StartAsync`/`StopAsync`/`ReconcileAsync` plus the outbound
`IPrintCallbacks`. T005 section 2 assumes an inbound `IPrinterFleet` with notifications, plus
`ReconnectAsync`, `TestPrintAsync` and `EnqueueAsync`. Nobody defines it. Fix (T004 owns it): add

```csharp
public interface IPrinterFleet
{
    Task EnqueueAsync(Guid locationTicketId, PrintJobKind kind, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> ReconnectAsync(Guid productionLocationId, CancellationToken cancellationToken);
    Task TestPrintAsync(Guid productionLocationId, CancellationToken cancellationToken);
}
```

implemented by `PrinterFleet`. Notifications stay outbound through `IPrintCallbacks`, which T005
implements (see T005-10); T005's "`IObservable` or an event" assumption is dropped, not reconciled.

**T004-9. Mock fault arming has two shapes.** T005 assumes
`MockPrinterTransport.ArmFault(Guid locationId, MockFault fault, MockFaultMode mode)`; T004 defines
`IMockFaultRegistry.Arm(Guid productionLocationId, MockFault fault, MockFaultMode mode)`. T004's wins;
T005's admin endpoint resolves `IMockFaultRegistry` from DI and forwards, returning 422 when the
location's transport kind is not `Mock`.

**T004-10. The slip resx pair collides by name with the desktop pair.** T004's constraint offers
"`Strings.de.resx` and `Strings.en.resx` (or the domain-specific pair this task adds, for example
`SlipStrings.de.resx` / `SlipStrings.en.resx`)". T007 step 1 creates `Strings.de.resx` /
`Strings.en.resx` in the desktop project. Different projects, so no build collision, but two files with
one name across the product invites the wrong one being edited. Fix: T004 commits to
`SlipStrings.de.resx` / `SlipStrings.en.resx` in `GastronomyApp.Infrastructure`, and drops the option.
T004 is the only document that creates a backend resx pair; that is correct and is now unambiguous.

**T004-11. T004 writes into `GastronomyApp.Api.Tests`, which T005 also claims.** T004 step 6 and step 8
create `Api.Tests/Printing/*`; T005 section 3 lists a full `Api.Tests` layout and claims the deletion of
T001's `ScaffoldingSmokeTest.cs`. Fix: whichever lands first deletes it; both documents say "delete
`backend/GastronomyApp.Api.Tests/ScaffoldingSmokeTest.cs` if it is still present". No other file
collides between the two.

---

### T005 Api

This document needs rewriting, not patching. Findings 1 to 4 each invalidate a section.

**T005-1. `DomainException` does not exist (ruling 1).** T005 section 2: "returning a result record or
throwing a typed exception derived from `GastronomyApp.Core.DomainException` (assumed base type,
carrying `Code` and `MessageKey` string properties...)". Core returns `Result<TValue, TFailure>`. Fix:
step 2's middleware becomes a result-to-envelope mapper at the endpoint layer, and the status table is
rekeyed from `Code` prefixes onto T002's failure enums. The concrete mapping the slice needs:

| Core failure value | HTTP | `code` |
|---|---|---|
| `OrderValidationFailureReason.NoLines`, `.QuantityOutOfRange`, `.TableLabelMissing`, `.TableLabelTooLong` | 400 | `ValidationFailed` |
| `OrderValidationFailureReason.UnknownCatalogItemId`, `.StationRequired`, `.StationNotAssignedToItem` | 422 | `UnprocessableEntity` |
| `RoutingFailureReason.StationRequired`, `.StationNotAssignedToItem` | 422 | same |
| `InfrastructureException(DatabaseUnavailable)` | 503 | `DatabaseUnavailable` |

The 401/403/404/409/410 rows in T005's existing table are still correct as spec facts; they now come
from endpoint-level checks and repository lookups rather than from exception types. The **one** exception
middleware, per ruling 1, catches `InfrastructureException` and nothing else; every other unclassified
exception rethrows, exactly as T005's own root-rule-2 reasoning already says.

**T005-2. `DatabaseUnavailableException` is the wrong name (ruling 1).** T005 step 3 assumes
`GastronomyApp.Infrastructure.DatabaseUnavailableException`. T003 defines
`InfrastructureException` with `Reason == InfrastructureFailureReason.DatabaseUnavailable`. Fix: catch
T003's type; delete the `IDatabaseUnavailableClassifier` fallback paragraph, since the seam is now
settled.

**T005-3. The entire `GastronomyApp.Core.UseCases` surface does not exist (ruling 2).** T005 names
sixteen use cases (`RedeemEnrolmentInvitationUseCase`, `GetCatalogUseCase`, `PlaceOrderUseCase`,
`GetOrdersForPersonUseCase`, `GetOrderByIdUseCase`, `ResolveTicketUseCase`, `ReprintTicketUseCase`,
`CreateEnrolmentInvitationUseCase`, `AcknowledgeStationTicketUseCase`, `StartEventSessionUseCase`, and
the CRUD ones), all with `ExecuteAsync`. T002 produces `OrderAcceptanceService`,
`OrderStatusCalculator`, `OrderTotalCalculator`, `OrderRoutingResolver`, `TicketStateMachine`,
`PrintJobStateMachine`, `RetryPolicy`, `GiveUpWindowCalculator`, `TicketAcknowledgePolicy`, `Never`.
Nothing else exists in any document. Fix, per ruling 2 and the vertical-slice scope:

- Order placement calls `OrderAcceptanceService.AcceptAsync(OrderAcceptanceRequest, ct)` and branches
  on `Result.IsSuccess` and `OrderAcceptanceResult.WasAlreadyAccepted` for 201 versus 200. This is the
  seam T005 already relies on and it survives intact.
- Acknowledge calls `TicketAcknowledgePolicy.CanAcknowledge(LocationTicketStatus, StationPrintability)`
  with the `StationPrintability` read through `IPrinterStatusReader.GetCurrentAsync`. T005's own rule
  that the endpoint never re-evaluates the predicate is preserved exactly; only the call target changes.
- Reprint and resolve call `TicketStateMachine.CanTransition` before writing, then write, then
  recompute the order projection per T005-4.
- Every remaining endpoint (catalog read, orders read, admin CRUD, enrolment, event session) is a query
  or a store call with no domain rule attached: T005 owns those handlers directly over T003's
  repositories and `GastronomyAppDbContext`. Say so explicitly, so a coder does not stub sixteen
  non-existent Core classes.

**T005-4. The projection-write assumption must be split, not deleted (ruling 4).** T005 section 2 says
its job "is exactly two things and no more: push the SignalR events... and read the already-written
projection back out", and section 13 repeats "this subscription only pushes; it never writes
`LocationTicket.Status`, `PrintJob` rows, or runs `OrderStatusCalculator` itself". That is correct for
the print path only. On the three HTTP paths T005 owns (acceptance, resolve, acknowledge), T005 is the
writer. Fix: state the ruling-4 sentence from T002-6 verbatim in both places, and add the projection
write to the resolve and acknowledge handlers' test lists.

**T005-5. `Build` must drop `string[] args` (ruling 3).** T005 step 1:

```csharp
public WebApplication Build(ApiHostOptions options, string[] args)
```

Fix: `public WebApplication Build(ApiHostOptions options)`, built through
`WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = ... })` or
`CreateSlimBuilder()` with no args. Everything else about the shape (instance method, unstarted
`WebApplication`, caller owns `StartAsync`/`StopAsync`) is already right and is what ruling 3 ratifies.
Delete the "if T007 needs a different shape" paragraph in section 8; T007 adopts this one.

**T005-6. `ApiHostOptions` spelling is authoritative and T007 must adopt it.** T005's record with three
`required init` properties in `GastronomyApp.Api.Options` wins over T007's positional record. No change
needed in T005; recorded here so the reconciliation goes one way.

**T005-7. `WebApplicationFactory<GastronomyAppApiApplication>` will not work as written.** T005 step 7
wires `ApiTestFactory` as a `WebApplicationFactory<T>` over a composition class. `WebApplicationFactory`
needs an entry-point assembly with a discoverable `Program`; `GastronomyApp.Api` is a class library with
no entry point, which is exactly the shape T001 chose deliberately. Fix: `ApiTestFactory` calls
`new GastronomyAppApiApplication().Build(options)` itself against a temp data directory and port 0, then
uses `app.GetTestClient()` after `StartAsync`, or wires `TestServer` directly. This also removes the
need for the `Microsoft.AspNetCore.Mvc.Testing` package.

**T005-8. New packages need the same explicit exception QRCoder gets.** T005 step 0 adds
`Microsoft.AspNetCore.SignalR.Client` and `Microsoft.AspNetCore.Mvc.Testing` to
`Directory.Packages.props`. T001 forbids packages beyond its list without reporting the gap. Fix: state
in T005 that this is an allowed, architect-approved exception, same class as ruling 7's QRCoder, and
drop `Mvc.Testing` per T005-7. `Microsoft.AspNetCore.SignalR.Client` stays (test project only).

**T005-9. `AddGastronomyAppInfrastructure` does not exist.** T005 section 2 assumes T003's extension
method `IServiceCollection AddGastronomyAppInfrastructure(this IServiceCollection services, string
dataDirectory)`. T003 defines no DI surface at all. Fix: T003 adds it (it owns the SQLite path, the
`SqliteConnectionFactory`, the migration-apply-on-startup behaviour and every repository registration),
and T005 keeps calling exactly that one method. Add it to T003's file layout as
`ServiceCollectionExtensions.cs`.

**T005-10. Notification direction is inverted.** See T004-8. Fix in T005: `HubNotificationDispatcher`
**implements** T004's `IPrintCallbacks` and is registered as its implementation, instead of subscribing
to events on `IPrinterFleet`. `GastronomyAppApiApplication.Build` registers `PrinterFleet` as the hosted
service and `EfCorePrinterWorkerDataAccess`, `ResxSlipTextProvider`, the transports and the transport
factory, which is the composition-root wiring T004 section 6 explicitly hands to whoever owns the
composition root. That is T005; say so in both documents.

**T005-11. Who enqueues print jobs after acceptance.** T005 section 2 assumes "`PlaceOrderUseCase`
(T002) is assumed to call this itself as part of accepting the order". T002's `OrderAcceptanceService`
takes no fleet dependency and must not (Core has no framework or printing dependency). Fix: T005's order
endpoint calls `IPrinterFleet.EnqueueAsync` once per ticket in the accepted order, after a genuine 201,
never after a 200. T005 already wrote that fallback sentence; promote it to the rule and delete the
assumption.

**T005-12. `ISessionStateQuery` (T007's dependency) is unassigned.** Nobody defines it. Fix: T005
defines it and registers it, in `GastronomyApp.Api` (namespace `GastronomyApp.Api.Hosting`), backed by
a read of the active `EventSession` through T003. Rationale for putting it in Api rather than Core: T007
resolves it from `app.Services` on the returned `WebApplication` and never sees Core; T002 is the
blocking document and this needs no domain service. T007 updates its `using` (see T007-3).

**T005-13. Step 5's throwaway `/api/admin/health-check` endpoint.** Harmless, but it must be deleted in
step 8 as T005 itself says; call it out in the verification list so it does not ship.

**T005-14. Two admin surfaces T006 consumes are out of T005's scope.** `GET/PUT
/api/admin/table-suggestions` (and `.../from-last-session`) and `POST /api/admin/printers/discover` are
in T005's section 7 out-of-scope list, and T006 builds screens against both. Resolve at T006 (see
T006-4 and T006-5); no change to T005's scope.

---

### T006 Frontend

**T006-1. `OrderStatus` has the wrong member set (ruling 5).** T006 section 3.2:

```typescript
export type OrderStatus = 'Accepted' | 'NeedsAttention'
```

and section 8: "the frontend's `OrderStatus` union has exactly these two members". Spec 3.1's state
table and T002's enum both give four: `Accepted`, `Printing`, `Printed`, `NeedsAttention`. Fix: the
union carries all four. T006's stated safety mechanism (an `assertNever` that fails to compile when the
backend adds a value) is a good mechanism pointed at the wrong baseline; with two members it would have
failed on the first real payload instead of at review time.

**T006-2. `presentOrderState` must map the full enum, plus spec 3.1's rendering rule.**
`OrderPresentationState` is `'Printing' | 'Printed' | 'HandledOnPaper' | 'NeedsAttention'`, which is
right, but the mapping must now be stated: `Accepted` and `Printing` both present as `'Printing'` (spec
3.1's state table gives both "Wird gedruckt" / "Printing"); `Printed` presents as `'HandledOnPaper'`
when at least one of the order's tickets is `HandledOnPaper`, else `'Printed'` (spec 3.1: "when at
least one of the order's tickets is `HandledOnPaper`, the phone renders
`orders.status.handledOnPaper`"); `NeedsAttention` presents as itself.

**T006-3. `failureReason` typed as `string | null` cannot support the promise made for it.** T006
section 3.2 types `TicketSummary.failureReason: string | null`, and section 3.2's `messageForTicket`
says "a `failureReason` this module does not recognise is a compile error, not a fallback". A `string`
makes that impossible. Fix: type it as the nine-member union mirroring T002's `PrintFailureReason`
(`'PaperEnd' | 'CoverOpen' | 'Unreachable' | 'Timeout' | 'SocketDropped' | 'PrinterError' |
'StationDisabled' | 'StationFaulty' | 'TicketResolvedByHuman'`), so `assertNever` does the work the
document claims for it.

**T006-4. `POST /api/admin/printers/discover` and `PrinterDiscovered` have no backend in the slice.**
T006 section 2.2 consumes the endpoint, section 2.4 consumes the event, section 7 says "the discovery
button and result list for a real network printer are in scope for `PrintersList.vue`"; T005 section 7
excludes the endpoint and T004 excludes discovery entirely. Fix: T006 drops the discovery button and the
`PrinterDiscovered` handler from the slice and says so in its section 7, which currently contradicts
itself on this point.

**T006-5. Table suggestions management has no backend in the slice.** T006 section 2.2 and section 7
put the table-suggestions screen in scope ("spec 8.9 names it as part of catalog setup this slice
needs"); T005 excludes `GET/PUT /api/admin/table-suggestions` and `.../from-last-session`. The read side
survives regardless, because `GET /api/catalog` carries `tableSuggestions[]` and that is what
`tableLabel.ts`'s `suggestionMatches` consumes. Fix: keep the chips on the review screen, drop the admin
management screen from the slice, or add the two endpoints to T005. Recommendation: drop the screen; the
chips work without it.

**T006-6. `ReprintCount` is missing from the assumed station ticket payload.** T006 section 2.3 says
each ticket carries "`canAcknowledge` and, when false, the reason key" only, yet step 9's
`StationTicketRow.vue` test covers "the `station.reprint` chip". Spec 5.6 requires `ReprintCount` on the
row and names the key `station.reprint`; T005's step 9 does list `ReprintCount` in its payload. Fix:
add `reprintCount: number` to T006's station row shape. The chip renders when it is greater than zero.

**T006-7. Two Playwright configs.** T001 created `frontend/e2e/playwright.config.ts`; T006 section 3.1
adds `frontend/e2e/order-placement/playwright.config.ts`. Fix: one config, T001's, at
`frontend/e2e/playwright.config.ts` with `testDir: '.'`; T006 deletes the second and puts only
`order-placement.spec.ts` and `order-placement.flow.md` in the subfolder.

**T006-8. Nobody adds the `test:e2e` script.** `frontend/CLAUDE.md` documents `npm run test:e2e`; T001
installs `@playwright/test` and writes no script; T006 step 11 says "wire `npm run test:e2e` to run it"
without naming the file it edits. Fix: T006 adds `"test:e2e": "playwright test --config e2e/playwright.config.ts"`
to `frontend/package.json`.

**T006-9. Nobody deletes T001's frontend placeholders.** T001 says `src/core/placeholder.ts` is deleted
"in the same task that adds the first real `src/core/` module" and its spec with it. T006 adds nine core
modules and never mentions either file. Fix: T006 step 1 deletes `frontend/src/core/placeholder.ts` and
`frontend/tests/core/placeholder.spec.ts`.

**T006-10. The SignalR client package is missing.** T006 sections 2.4 and 3.3 build a `HubConnection`;
T001's allowed frontend package list is Vite/Vue/TypeScript plus `pinia`, `vue-i18n`, `vitest`,
`@vue/test-utils`, `jsdom`, `@playwright/test`. Fix: T006 adds `@microsoft/signalr` to
`frontend/package.json`, stated as an allowed exception to T001's list on the same footing as ruling 7's
QRCoder.

**T006-11. `tests/core/i18nCoverage.spec.ts` is named in step 2 but absent from the section 3.1
layout.** Cosmetic; add it, so the file list is the whole file list.

Confirmed clean at this seam: T006's REST tables in section 2.1, its SignalR event table in 2.4, its
draft-cart and submission-identity rules in 2.5, and its plural-forms handling in step 2 all match spec
5.2 to 5.4, 6.2, 9.2 to 9.3 and 8.1 exactly, including `header.stationWaiting` being appended to an
existing banner rather than shown alone (spec 8.5) and the station page refetching rather than rendering
a payload (spec 6.2).

---

### T007 Desktop

**T007-1. `ApiHost.Build` does not exist (ruling 3).** T007 sections 2, 5 step 9 and 9.1 assume:

```csharp
public static class ApiHost
{
    public static WebApplication Build(ApiHostOptions options);
}
```

Fix: `new GastronomyAppApiApplication().Build(options)`, an instance method with no `args`.
`HostLauncher` constructs the class. T007's own note that a static entry point would break root rule 1
was correct and is now resolved in its favour.

**T007-2. `ApiHostOptions` spelling.** T007 declares a positional record; T005 declares three `required
init` properties in `GastronomyApp.Api.Options`. T005's wins (T005-6). Fix the declaration in T007
section 2 and stop re-declaring it at all: it belongs to Api, and T007 only constructs it.

**T007-3. `ISessionStateQuery` namespace.** T007 assumes `GastronomyApp.Core.Sessions`. Per T005-12 it
lives in `GastronomyApp.Api.Hosting` and is registered by `Build`. The shape T007 depends on
(`Task<bool> IsSessionActiveAsync(CancellationToken)`, resolved from `app.Services`) is unchanged, which
is what T007 asked for.

**T007-4. The QR encoder wording is replaced (ruling 7).** T007 section 6: "`QrCodeGenerator` picks a
small dependency-free managed QR encoder (no native interop...)" and assumption 9.4 leaves the library
open. Fix: QRCoder (MIT) is ratified. T007's coder adds `QRCoder` to `Directory.Packages.props`, stated
in the document as an architect-approved exception to T001's no-new-packages constraint. `IQrCodeGenerator`
stays exactly as declared; `QrCodeGenerator` adapts QRCoder's `QRCodeData.ModuleMatrix` into
`IReadOnlyList<bool[]>`. Keep the note that open question 11 governs only the printed-slip path, and add
that the same package would serve the raster fallback if 11 ever forces it. T004 needs no package and no
matrix for `GS ( k`; that stays true.

**T007-5. The desktop scaffolding smoke test deletion is unassigned.** T001 requires
`desktop/GastronomyApp.Desktop.Tests/ScaffoldingSmokeTest.cs` to be deleted by the first task adding a
real fixture there. T007 adds five fixtures and never says it. Fix: add the deletion to T007 step 3.

**T007-6. `Avalonia.Headless` package references are already done.** T007 section 3 marks
`GastronomyApp.Desktop.Tests.csproj` as "edited: FakeItEasy, Avalonia.Headless.NUnit references"; T001
step 7b already added all three and pinned the versions centrally. Fix: drop the edit note so nobody
re-adds a `Version` attribute and breaks central package management.

**T007-7. The window title literal handover.** T007 section 6 says `desktop.windowTitle` replaces T001's
deliberate literal. Correct and consistent with T001's own note. No change; recorded so the T001 carve-out
is visibly closed.

**T007-8. `HostLaunchResult.Started(WebApplication)` puts an ASP.NET type in a desktop port.** Legal (the
desktop references Api, which carries the framework reference) and it is what lets `HostLauncher` own
`StartAsync`/`StopAsync`. No change, but it means `GastronomyApp.Desktop.Tests` faking `IHostLauncher`
must never need to construct a real `WebApplication`; T007's tests only assert on the non-`Started`
cases, which they already do.

**T007-9. Tray icon.** T007 section 10 offers to drop it if the review prefers. Keep it as specified:
two menu items, both routed to `MainWindowViewModel`'s existing commands, no independent logic. It costs
nothing and matches `desktop/CLAUDE.md`'s note that `TrayIcon` is native.

---

## 3. Assumption reconciliation table

Every "Assumed from" entry in T003 through T007, checked against the defining document.

### T003 assumptions (against T002)

| Assumption as T003 spells it | Defining document | Verdict | Resolution |
|---|---|---|---|
| Entities in `GastronomyApp.Core.Domain` | T002 sec 6: `GastronomyApp.Core.Entities` | MISMATCH | T003-1: use `.Entities` |
| Sixteen entities including `PrinterConfiguration`, `PrinterStatus` | T002 sec 4.2 defines fourteen, excludes those two | MISMATCH | T002-4: T002 adds both |
| `Order.Status` typed `OrderStatus` | T002 `public enum OrderStatus` | VERIFIED | none |
| `LocationTicket.Status` typed `TicketStatus` | T002 `public enum LocationTicketStatus` | MISMATCH | T003-2 |
| `PrintJob.Status` typed `PrintJobStatus` | T002 `public enum PrintJobStatus` | VERIFIED | none |
| Enum member names are the spec's state names | T002 sec 4.1, checked row by row against spec 3.1/3.2/3.3 | VERIFIED | ruling 6 satisfied |
| `CounterKind` enum with `GlobalOrder`, `LocationSequence`, `PrinterProcessId` | T002 `public enum NumberCounterKind` with those three members | MISMATCH (name only) | use `NumberCounterKind`; entity property is `CounterKind`, which T002 already spells that way |
| `PrinterEndpointKey` is a `string` built by a Core `PrinterEndpointKeyBuilder` | absent from T002 | MISMATCH | ruling 8, T002-1 |
| `IOrderRepository.AcceptAsync(NewOrderRequest, ct)` | T002: `FindByClientOrderIdAsync` + `AddAsync` | MISMATCH | T003-4, and the acceptance body moves to `OrderAcceptanceService` |
| `NewOrderRequest` / `NewOrderLineRequest` | T002: `OrderAcceptanceRequest` / `OrderAcceptanceLineRequest` | MISMATCH | T003-5 |
| `NewOrderLineRequest.ChosenProductionLocationId` | T002: `OrderAcceptanceLineRequest.ProductionLocationId` | MISMATCH | T003-5 |
| `OrderAcceptanceResult(Order, bool)` in `Core.Ports` | T002: same name, `Core.Results`, init properties | MISMATCH | T003-6 |
| `INumberCounterAllocator` | T002: `INumberAllocator` | MISMATCH | T003-3 |
| `AllocateGlobalOrderNumberAsync(Guid, ct)` | T002: identical | VERIFIED | none |
| `AllocateLocationSequenceNumberAsync(Guid, Guid, ct)` | T002: identical | VERIFIED | none |
| `AllocatePrinterProcessIdAsync(string, ct)` | absent from T002 | MISMATCH | T002-5: add to `INumberAllocator` |
| `IDeviceTokenStore` in `Core.Ports` | absent (T002 sec 8 excludes) | MISMATCH | T002-8: T003 declares it |
| `IEnrolmentInvitationStore` in `Core.Ports` | absent (T002 sec 8 excludes) | MISMATCH | T002-8: T003 declares it |
| `InfrastructureException` in `namespace GastronomyApp.Core` | absent | MISMATCH | ruling 1 + T003-7: T003 declares it in Infrastructure |
| `ProcessIdAllocator` as a Core class | absent (T002 sec 8 excludes) | MISMATCH | T003-11: cycling stays in T003 |
| `AcceptLanguageParser` in Core | absent | MISMATCH | T003-12: private method in T003 |

### T004 assumptions (against T002 and T003)

| Assumption as T004 spells it | Defining document | Verdict | Resolution |
|---|---|---|---|
| `PrintJob`, `PrintAttempt`, `LocationTicket`, `ProductionLocation`, `Order`, `OrderLine`, `NumberCounter`, `EventSession` entities | T002 sec 4.2 | VERIFIED | none |
| `PrinterConfiguration`, `PrinterStatus` entities | T002 sec 4.2 excludes | MISMATCH | T002-4 |
| `TicketStatus` enum, eight members | T002 `LocationTicketStatus`, same eight members in the same order | MISMATCH (name only) | T004-1 |
| `PrintJobStatus`, ten members | T002: identical | VERIFIED | none |
| `PrintJobKind` (`Initial`, `Reprint`, `Test`) | T002: identical | VERIFIED | none |
| `TicketStateMachine.CanTransition(from, to)` | T002: identical shape | VERIFIED | parameter type renamed per T004-1 |
| `PrintJobStateMachine` equivalent | T002: identical | VERIFIED | none |
| `OrderStatusCalculator.Calculate(IReadOnlyCollection<TicketStatus>, bool isPractice)` | T002: `Calculate(IReadOnlyCollection<LocationTicketStatus>, bool isPracticeSession)` | MISMATCH (type name only) | T004-1 |
| `PrinterTransportKind` | T002: `TransportKind` (`Network`, `Agent`, `Mock`) | MISMATCH | T004-5 |
| `PrintDispatchOutcome` | T002: `PrintAttemptOutcome`, six members matching spec 7.6 | MISMATCH | T004-5 |
| `IPrinterTransport`, `IPrinterSession`, `PrinterEndpoint`, `PrintPayload`, `PrinterStatusSnapshot`, `PrintDispatchResult` in Core | absent from T002 (sec 8 excludes) | MISMATCH | T002-7: T004 introduces them in `Core.Printing` |
| `FailureReason` as strings or an enum | T002: `PrintFailureReason` enum, nine members, exactly T004's list | MISMATCH (typing) | T004-6: take the enum |
| `ISlipTextProvider` + `SlipStrings` in `Core.Localization` | T004 itself; ruling 9 ratifies | VERIFIED | T002-2 cross-references it |
| `RetryPolicy` "if T002 has already placed one in Core" | T002 sec 6 lists `RetryPolicy` | VERIFIED | T004-2: close the fork, always call it |
| `GastronomyAppDbContext` with the ten DbSets it names | T003 sec 3: all ten present, plus six more | VERIFIED | none |
| `BEGIN IMMEDIATE` write serialization, 5 second busy timeout | T003 steps 2 and 4 | VERIFIED | none |
| Counter allocator `AllocateNextAsync(CounterKind, Guid?, Guid?, string?, ct)` | T003: three named methods, no generic one | MISMATCH | T004-7 |
| `PrinterEndpointKeyBuilder`, non-static, one place | ruling 8, T002-1 | VERIFIED once T002-1 lands | none |
| Direct `DbContext` injection acceptable, retargetable to a T003 port | T003 exposes no worker-shaped port | VERIFIED | `EfCorePrinterWorkerDataAccess` in Infrastructure over the DbContext, as T004 planned |

### T005 assumptions (against T002, T003, T004)

| Assumption as T005 spells it | Defining document | Verdict | Resolution |
|---|---|---|---|
| `GastronomyApp.Core.UseCases`, one class per use case, `ExecuteAsync` | T002: `Core.Services`, per-service method names | MISMATCH | ruling 2, T005-3 |
| `Core.DomainException` with `Code`, `MessageKey`, `Parameters` | absent | MISMATCH | ruling 1, T005-1 |
| `PlaceOrderUseCase.ExecuteAsync` returning `{Order, WasAlreadyAccepted}` | T002: `OrderAcceptanceService.AcceptAsync` returning `Result<OrderAcceptanceResult, OrderValidationFailure>`, `OrderAcceptanceResult { Order, WasAlreadyAccepted }` | MISMATCH (call shape), VERIFIED (result fields) | T005-3 |
| `AcknowledgeStationTicketUseCase` owning `canAcknowledge` | T002: `TicketAcknowledgePolicy.CanAcknowledge(LocationTicketStatus, StationPrintability)`, implementing spec 5.6 verbatim | MISMATCH (name), VERIFIED (rule) | T005-3 |
| `GetCatalogUseCase`, `GetOrdersForPersonUseCase`, `GetOrderByIdUseCase`, `ResolveTicketUseCase`, `ReprintTicketUseCase`, `CreateEnrolmentInvitationUseCase`, `StartEventSessionUseCase`, CRUD use cases | absent everywhere | MISMATCH | T005-3: T005 owns these handlers over T003 |
| `IClock` port in Core | T002 sec 6 defines `IClock` with `DateTime UtcNow` | VERIFIED | none |
| `IGuidGenerator` port in Core | absent; T002 has `OrderAcceptanceService` calling `Guid.NewGuid()` itself, deliberately | MISMATCH | drop the assumption |
| `AddGastronomyAppInfrastructure(services, dataDirectory)` | absent from T003 | MISMATCH | T005-9: T003 adds it |
| `IDeviceTokenStore.VerifyAsync` returning `Task<Device?>` | T003: `Task<DeviceVerificationResult>` | MISMATCH | T003-10 |
| `MockPrinterTransport.ArmFault(Guid, MockFault, MockFaultMode)` | T004: `IMockFaultRegistry.Arm(Guid, MockFault, MockFaultMode)` | MISMATCH | T004-9 |
| `MockFault` seven values, `MockFaultMode` two | T004: seven plus `None`, and `Once`/`Sticky` | VERIFIED | none |
| `DatabaseUnavailableException` in Infrastructure | T003: `InfrastructureException(InfrastructureFailureReason.DatabaseUnavailable, ...)` | MISMATCH | ruling 1, T005-2 |
| `IPrinterFleet` with notifications, `ReconnectAsync`, `TestPrintAsync`, `EnqueueAsync` | absent from T004 | MISMATCH | T004-8, T005-10 |
| `PrinterFleetHostedService` | T004: `PrinterFleet : IHostedService` | MISMATCH (name) | use `PrinterFleet` |
| `TicketStatusChangedNotification` fields per spec 6.2 | T004: `IPrintCallbacks.OnTicketStatusChangedAsync(orderId, ticketId, status, failureReason, ct)` | MISMATCH (payload is narrower) | T005's dispatcher reads the remaining fields (`globalOrderNumber`, `locationName`, `sequenceNumber`, `printerHasPaper`, `messageKey`, `parameters`) from the projection it just read; state this in T005 step 13 |
| `PrinterStatusChanged` notification fields | T004: `OnPrinterStatusChangedAsync(locationId, snapshot, isFaulty, waitingTicketCount, ct)` | VERIFIED (sufficient) | dispatcher composes the wire payload from the snapshot |
| T004's worker writes the ticket and order projection in its own transaction | ruling 4 | VERIFIED for the print path | T005-4: T005 owns the three HTTP paths |
| `PlaceOrderUseCase` enqueues print jobs itself | T002: no fleet dependency | MISMATCH | T005-11 |

### T006 assumptions (against T005, and spec 5/6 as the contract)

| Assumption as T006 spells it | Defining document | Verdict | Resolution |
|---|---|---|---|
| The ten phone REST rows in sec 2.1 | T005 steps 7, 8, 11 and spec 5.2 to 5.4, 5.7 | VERIFIED | none |
| `OrderStatus` is `'Accepted' \| 'NeedsAttention'` | T002 enum: four members; spec 3.1 state table: four | MISMATCH | ruling 5, T006-1 |
| `TicketStatus` eight members | T002 `LocationTicketStatus`: same eight | VERIFIED | none |
| Error envelope `{code, messageKey, parameters, details}`, `details` never for a device | T005 step 2 `ApiError`; spec 5.1 | VERIFIED | none |
| Station rows carry `canAcknowledge` plus a reason key | T005 step 9; spec 5.6 | VERIFIED | none |
| Station rows carry `ReprintCount` for the `station.reprint` chip | T005 step 9 lists it; T006's own sec 2.3 omits it | MISMATCH (inside T006) | T006-6 |
| Nine SignalR events with the payloads in sec 2.4 | spec 6.2; T005 step 12 | VERIFIED | none |
| `PrinterDiscovered` and `POST /api/admin/printers/discover` | T005 sec 7: out of scope | MISMATCH | T006-4 |
| `GET/PUT /api/admin/table-suggestions`, `.../from-last-session` | T005 sec 7: out of scope | MISMATCH | T006-5 |
| Reconnect intervals 0, 2, 5, 10, 30 then 30s; refetch on reconnect; 15s poll fallback | spec 6.3 | VERIFIED | none |
| One hub at `/hub`, device auth via `access_token` query | T005 step 4 and step 12; spec 6 | VERIFIED | none |
| Draft cart and `clientOrderId` rules in sec 2.5 | spec 9.2, 9.3; T005 step 8's idempotency tests | VERIFIED | none |
| Admin API returns 404 to a non-laptop caller, `/admin` page itself served everywhere | T005 step 5; spec 5.1 | VERIFIED | none |

### T007 assumptions (against T005)

| Assumption as T007 spells it | Defining document | Verdict | Resolution |
|---|---|---|---|
| `static class ApiHost` with `static WebApplication Build(ApiHostOptions)` | T005: instance `GastronomyAppApiApplication.Build(options, args)`; ruling 3 removes `args` | MISMATCH | T007-1 |
| `ApiHostOptions(string DataDirectory, int Port, string BindAddress)` positional | T005: same three fields as `required init` properties | MISMATCH (spelling) | T007-2 |
| `Build` returns an unstarted `WebApplication`; the caller owns `StartAsync`/`StopAsync` | T005 step 1 states exactly this | VERIFIED | none |
| `ISessionStateQuery.IsSessionActiveAsync(ct)` in `Core.Sessions`, resolved from `app.Services` | absent everywhere | MISMATCH | T005-12, T007-3 |
| Port-in-use surfaces as `IOException`/`SocketException` from `StartAsync` | T005 binds via `UseUrls`, so Kestrel binds at `StartAsync` | VERIFIED | none |
| "Which network to display" is desktop-local, not in `ApiHostOptions` | T005 step 1 says the same, in the same words | VERIFIED | none |
| A dependency-free managed QR encoder chosen inside T007 | ruling 7: QRCoder, MIT | MISMATCH | T007-4 |

---

## 4. Orphans and overlaps

**Orphans, things every document assumed someone else would build:**

| Orphan | Assumed by | Assigned to |
|---|---|---|
| `PrinterEndpointKeyBuilder` | T003, T004 | T002 (ruling 8), T002-1 |
| `PrinterConfiguration`, `PrinterStatus` entities | T003, T004 | T002, T002-4 |
| `INumberAllocator.AllocatePrinterProcessIdAsync` | T003, T004 | T002, T002-5 |
| Spec 7.2's transport port surface | T004 | T004 itself, in `Core.Printing`, T002-7 |
| `IDeviceTokenStore`, `IEnrolmentInvitationStore` interfaces | T003, T005 | T003, T002-8 |
| `InfrastructureException` | T003, T005 | T003, in Infrastructure, T003-7 |
| `IPrinterFleet` (inbound: enqueue, reconnect, test print) | T005 | T004, T004-8 |
| The Api composition root's DI wiring for fleet, transports, data access, slip text | T004 hands it off "to T003 or a later Api-wiring task" | T005, T005-10 |
| `AddGastronomyAppInfrastructure` | T005 | T003, T005-9 |
| `ISessionStateQuery` | T007 | T005, in `GastronomyApp.Api.Hosting`, T005-12 |
| The backend slip resx pair | T004 names it conditionally | T004, fixed as `SlipStrings.de/en.resx`, T004-10 |
| SignalR client wiring on the frontend (`@microsoft/signalr` package) | T006 uses a `HubConnection` | T006, T006-10 |
| The Playwright config and the `test:e2e` script | T001 wrote a config, no script; T006 wrote a second config | T006, T006-7 and T006-8 |
| Deletion of `frontend/src/core/placeholder.ts` and its spec | T001 requires it, T006 never claims it | T006, T006-9 |
| Deletion of `desktop/.../ScaffoldingSmokeTest.cs` | T001 requires it, T007 never claims it | T007, T007-5 |

The four T001 smoke tests, resolved: Core.Tests is deleted by T002 (its step 3, explicit);
Infrastructure.Tests by T003 (its section 3, explicit); Api.Tests by T004 or T005, whichever lands
first (T004-11); Desktop.Tests by T007 (T007-5). All four are now claimed exactly once.

**Overlaps, two documents claiming the same work:**

| Overlap | Resolution |
|---|---|
| Order acceptance logic: T002's `OrderAcceptanceService` and T003's `OrderRepository.AcceptAsync` both allocate numbers, create tickets, compute totals | T003-4: T002 owns the use case, T003 owns the transaction and the storage |
| Retry mapping: T002's `RetryPolicy` and T004's worker `MapOutcome` | T004-2: T002 owns it |
| Give-up window: T002's `GiveUpWindowCalculator` and T004's worker tests 27 to 30 | T004-3: T002 owns the arithmetic, T004 owns the suspension-period reconstruction |
| Order status projection write: T004's worker and T005's notification handler | ruling 4: split by path, stated identically in both, T002-6 / T004-4 / T005-4 |
| `TransportKind` versus `PrinterTransportKind`, `PrintAttemptOutcome` versus `PrintDispatchOutcome` | T004-5: T002's names |
| `GastronomyApp.Api.Tests` layout and the smoke-test deletion | T004-11 |
| `Directory.Packages.props`: T005 adds SignalR.Client (+ Mvc.Testing, dropped), T007 adds QRCoder, T006 adds an npm package | Three separate, architect-approved exceptions; each document states its own, none removes another's |
| The station acknowledge rule: T002's `TicketAcknowledgePolicy`, T005's endpoint, T006's button | Single source confirmed: policy computes, endpoint serializes, button renders. No recomputation anywhere. See section 5. |

---

## 5. Hunt areas, one line each

- **Spelling of every named type, port, enum member, record field and endpoint:** covered in the section
  3 tables; 42 assumptions checked, 24 VERIFIED, 18 MISMATCH, every mismatch resolved above.
- **Ownership overlaps:** eight found, all resolved in section 4; the acceptance overlap (T002 against
  T003) is the one that would have produced two working but divergent implementations.
- **Orphans:** fifteen found, all assigned in section 4; the resx files, the SignalR client wiring, the
  Playwright config and the four smoke-test deletions were all genuinely unowned.
- **Sequencing traps:** T003's step 3 allocator and T004's fleet grouping both need
  `PrinterEndpointKeyBuilder`, which T002-1 places early in T002's order; T003's step 4 needs
  `OrderAcceptanceService`, which is T002's step 6, so T003 cannot start step 4 until T002 is complete
  (steps 0 to 3 of T003 are independent and can start on T002's step 1); T004's step 8 integration tests
  need T003's `GastronomyAppDbContext` and migration, so T004 steps 1 to 7 can run in parallel with T003
  but step 8 cannot; T006's Playwright spec is written now and stays `test.skip` until T005 lands, which
  T006 already states correctly; T007 needs only T005's step 1 plus T005-12's `ISessionStateQuery`, so it
  can start as soon as the composition entry compiles.
- **Spec 7.6 transport-dependent mapping, T002 against T004:** both take the ten rows verbatim and both
  split only the `Confirmed` row by transport (`Mock` to `PrintedOnTestPrinter`, `Network`/`Agent` to
  `Printed`); T002's `RetryPolicy` and T004's test 22 enumerate the identical row set. **Clean**, once
  T004-2 stops T004 reimplementing it and T002-3 adds the failure-reason field its own prose assumes.
- **`canAcknowledge`, T002 against T005 against T006:** T002's `TicketAcknowledgePolicy.CanAcknowledge`
  implements spec 5.6's boxed expression exactly, including the `Printing` exclusion; T005's endpoint
  serializes the decision and never re-evaluates it; T006's row renders the button from the server's
  boolean and refetches on both station events rather than recomputing. **Clean**, three layers, one
  producer, no drift.
- **The `station.reprint` chip:** spec 5.6 requires `ReprintCount` on the row and names the key; T005's
  step 9 payload carries it; T006 renders it but omits it from its own assumed shape. One gap, T006-6.
- **Plural forms, T006 against spec 8.5 and 8.1:** T006 step 2 routes every `{count}` key through
  vue-i18n plural forms with the singular written alongside, and knows that the backend and desktop resx
  strings have no plural machinery and split into two keys instead (T007 step 1 carries
  `desktop.phones.one` / `desktop.phones.many`). **Clean.**
- **Verification-command collisions:** none. T002 filters `~GastronomyApp.Core`, T003
  `~GastronomyApp.Infrastructure`, T004 `~Printing`, T005 the `Api.Tests` project, T007
  `~GastronomyApp.Desktop.Tests`. Two notes: T003's filter will also match T004's
  `Infrastructure.Tests/Printing` fixtures once they exist, so T003 cannot read a green run of that
  filter as proof of its own scope alone; and T005's step-3 solution-wide run includes T004's fixtures by
  design. The four suites compose into one green `dotnet test GastronomyApp.slnx` provided each of the
  four `ScaffoldingSmokeTest.cs` files is deleted exactly once, which section 4 now guarantees. The
  frontend suites (`npm test`, `npm run test:e2e`) sit outside the solution and do not interact.
