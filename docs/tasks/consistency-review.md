# Cross-task consistency review: T002 through T007

This is the original cross task review of the implementation briefs, written before any of the code
existed. Printing has since been removed from the product entirely, and the printer work has been
taken out of this file. What is left of it is the order lifecycle those briefs were written around,
whose states are still named after printing and after a ticket that was never built. That lifecycle is
gone too: an order item now moves from waiting to being prepared to ready on a station's tablet. For a
current description of the product, read `docs/spec.md`.

Scope: the seams only. Every place one task document assumes what another defines. The spec itself is
not re-reviewed; it is quoted only where a seam turns on it. Architect rulings 1 to 10 are applied as
given and are not relitigated: where a document disagrees with a ruling, that is a finding against
that document.

Finding ids are `T00X-N` so a fix instruction can cite them.

---

## 1. Verdict

| Document | Findings | Can go to coding as-is? |
|---|---|---|
| T002 Core domain | 3 | **Almost. Fix T002-6 first (additive, a few lines of edits), then code.** It blocks every sibling, so it goes first and its fixes are cheap. |
| T003 Infrastructure | 11 | No. Section 2 is written against a Core that does not exist; every port name, the entity namespace, the status enum name, and the whole of step 4's ownership must be corrected before a coder starts. |
| T005 Api | 12 | **No. This is the document that needs real rewriting**, not reconciling. Rulings 1, 2, 3 and 4 all land on it, and its entire "assumed from T002" use-case surface does not exist anywhere in the other documents. |
| T006 Frontend | 8 | No. One hard correctness fix (`OrderStatus`, ruling 5), plus it must adopt four orphans nobody else picked up. |
| T007 Desktop | 9 | No, but the fixes are mechanical: rulings 3 and 7 applied verbatim, plus one port assignment. |

Order of work: T002 fixes, then T003, then T005, with T006 and T007 startable
any time after T002 and T005's step 1 respectively.

---

## 2. Per-document findings

### T002 Core domain

**T002-6. The order status projection ownership rule is not stated.** Ruling 4 requires it, and T002 is
the document that owns `OrderStatusCalculator`. Fix: add one sentence to T002 step 5: "Whoever changes
a ticket writes the order status projection in the same transaction through this calculator: the Api
layer does this on every HTTP path that touches a ticket (acceptance, resolve, acknowledge). The
calculator itself never writes." T005 then quotes that sentence verbatim (see T005-4).

**T002-8. Enrolment and device ports are orphaned.** T003 assumes `IDeviceTokenStore` and
`IEnrolmentInvitationStore` "in `GastronomyApp.Core.Ports`"; T005 assumes `IDeviceTokenStore` from
Infrastructure; T002 section 8 explicitly excludes both. No domain service consumes them, so Core does
not need to own them for the domain's sake. Resolution: T003 declares both interfaces in
`GastronomyApp.Core.Ports` as part of its own work and says so in its section 2 (changing "assumes
T002 declares" to "this task declares"). T005 consumes `IDeviceTokenStore` only.

**T002-9. `AcceptLanguageParser` is assumed by T003 as a Core class and does
not exist.** Resolution below, in T003-12; the T002 action is only to leave it out
deliberately rather than by accident.

---

### T003 Infrastructure

**T003-1. Entity namespace.** T003 section 2.1: "one file per type under a `GastronomyApp.Core.Domain`
namespace". T002 section 6: "Entities (`GastronomyApp.Core.Entities`, all `sealed class`)". Fix: T003
uses `GastronomyApp.Core.Entities` throughout.

**T003-2. Ticket status enum name.** T003 section 2.1: "`LocationTicket.Status` is typed
`TicketStatus`". T002 section 4.1: `public enum LocationTicketStatus`. Fix: `LocationTicketStatus`
everywhere in T003.

**T003-3. Allocator port name and shape.** T003 section 2.2: `public interface
INumberCounterAllocator`. T002: `public interface INumberAllocator`. Fix: `INumberAllocator`, with
T002's two members. T003's implementation class name
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
assumption note is resolved, not open. T002's members do match spec 3.1 and 3.2 exactly, checked row by
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

**T003-12. `AcceptLanguageParser` assumption.** T003 step 7 leaves it open ("if T002 already defines an
`AcceptLanguageParser` in Core, this task calls it"). It does not. Fix: T003 adds a private method, as
its own text already prefers.

Cosmetic but in a load-bearing paragraph: T003 step 1's `EnrolmentInvitationConfiguration` bullet
contains the typo "is not sufinstant on its own". The generated-column reasoning around it is sound and
is not a finding.

---

### T005 Api

This document needs rewriting, not patching. The first four findings each invalidate a section.

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
`GetOrdersForPersonUseCase`, `GetOrderByIdUseCase`, `ResolveTicketUseCase`,
`CreateEnrolmentInvitationUseCase`, `StartEventSessionUseCase`, and
the CRUD ones), all with `ExecuteAsync`. T002 produces `OrderAcceptanceService`,
`OrderStatusCalculator`, `OrderTotalCalculator`, `OrderRoutingResolver`, `TicketStateMachine`,
`Never`.
Nothing else exists in any document. Fix, per ruling 2 and the vertical-slice scope:

- Order placement calls `OrderAcceptanceService.AcceptAsync(OrderAcceptanceRequest, ct)` and branches
  on `Result.IsSuccess` and `OrderAcceptanceResult.WasAlreadyAccepted` for 201 versus 200. This is the
  seam T005 already relies on and it survives intact.
- Resolve calls `TicketStateMachine.CanTransition` before writing, then writes, then
  recomputes the order projection per T005-4.
- Every remaining endpoint (catalog read, orders read, admin CRUD, enrolment, event session) is a query
  or a store call with no domain rule attached: T005 owns those handlers directly over T003's
  repositories and `GastronomyAppDbContext`. Say so explicitly, so a coder does not stub sixteen
  non-existent Core classes.

**T005-4. The projection-write assumption is the wrong way round (ruling 4).** T005 section 2 says
its job "is exactly two things and no more: push the SignalR events... and read the already-written
projection back out", and section 13 repeats "this subscription only pushes; it never writes
`LocationTicket.Status` or runs `OrderStatusCalculator` itself". On every HTTP path T005 owns
(acceptance, resolve, acknowledge), T005 is the
writer. Fix: state the ruling-4 sentence from T002-6 verbatim in both places, and add the projection
write to the resolve handler's test list.

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

**T005-12. `ISessionStateQuery` (T007's dependency) is unassigned.** Nobody defines it. Fix: T005
defines it and registers it, in `GastronomyApp.Api` (namespace `GastronomyApp.Api.Hosting`), backed by
a read of the active `EventSession` through T003. Rationale for putting it in Api rather than Core: T007
resolves it from `app.Services` on the returned `WebApplication` and never sees Core; T002 is the
blocking document and this needs no domain service. T007 updates its `using` (see T007-3).

**T005-13. Step 5's throwaway `/api/admin/health-check` endpoint.** Harmless, but it must be deleted in
step 8 as T005 itself says; call it out in the verification list so it does not ship.

**T005-14. An admin surface T006 consumes is out of T005's scope.** `GET/PUT
/api/admin/table-suggestions` (and `.../from-last-session`) is
in T005's section 7 out-of-scope list, and T006 builds a screen against it. Resolve at T006 (see
T006-5); no change to T005's scope.

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

**T006-5. Table suggestions management has no backend in the slice.** T006 section 2.2 and section 7
put the table-suggestions screen in scope ("spec 8.9 names it as part of catalog setup this slice
needs"); T005 excludes `GET/PUT /api/admin/table-suggestions` and `.../from-last-session`. The read side
survives regardless, because `GET /api/catalog` carries `tableSuggestions[]` and that is what
`tableLabel.ts`'s `suggestionMatches` consumes. Fix: keep the chips on the review screen, drop the admin
management screen from the slice, or add the two endpoints to T005. Recommendation: drop the screen; the
chips work without it.

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
`IReadOnlyList<bool[]>`.

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
| `Order.Status` typed `OrderStatus` | T002 `public enum OrderStatus` | VERIFIED | none |
| `LocationTicket.Status` typed `TicketStatus` | T002 `public enum LocationTicketStatus` | MISMATCH | T003-2 |
| Enum member names are the spec's state names | T002 sec 4.1, checked row by row against spec 3.1 and 3.2 | VERIFIED | ruling 6 satisfied |
| `CounterKind` enum with `GlobalOrder` and `LocationSequence` | T002 `public enum NumberCounterKind` with those two members | MISMATCH (name only) | use `NumberCounterKind`; entity property is `CounterKind`, which T002 already spells that way |
| `IOrderRepository.AcceptAsync(NewOrderRequest, ct)` | T002: `FindByClientOrderIdAsync` + `AddAsync` | MISMATCH | T003-4, and the acceptance body moves to `OrderAcceptanceService` |
| `NewOrderRequest` / `NewOrderLineRequest` | T002: `OrderAcceptanceRequest` / `OrderAcceptanceLineRequest` | MISMATCH | T003-5 |
| `NewOrderLineRequest.ChosenProductionLocationId` | T002: `OrderAcceptanceLineRequest.ProductionLocationId` | MISMATCH | T003-5 |
| `OrderAcceptanceResult(Order, bool)` in `Core.Ports` | T002: same name, `Core.Results`, init properties | MISMATCH | T003-6 |
| `INumberCounterAllocator` | T002: `INumberAllocator` | MISMATCH | T003-3 |
| `AllocateGlobalOrderNumberAsync(Guid, ct)` | T002: identical | VERIFIED | none |
| `AllocateLocationSequenceNumberAsync(Guid, Guid, ct)` | T002: identical | VERIFIED | none |
| `IDeviceTokenStore` in `Core.Ports` | absent (T002 sec 8 excludes) | MISMATCH | T002-8: T003 declares it |
| `IEnrolmentInvitationStore` in `Core.Ports` | absent (T002 sec 8 excludes) | MISMATCH | T002-8: T003 declares it |
| `InfrastructureException` in `namespace GastronomyApp.Core` | absent | MISMATCH | ruling 1 + T003-7: T003 declares it in Infrastructure |
| `AcceptLanguageParser` in Core | absent | MISMATCH | T003-12: private method in T003 |

### T005 assumptions (against T002 and T003)

| Assumption as T005 spells it | Defining document | Verdict | Resolution |
|---|---|---|---|
| `GastronomyApp.Core.UseCases`, one class per use case, `ExecuteAsync` | T002: `Core.Services`, per-service method names | MISMATCH | ruling 2, T005-3 |
| `Core.DomainException` with `Code`, `MessageKey`, `Parameters` | absent | MISMATCH | ruling 1, T005-1 |
| `PlaceOrderUseCase.ExecuteAsync` returning `{Order, WasAlreadyAccepted}` | T002: `OrderAcceptanceService.AcceptAsync` returning `Result<OrderAcceptanceResult, OrderValidationFailure>`, `OrderAcceptanceResult { Order, WasAlreadyAccepted }` | MISMATCH (call shape), VERIFIED (result fields) | T005-3 |
| `GetCatalogUseCase`, `GetOrdersForPersonUseCase`, `GetOrderByIdUseCase`, `ResolveTicketUseCase`, `CreateEnrolmentInvitationUseCase`, `StartEventSessionUseCase`, CRUD use cases | absent everywhere | MISMATCH | T005-3: T005 owns these handlers over T003 |
| `IClock` port in Core | T002 sec 6 defines `IClock` with `DateTime UtcNow` | VERIFIED | none |
| `IGuidGenerator` port in Core | absent; T002 has `OrderAcceptanceService` calling `Guid.NewGuid()` itself, deliberately | MISMATCH | drop the assumption |
| `AddGastronomyAppInfrastructure(services, dataDirectory)` | absent from T003 | MISMATCH | T005-9: T003 adds it |
| `IDeviceTokenStore.VerifyAsync` returning `Task<Device?>` | T003: `Task<DeviceVerificationResult>` | MISMATCH | T003-10 |
| `DatabaseUnavailableException` in Infrastructure | T003: `InfrastructureException(InfrastructureFailureReason.DatabaseUnavailable, ...)` | MISMATCH | ruling 1, T005-2 |
| The notification handler never writes the order projection | ruling 4 | MISMATCH | T005-4: T005 owns the write on every HTTP path |

### T006 assumptions (against T005, and spec 5/6 as the contract)

| Assumption as T006 spells it | Defining document | Verdict | Resolution |
|---|---|---|---|
| The ten phone REST rows in sec 2.1 | T005 steps 7, 8, 11 and spec 5.2 to 5.4, 5.7 | VERIFIED | none |
| `OrderStatus` is `'Accepted' \| 'NeedsAttention'` | T002 enum: four members; spec 3.1 state table: four | MISMATCH | ruling 5, T006-1 |
| `TicketStatus` eight members | T002 `LocationTicketStatus`: same eight | VERIFIED | none |
| Error envelope `{code, messageKey, parameters, details}`, `details` never for a device | T005 step 2 `ApiError`; spec 5.1 | VERIFIED | none |
| The SignalR events with the payloads in sec 2.4 | spec 6.2; T005 step 12 | VERIFIED | none |
| `GET/PUT /api/admin/table-suggestions`, `.../from-last-session` | T005 sec 7: out of scope | MISMATCH | T006-5 |
| Reconnect intervals 0, 2, 5, 10, 30 then 30s; refetch on reconnect; 15s refetch fallback | spec 6.3 | VERIFIED | none |
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
| `IDeviceTokenStore`, `IEnrolmentInvitationStore` interfaces | T003, T005 | T003, T002-8 |
| `InfrastructureException` | T003, T005 | T003, in Infrastructure, T003-7 |
| `AddGastronomyAppInfrastructure` | T005 | T003, T005-9 |
| `ISessionStateQuery` | T007 | T005, in `GastronomyApp.Api.Hosting`, T005-12 |
| SignalR client wiring on the frontend (`@microsoft/signalr` package) | T006 uses a `HubConnection` | T006, T006-10 |
| The Playwright config and the `test:e2e` script | T001 wrote a config, no script; T006 wrote a second config | T006, T006-7 and T006-8 |
| Deletion of `frontend/src/core/placeholder.ts` and its spec | T001 requires it, T006 never claims it | T006, T006-9 |
| Deletion of `desktop/.../ScaffoldingSmokeTest.cs` | T001 requires it, T007 never claims it | T007, T007-5 |

The four T001 smoke tests, resolved: Core.Tests is deleted by T002 (its step 3, explicit);
Infrastructure.Tests by T003 (its section 3, explicit); Api.Tests by T005 (its section 3);
Desktop.Tests by T007 (T007-5). All four are now claimed exactly once.

**Overlaps, two documents claiming the same work:**

| Overlap | Resolution |
|---|---|
| Order acceptance logic: T002's `OrderAcceptanceService` and T003's `OrderRepository.AcceptAsync` both allocate numbers, create tickets, compute totals | T003-4: T002 owns the use case, T003 owns the transaction and the storage |
| Order status projection write: T005's endpoint handlers and its notification handler | ruling 4: the endpoint handlers write, stated identically in T002 and T005, T002-6 / T005-4 |
| `Directory.Packages.props`: T005 adds SignalR.Client (+ Mvc.Testing, dropped), T007 adds QRCoder, T006 adds an npm package | Three separate, architect-approved exceptions; each document states its own, none removes another's |

---

## 5. Hunt areas, one line each

- **Spelling of every named type, port, enum member, record field and endpoint:** covered in the section
  3 tables; every assumption checked, and every mismatch resolved above.
- **Ownership overlaps:** all resolved in section 4; the acceptance overlap (T002 against
  T003) is the one that would have produced two working but divergent implementations.
- **Orphans:** all assigned in section 4; the SignalR client wiring, the
  Playwright config and the four smoke-test deletions were all genuinely unowned.
- **Sequencing traps:** T003's step 4 needs
  `OrderAcceptanceService`, which is T002's step 6, so T003 cannot start step 4 until T002 is complete
  (steps 0 to 3 of T003 are independent and can start on T002's step 1);
  T006's Playwright spec is written now and stays `test.skip` until T005 lands, which
  T006 already states correctly; T007 needs only T005's step 1 plus T005-12's `ISessionStateQuery`, so it
  can start as soon as the composition entry compiles.
- **Plural forms, T006 against spec 8.5 and 8.1:** T006 step 2 routes every `{count}` key through
  vue-i18n plural forms with the singular written alongside, and knows that the backend and desktop resx
  strings have no plural machinery and split into two keys instead (T007 step 1 carries
  `desktop.phones.one` / `desktop.phones.many`). **Clean.**
- **Verification-command collisions:** none. T002 filters `~GastronomyApp.Core`, T003
  `~GastronomyApp.Infrastructure`, T005 the `Api.Tests` project, T007
  `~GastronomyApp.Desktop.Tests`. The four suites compose into one green
  `dotnet test GastronomyApp.slnx` provided each of the
  four `ScaffoldingSmokeTest.cs` files is deleted exactly once, which section 4 now guarantees. The
  frontend suites (`npm test`, `npm run test:e2e`) sit outside the solution and do not interact.
