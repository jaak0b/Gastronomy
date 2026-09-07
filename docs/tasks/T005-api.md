# T005: GastronomyApp.Api composition, auth, REST and SignalR for the vertical slice

This is the original implementation brief, written before any of the code existed. Printing has
since been removed from the product entirely, and every printer instruction has been taken out of
this file. For a current description of the product, read `docs/spec.md`. Where this brief cites a
section number of the specification, it means the older and much longer version of that document. The
specification has since been cut back to purpose, decisions, reasons and the setup checklist, and the
source code is now the authority on every contract.

## 1. Objective

Build `GastronomyApp.Api` into a class library that composes a working `WebApplication`: device
bearer authentication, the loopback-only admin gate, the REST endpoints and the SignalR hub that
the vertical slice needs (enrolment, catalog, order placement, order status back to the phone, the
station screens, and the admin endpoints the slice cannot work without), the error envelope,
rate limiting, and the order and ticket projection writes that this task owns on its own HTTP paths.
Definition of done: `dotnet
test backend/GastronomyApp.Api.Tests` is green with the integration tests listed in section 6,
`dotnet build GastronomyApp.slnx` is zero warnings, and a test can enrol a device, fetch the
catalog, and place an order twice with the same `clientOrderId` and get back two byte-identical
bodies.

This task does not invent business rules. Every status code, payload shape, group name, event name
and rule quoted below is copied from `docs/spec.md` sections 5, 6, 2.8, 2.9, 9, 8.5, 11.2 and 11.3.
Where this task must decide something the spec leaves to an implementer (a C# type name, a folder
layout, a middleware shape), that decision is marked **Api decision**.

This document was rewritten after the cross-task consistency review at
`docs/tasks/consistency-review.md` found that its original "assumed from T002" surface (sixteen
`*UseCase.ExecuteAsync` classes and a `Core.DomainException` base) did not exist in any sibling
document. Every assumption below now cites the reconciliation table in that review's section 3, and
every signature is the one the review settled on, not a guess this task made independently.

## 2. Assumed from earlier tasks

T005 is one of several tasks authored in parallel against the same spec, alongside T002 (Core: domain
model, ports, services), T003 (Infrastructure: EF Core store, device token hashing), T006 (frontend)
and T007 (desktop host).
As of this rewrite none of T002, T003, T006 or T007 has been coded; the signatures below are
the ones the consistency review's reconciliation tables record as the contracts each of those
documents commits to. Every assumption cites the finding or table row it comes from, so a reviewer
correcting a real divergence between this document and the coded T002/T003 can find the exact
seam.

### Assumed from T002 (`GastronomyApp.Core`)

Per the review's section 3 "T005 assumptions (against T002, T003)" table, resolving findings
T005-1 and T005-3:

- **No exception-based domain error channel.** T002's services return `Result<TValue, TFailure>`,
  not a thrown `DomainException`. This task never catches a Core exception for a validation or
  business-rule failure; it always inspects `Result.IsSuccess` and, on failure, the typed
  `TFailure` value.
- `GastronomyApp.Core.Services.OrderAcceptanceService.AcceptAsync(OrderAcceptanceRequest request,
  CancellationToken cancellationToken)` returning `Result<OrderAcceptanceResult,
  OrderValidationFailure>`, where `OrderAcceptanceResult` (namespace `GastronomyApp.Core.Results`,
  `sealed record` with `required init` properties) carries `Order Order` and `bool
  WasAlreadyAccepted`. `WasAlreadyAccepted` is what this task uses to pick 200 versus 201 without
  re-deriving idempotency logic that section 5.4 already assigns to the unique index on
  `ClientOrderId`, inside the service's own transaction. This task never computes a total and never
  allocates a number itself.
- `GastronomyApp.Core.Services.OrderStatusCalculator.Calculate(IReadOnlyCollection<LocationTicketStatus>
  ticketStatuses, bool isPracticeSession)` returning the order's `OrderStatus`. Per the sentence T002
  itself is required to state (review finding T002-6, quoted verbatim so T002 and this task
  say the identical thing): **"Whoever changes a ticket writes the order status projection in the
  same transaction through this calculator: the Api layer does this on every HTTP path that touches
  a ticket (acceptance, resolve, acknowledge). The calculator itself never writes."** This task is
  the writer on every one of those HTTP paths; see steps 8 and 10 below for where each
  write happens. `OrderStatusCalculator` computes; it never opens a transaction and never persists
  anything, so this task's own repository call around it is what commits the write.
- `GastronomyApp.Core.Services.TicketStateMachine.CanTransition(LocationTicketStatus from,
  LocationTicketStatus to)` returning `bool`, called by this task's resolve handler
  before writing a new ticket status, so an illegal transition is refused before the
  projection write in the same call.
- `GastronomyApp.Core.Ports.IClock` with `DateTime UtcNow`, and no `IGuidGenerator` port: T002's
  `OrderAcceptanceService` calls `Guid.NewGuid()` itself, so this task never generates a domain id on
  T002's behalf (the review's table drops the `IGuidGenerator` assumption outright rather than
  resolving it, because nothing needs it).
- Every remaining endpoint this task serves that carries no business rule of its own (catalog read,
  orders read, admin CRUD for locations/items/server-people, enrolment invitation
  creation, event session start's own guard) is **not** behind a T002 use case at all: T002 defines
  no such classes, and the review resolves this by having T005 own those handlers directly, reading
  and writing through T003's repositories and `GastronomyAppDbContext` (review finding T005-3, last
  bullet). This task states that plainly at each such endpoint in step 7 and step 10 below so nobody
  building against this document stubs a non-existent Core class.
- `GastronomyApp.Core.Ports.IOrderRepository` with exactly two members,
  `Task<Order?> FindByClientOrderIdAsync(Guid clientOrderId, CancellationToken cancellationToken)`
  and `Task AddAsync(Order order, CancellationToken cancellationToken)`. This task never calls a
  wider `AcceptAsync`-shaped repository method; T003 implements only these two, and the transaction
  and number allocation live inside `OrderAcceptanceService` and the transaction wrapper T003 owns
  around it (review finding T003-4).

### Assumed from T003 (`GastronomyApp.Infrastructure`)

Per the review's T003 findings and its "T005 assumptions" table:

- `GastronomyApp.Infrastructure.ServiceCollectionExtensions.AddGastronomyAppInfrastructure(this
  IServiceCollection services, string dataDirectory)` (review finding T005-9: T003 adds this; it did
  not exist before the review and this task still only calls the one method, owning none of the
  SQLite path, connection factory or migration-apply-on-startup behaviour behind it).
- `GastronomyApp.Core.Ports.IDeviceTokenStore` (declared by T003 itself, per review finding T002-8,
  not by T002) with:
  - `Task<DeviceVerificationResult> VerifyAsync(string tokenLookupId, string secret,
    CancellationToken cancellationToken)` (review finding T003-10 settles this over this task's
    earlier, wrong assumption of a bare `Task<Device?>`). `DeviceVerificationResult` is assumed to
    distinguish "unknown token" from "revoked device" for the caller; this task's authentication
    handler (step 4) reads whichever discriminated shape T003 lands with and maps both cases to 401,
    since section 5.1 does not require the phone to see the difference.
  - `Task<IssuedDeviceToken> IssueAsync(...)` returning `public sealed record IssuedDeviceToken(Device
    Device, string PlaintextToken)` (review finding T003-9), which is how this task's enrolment
    endpoint gets the one-time `deviceToken` value section 5.2's response carries, since `Device`
    itself never carries the plaintext.
- `GastronomyApp.Core.Ports.IEnrolmentInvitationStore` (declared by T003, review finding T002-8),
  shape read directly off whatever T003 lands with; this task calls it from the enrolment endpoints
  in step 7 and does not re-declare it.
- `GastronomyApp.Infrastructure.InfrastructureException` (review finding T003-7, correcting this
  task's earlier wrong assumption of a Core-namespaced `DatabaseUnavailableException`), constructed
  with an `InfrastructureFailureReason` enum whose `DatabaseUnavailable` member is the one this
  task's single infrastructure-exception middleware (step 3) catches by type and reason.
- `GastronomyAppDbContext`, resolved through DI once `AddGastronomyAppInfrastructure` has run; this
  task's handlers for the use-case-free endpoints named above query and write through it directly
  (or through whatever narrow repository T003 exposes for that entity), never constructing
  `DbContextOptions` themselves.

Every one of the assumptions above is restated at its call site in section 4 with a named review
finding, so a reviewer reconciling a coded T002/T003 against this document can find every seam
by searching this file for "review finding".

## 3. Layout after completion

```
backend/GastronomyApp.Api/
  GastronomyApp.Api.csproj                 (edited: adds SignalR, rate limiting are part of
                                             Microsoft.AspNetCore.App already referenced in T001)
  GastronomyAppApiApplication.cs           (the composition entry, see step 1)
  Options/
    ApiHostOptions.cs
  Hosting/
    ISessionStateQuery.cs
    EventSessionStateQuery.cs
  Auth/
    DeviceAuthenticationHandler.cs
    DeviceAuthenticationSchemeOptions.cs
    LoopbackAdminAuthorizationMiddleware.cs
    StationAccessKeyMiddleware.cs
  ErrorHandling/
    ApiError.cs
    ResultEnvelope.cs
    InfrastructureExceptionMiddleware.cs
  RateLimiting/
    RateLimitPolicies.cs
  Endpoints/
    EnrolmentEndpoints.cs
    SessionEndpoints.cs
    CatalogEndpoints.cs
    OrderEndpoints.cs
    AdminLocationEndpoints.cs
    AdminItemEndpoints.cs
    AdminServerPeopleEndpoints.cs
    AdminEnrolmentEndpoints.cs
    AdminEventSessionEndpoints.cs
    StationEndpoints.cs
    HealthEndpoints.cs
  Hub/
    GastronomyHub.cs
    HubNotificationDispatcher.cs
  wwwroot/                                  (unchanged from T001, filled by the frontend build)
GastronomyApp.Api.Tests/
  ApiTestFactory.cs
  EnrolmentEndpointsTest.cs
  DeviceAuthenticationTest.cs
  CatalogEndpointsTest.cs
  OrderEndpointsTest.cs
  OrderIdempotencyTest.cs
  StationEndpointsTest.cs
  AdminAddressGateTest.cs
  ErrorEnvelopeTest.cs
  HubGroupMembershipTest.cs
```

`ScaffoldingSmokeTest.cs` from T001 is deleted by this task; its own verification step confirms the
file is absent before treating the suite as green.

## 4. Ordered steps

TDD red-first for every step below: write the failing test named in that step, run it, quote the
red output, then write the production code, then quote the green output. Steps are ordered so that
each one's tests can compile against what the previous step built; do not reorder them to "batch"
similar work, because a later step's test depends on an earlier step's endpoint existing.

### Step 0: Preconditions

Confirm T001's scaffold builds (`dotnet build GastronomyApp.slnx`). Confirm `GastronomyApp.Core`
and `GastronomyApp.Infrastructure` contain the members section 2 assumes; if a referenced type or
method does not exist yet, or exists under a different name than the review's reconciliation table
records, do not stub it inside `GastronomyApp.Api`. Stop and report the exact mismatch against
section 2, because a stub here duplicates a concept T002/T003 already own (root rule 6) and
becomes the interim solution root rule 6 forbids.

Add the NuGet package this task needs beyond T001's list, to `Directory.Packages.props` and the test
project, pinned to the latest stable version at execution time:

```
Microsoft.AspNetCore.SignalR.Client   (test project only, for the hub integration tests)
```

This is stated as an explicit, architect-approved exception to T001's no-new-packages constraint,
the same class of exception ruling 7 grants QRCoder in T007 (review finding T005-8). No testing-host
package is added: per review finding T005-7, `WebApplicationFactory<T>` needs an entry-point
assembly with a discoverable `Program`, and `GastronomyApp.Api` deliberately has none (T001's own
decision). `ApiTestFactory` (step 7) builds and starts a `GastronomyAppApiApplication` directly
instead, so `Microsoft.AspNetCore.Mvc.Testing` is never added.

`Microsoft.AspNetCore.SignalR` server-side and `System.Threading.RateLimiting` /
`Microsoft.AspNetCore.RateLimiting` ship inside the `Microsoft.AspNetCore.App` shared framework
already referenced via `FrameworkReference` in T001; do not add them as separate packages.

### Step 1: The composition entry, red first with a smoke test

Write `GastronomyApp.Api.Tests/GastronomyAppApiApplicationTest.cs` asserting that calling the
composition method with an `ApiHostOptions` pointing at a temp directory and port 0 returns a
non-null `WebApplication` whose `Services.GetRequiredService<GastronomyAppDbContext>()` resolves
without throwing. Run it, confirm it fails to compile (the type does not exist yet), which is this
task's red proof for a composition-root step.

Then write `Options/ApiHostOptions.cs`:

```csharp
namespace GastronomyApp.Api.Options;

public sealed record ApiHostOptions
{
    public required string DataDirectory { get; init; }
    public required int Port { get; init; }
    public required string BindAddress { get; init; }
}
```

Three fields only, matching exactly the three settings section 10.1's settings-window table assigns
to the desktop host (port, bind address, data folder); "which network's address is shown" is a
desktop-only display concern over the same bind address and is not a server configuration input, so
it is not a field here. This shape and this namespace are authoritative for the whole product: T007
constructs values of this record and must not redeclare it (review finding T005-6 / T007-2).

Write `GastronomyAppApiApplication.cs`. **This is the public signature the desktop host (T007)
calls. Per the consistency review's ruling 3, `Build` takes no `args` array** (review finding T005-5,
correcting this task's original signature, which threaded a `string[] args` parameter through for no
reason `WebApplicationFactory` or T007 ever needed):

```csharp
namespace GastronomyApp.Api;

public sealed class GastronomyAppApiApplication
{
    public WebApplication Build(ApiHostOptions options)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.WebHost.UseUrls($"http://{options.BindAddress}:{options.Port}");
        // ... service registration, see steps 2-9 and 13 below
        var app = builder.Build();
        // ... middleware and endpoint mapping, see steps 2-9 below
        return app;
    }
}
```

No static method anywhere in this shape, per root rule 1; the desktop host and this task's own test
factory both instantiate `GastronomyAppApiApplication` and call `Build` on it. `Build` returns the
`WebApplication` unstarted. Starting it (`app.StartAsync()`), awaiting shutdown, and disposing it
belong to the caller, never to this method, because a composition step that also runs the server
cannot be used by a test that wants a bound `TestServer` without a real socket, and because T007's
own host launcher (`HostLauncher`) is what owns `StartAsync`/`StopAsync` per the review's T007
findings.

Verify green: `dotnet test backend/GastronomyApp.Api.Tests --filter
FullyQualifiedName~GastronomyAppApiApplicationTest`.

### Step 2: The error envelope and the result-to-envelope mapper

Red: `ErrorHandling/ApiErrorTest.cs` (unit test, no host needed) asserting `ApiError` serializes to
exactly the shape in section 5.1:

```json
{ "code": "DatabaseUnavailable", "messageKey": "review.sendFailedDatabase", "parameters": {}, "details": null }
```

Green: `ErrorHandling/ApiError.cs`:

```csharp
namespace GastronomyApp.Api.ErrorHandling;

public sealed record ApiError
{
    public required string Code { get; init; }
    public required string MessageKey { get; init; }
    public required IReadOnlyDictionary<string, string> Parameters { get; init; }
    public string? Details { get; init; }
}
```

`Details` is populated only when the request that produced the error carried the admin claim from
step 5, per section 5.5's "Where the technical detail may and may not go." Never populated for a
device-authenticated or station-key-authenticated caller.

**There is no exception-based domain error middleware.** Per review finding T005-1, T002's services
return `Result<TValue, TFailure>`, so there is nothing for a global exception-handling middleware to
catch on the validation/business-rule path. Instead this task writes one small, endpoint-layer
mapper, `ErrorHandling/ResultEnvelope.cs`, an ordinary instance class registered in DI and injected
into every endpoint handler that calls into a Core service returning a `Result`:

```csharp
namespace GastronomyApp.Api.ErrorHandling;

public sealed class ResultEnvelope
{
    public IResult ToProblem(OrderValidationFailure failure)
    {
    }

    public IResult ToProblem(RoutingFailure failure)
    {
    }
}
```

`ResultEnvelope` is registered once in `GastronomyAppApiApplication.Build` (`builder.Services
.AddSingleton<ResultEnvelope>()`, since it holds no per-request state) and reaches each endpoint
either through constructor-style delegate injection on the minimal-API handler
(`([FromServices] ResultEnvelope resultEnvelope, ...) => { }`) or through an endpoint filter that
wraps a returned `Result` before the handler's response is written, whichever a given endpoint file
in step 7 through step 10 reads more clearly with; either way it is resolved from the container like
every other collaborator in this task, not called as a static member.

Red: `ErrorHandling/ResultEnvelopeTest.cs`, a unit test per row of the table below, asserting the
exact status and `ApiError.Code`.

Green: implement the table. **Api decision**, since T002 knows nothing about HTTP and assigns no
status codes itself:

| Core failure value | HTTP | `code` |
|---|---|---|
| `OrderValidationFailureReason.NoLines`, `.QuantityOutOfRange`, `.TableLabelMissing`, `.TableLabelTooLong` | 400 | `ValidationFailed` |
| `OrderValidationFailureReason.UnknownCatalogItemId` | 422 | `UnprocessableEntity` |
| `RoutingFailureReason.StationRequired`, `.StationNotAssignedToItem` | 422 | `UnprocessableEntity` |

Every other status row this task needs (401, 403, 404, 409, 410) is not a `Result` failure at all:
it comes from an endpoint-level check (an unknown token, a mismatched `ServerPersonId`, a lookup
that returns null, a re-read that finds the wrong state) written directly in the endpoint handler,
exactly as section 5's tables describe them as facts about the request rather than as a domain rule
Core enforces. This task's endpoint tests (steps 7 through 10) assert each of those directly.

### Step 3: The one infrastructure exception middleware

Red: `ErrorHandling/InfrastructureExceptionMiddlewareTest.cs`, an integration test through a
throwaway test-only endpoint that throws
`new GastronomyApp.Infrastructure.InfrastructureException(InfrastructureFailureReason.DatabaseUnavailable,
...)`, asserting 503, `code: "DatabaseUnavailable"`, `messageKey: "review.sendFailedDatabase"`, per
section 5.1. This corrects this task's earlier, wrong assumption of a Core-namespaced
`DatabaseUnavailableException` (review finding T005-2, T003-7).

Green: `ErrorHandling/InfrastructureExceptionMiddleware.cs`. **This is the one middleware this task
registers for error handling, and it does exactly one thing**: it catches
`GastronomyApp.Infrastructure.InfrastructureException` whose `Reason` is
`InfrastructureFailureReason.DatabaseUnavailable`, maps it to the envelope above, and rethrows every
other exception unchanged. Root rule 2 (no silently swallowed errors) binds directly: an
unclassified exception crashes the process, which the desktop host's own error window in section
10.1 is the stated response to, rather than this task guessing an envelope for a failure nobody
wrote a message key for.

So the reconciled pair this task registers is: **the `ResultEnvelope` mapper (step 2), called
explicitly at the endpoint layer wherever a Core `Result` comes back**, and **the
`InfrastructureExceptionMiddleware` (this step), the one process-wide `UseMiddleware` registration,
which only ever catches `InfrastructureException(DatabaseUnavailable)`**. Nothing else in this task
catches an exception for the purpose of producing an HTTP response.

### Step 4: Device authentication

Red: `Auth/DeviceAuthenticationTest.cs`, an integration test asserting: no `Authorization` header on
a device-scoped endpoint returns 401; a malformed header (no dot) returns 401; a well-formed but
unknown `TokenLookupId` returns 401; a revoked device's token returns 401; a valid token succeeds and
the endpoint can read the resolved `ServerPersonId` and `DeviceId` from `HttpContext.User`.

Green: `Auth/DeviceAuthenticationSchemeOptions.cs` and `Auth/DeviceAuthenticationHandler.cs`
implementing `AuthenticationHandler<DeviceAuthenticationSchemeOptions>`:

```csharp
namespace GastronomyApp.Api.Auth;

public sealed class DeviceAuthenticationHandler(
    IOptionsMonitor<DeviceAuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IDeviceTokenStore deviceTokenStore)
    : AuthenticationHandler<DeviceAuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // parse "Bearer <TokenLookupId>.<secret>", split on the dot exactly once,
        // call deviceTokenStore.VerifyAsync, which per T003 returns a DeviceVerificationResult
        // distinguishing "unknown" from "revoked"; both fail authentication with 401 here,
        // because section 5.1 does not require the phone to see the difference.
        // On success, build a ClaimsPrincipal with ClaimTypes.NameIdentifier = device.ServerPersonId,
        // a "device_id" claim = device.Id, and a "language" claim = device.Language, per section 2.9's
        // "scoped by ServerPersonId" and section 5.4's per-person endpoint scoping.
    }
}
```

This is not a static method: it is an instance registered per-request by the authentication
middleware, which is the framework-metadata-registration exception in root backend rule 1 (the
handler's constructor and lifecycle are entirely framework-owned).

`access_token` query parameter support for the hub (section 6, "passed as an `access_token` query
parameter, which is the transport SignalR supports for WebSockets") is wired via
`OnMessageReceived` on the same scheme's events, reading `context.Request.Query["access_token"]`
when the path starts with `/hub` and no `Authorization` header is present, and treating it exactly
like a bearer token from there.

`LastSeenAtUtc` is updated by `IDeviceTokenStore.VerifyAsync` itself as part of verification, per
this task's reading of T003's ownership of that store (co-locating "verify" and "record seen" avoids
a second write path to the same row); if T003 lands without that write, this handler adds a second
call after a successful verify, and this paragraph is the seam to correct.

### Step 5: Loopback-only admin gate and the station access key

Red: `Auth/LoopbackAdminGateTest.cs`. Using an in-process `TestServer` (constructed by
`ApiTestFactory`, step 7), which exposes `HttpContext.Connection.RemoteIpAddress`, assert: a request
to `/api/admin/health-check` (a throwaway probe endpoint under the real admin prefix, deleted once a
real admin endpoint exists in step 10) from `127.0.0.1` succeeds; from an address configured as one
of the host's own bound addresses succeeds; from any other address returns 404, not 401 or 403, per
section 5.1. Assert the served `/admin` page itself returns 200 regardless of caller address, only
the `/api/admin/...` prefix is gated.

Green: `Auth/LoopbackAdminAuthorizationMiddleware.cs`, registered before endpoint routing, matching
only paths starting with `/api/admin/`, checking `IsLoopback` or membership in the set of addresses
the process is bound to (read from `IServer.Features.Get<IServerAddressesFeature>()` or, for the
0.0.0.0 wildcard bind case, every non-loopback IPv4 address `NetworkInterface.GetAllNetworkInterfaces()`
reports, matching section 5.5's "the laptop's own bound addresses"), and short-circuiting to 404
otherwise. **Api decision**: this runs as `IMiddleware`, not as an `IAuthorizationRequirement`,
because a 404 has to happen before model binding or Problem Details formatting could turn it into a
different status, and because section 5.1 is explicit that this is a routing-level fact ("Requests
to admin paths from any other address get 404") rather than an authorization failure.

Red: `Auth/StationAccessKeyTest.cs` asserting a request to `/api/station/{accessKey}/tickets` with
an unknown or regenerated key returns 404 per section 5.6's closing paragraph, and a valid key
resolves the location set for `GET .../locations` and the requested-or-default location for
`.../tickets`.

Green: `Auth/StationAccessKeyMiddleware.cs`, matching `/api/station/{accessKey}` and
`/station/{accessKey}`, resolving the key against the location's access key row through T003's
`GastronomyAppDbContext`, or short-circuiting 404.

### Step 6: Rate limiting

Red: `RateLimiting/RateLimitTest.cs` asserting the 21st `/api/enrolment/redeem` request from one
address inside a minute returns 429 with `messageKey: "review.tooManyRequests"`, and the 601st
device-scoped request from one device inside a minute returns 429, per section 5.1's exact numbers
(20/min per address, 600/min per device).

Green: `RateLimiting/RateLimitPolicies.cs`, using `Microsoft.AspNetCore.RateLimiting`'s
`AddFixedWindowLimiter`, one policy keyed by `HttpContext.Connection.RemoteIpAddress` for the
enrolment endpoint and one keyed by the authenticated device id for every other device-scoped
endpoint. The 429 body still goes through `ApiError` from step 2, so the rate limiter's `OnRejected`
callback writes an `ApiError` rather than the framework default body.

### Step 7: Enrolment, session and catalog endpoints (the front half of the slice)

Write `GastronomyApp.Api.Tests/ApiTestFactory.cs` first, since every remaining step's tests depend on
it. Per review finding T005-7, this is not a `WebApplicationFactory<T>`, because `GastronomyApp.Api`
has no entry-point assembly by design (T001's own decision). `ApiTestFactory` instead calls `new
GastronomyAppApiApplication().Build(options)` directly against a temp data directory and port 0, then
either awaits `app.StartAsync()` and uses `app.GetTestClient()`, or wires a `TestServer` over the
same `IServiceProvider` and `IApplicationBuilder` pipeline, whichever the built-in ASP.NET Core test
host APIs make simpler once this step is coded; either way the factory never touches
`Microsoft.AspNetCore.Mvc.Testing`.

Each endpoint below is its own red-then-green sub-step against `ApiTestFactory`. Write the failing
integration test for each row of the table below before writing the minimal endpoint code that makes
it pass; do not write all four endpoints and then all four tests.

| Endpoint | Test asserts |
|---|---|
| `POST /api/enrolment/redeem` | 200 body shape from section 5.2, `deviceToken` present once (from `IssuedDeviceToken.PlaintextToken`); 400 both-or-neither code forms; 404 wrong six digits; 410 consumed/expired invitation; 422 after ten wrong six-digit attempts |
| `GET /api/session` | 200 shape from section 5.2 with device auth; 401 without |
| `PUT /api/session/language` | 204, and a following `GET /api/session` reflects the change |
| `GET /api/catalog` | Shape from section 5.3 exactly, sold-out items present with `isAvailable: false`, deactivated items absent |

Write `Endpoints/EnrolmentEndpoints.cs`, `Endpoints/SessionEndpoints.cs`,
`Endpoints/CatalogEndpoints.cs`, each a `static class` with one `MapGroup`-returning extension
method (`public static IEndpointRouteBuilder MapEnrolmentEndpoints(this IEndpointRouteBuilder
routes)`), which is the ASP.NET Core minimal-API convention and falls under the same
framework-metadata-registration exception as step 4's handler: the method itself holds no state and
is never called except once, from `GastronomyAppApiApplication.Build`, wiring `app.MapGroup(...)`.

These four endpoints carry no Core service call for a business rule beyond what `IEnrolmentInvitationStore`
and `IDeviceTokenStore` (both T003) already enforce for redemption; the catalog and session reads are
plain queries through T003's `DbContext`, per section 2's statement that this task owns
use-case-free endpoints directly rather than stubbing a non-existent Core class for them.

### Step 8: Order endpoints, idempotency and projection writes

Red first, one test per row:

| Endpoint | Test asserts |
|---|---|
| `POST /api/orders`, first submission | 201, body shape from section 5.4, tickets array with `sequenceNumber` per location; the order's projected status is written by this endpoint through `OrderStatusCalculator` in the same transaction as the accepting write |
| `POST /api/orders`, same `clientOrderId` resubmitted | 200, **response body byte-for-byte identical to the original 201 body except the status code**, no second row in `Order` or `LocationTicket` |
| `POST /api/orders`, same `clientOrderId`, different body | 409 |
| `POST /api/orders`, unknown item id | 422, via `ResultEnvelope.ToProblem` on `OrderValidationFailureReason.UnknownCatalogItemId` |
| `POST /api/orders`, sold-out or deactivated item id present in lines | 201 (accepted, not rejected, per section 5.4) |
| `POST /api/orders`, `expectedTotalCents` absent/zero/wrong | 201, stored total is the backend-computed one, response echoes both totals |

The byte-for-byte assertion for the 200-versus-201 case is written as a literal string/JSON-document
equality on the two response bodies with the status code and any `Date`-style transport header
excluded from the comparison, not as a looser "same fields" assertion, because section 5.4 states
the rule as "byte for byte the body of the 201 it repeats."

Green: `Endpoints/OrderEndpoints.cs`. The order-creation handler:

1. Calls `OrderAcceptanceService.AcceptAsync`, branching only on `Result.IsSuccess` (mapping a
   failure through `ResultEnvelope.ToProblem`) and, on success, on
   `OrderAcceptanceResult.WasAlreadyAccepted` to pick 200 versus 201.
2. **Only on a genuine 201** (`WasAlreadyAccepted == false`): writes the order's projected status
   through `OrderStatusCalculator.Calculate`, in the same repository call that persisted the
   acceptance, per the sentence quoted in section 2 against review finding T002-6. A 200 does not,
   because nothing new happened.

This handler performs no idempotency check of its own beyond reading `WasAlreadyAccepted`, because
section 5.4 assigns that check to the unique index inside `OrderAcceptanceService`'s own transaction
and duplicating it here would be the second producer of a figure root rule 6 forbids.

The resolve handler in step 10 follows the same two-part shape: call the Core state machine that
decides whether the write is legal, then, only if it is,
write the new ticket status and recompute-and-write the order's projected status through
`OrderStatusCalculator` in one transaction, before pushing anything over SignalR.

### Step 9: Station endpoints

Red first: `StationEndpointsTest.cs` covering the table in section 5.6:

- `GET /station/{accessKey}` returns the SPA shell in station mode (this task returns the same
  `index.html` T006's build produces; asserting only that the response is 200 with `text/html`, not
  asserting frontend content, which is out of this task's scope).
- `GET /api/station/{accessKey}/locations` lists active locations.
- `GET /api/station/{accessKey}/tickets` lists the station's open tickets, oldest sequence
  number first, each row carrying every field section 5.6
  lists ("the ticket number, the order number, the table label, every line with its quantity and any
  line note, the order note, the time the order was taken").
- `?locationId=` filters; omitted defaults to the key's own location.
- Unknown or regenerated key: 404 on every one of the above.

Green: `Endpoints/StationEndpoints.cs`.

### Step 10: Admin endpoints in scope for the slice

Red first, one test per endpoint group, against the loopback gate from step 5 (every admin test
calls through the test factory's client with `RemoteIpAddress` set to loopback):

- Locations: `GET/POST /api/admin/locations`, `PUT /api/admin/locations/{id}`, `POST
  .../deactivate`, `POST .../regenerate-access-key`, `GET .../station-card`.
- Items: `GET/POST /api/admin/items`, `PUT /api/admin/items/{id}`, `POST .../availability`, `POST
  .../deactivate`. (CSV import, `POST /api/admin/catalog/import`, is explicitly out of scope, see
  section 7.)
- Assignments: covered by the `locationIds[]` body on the item endpoints above; there is no separate
  assignment endpoint per section 5.5's table.
- Server people and enrolment: `GET /api/admin/server-people`, `PUT .../{id}`, `POST
  .../revoke-device`, `POST .../deactivate`, `POST /api/admin/enrolment/invitations`.
- Event session: `GET /api/admin/event-session`, `POST /api/admin/event-session` with every refusal
  guard from section 2.3 that T002's own event-session start service is assumed to enforce (this
  task tests only that the guard's 409 and its blocking-conditions body reach the caller, not that
  the guard logic is correct, which is T002's own test).
- `GET /api/admin/orders`, `POST .../tickets/{ticketId}/resolve`. Resolve moves the ticket per body
  via `TicketStateMachine.CanTransition` and then writes the order's
  projected status through `OrderStatusCalculator` in the same transaction, exactly as the order
  endpoints in step 8 do.

Green: one `Endpoints/Admin*Endpoints.cs` file per group as laid out in section 3. None of these
re-implements a guard T002 or T003 already owns; the endpoints that touch no business rule (plain
CRUD for locations, items, server people) are, per section 2, this task's own handlers directly over
T003's repositories, stated so a coder does not look for a non-existent Core use case for them.

`GET /api/admin/export/orders.csv`, `POST
/api/admin/backup`, `GET /api/admin/diagnostics`, `GET /api/admin/log`, `PUT/GET
/api/admin/table-suggestions`, `POST .../from-last-session`, and `POST /api/admin/catalog/import`
are **not** written in this task. Section 7 restates this.

### Step 11: Health

Red: `HealthEndpointsTest.cs` asserting `GET /api/health` is reachable without any auth and any
address, returning the shape in section 5.7.

Green: `Endpoints/HealthEndpoints.cs`.

### Step 12: The SignalR hub

Red: `Hub/GastronomyHubTest.cs`, connecting a `HubConnection` (from the test-only
`Microsoft.AspNetCore.SignalR.Client` package) with a valid device token as `access_token`, and
asserting group membership by triggering an action that only reaches one group and asserting the
connected client receives it while a second connection in a different group does not. One test per
row of section 6.1's group table, and one test per row of section 6.2's event table (payload shape
and audience). Include the two structural tests from section 11.2's SignalR row explicitly: an event
reaches exactly the groups listed and no others, and revoking a device removes its connection from
every group and aborts it in the same transaction that sets `RevokedAtUtc`.

Green: `Hub/GastronomyHub.cs` (`Hub` subclass, `OnConnectedAsync` adds the caller to
`person:{serverPersonId}`, `device:{deviceId}`, `devices`, `admin` or `stations` depending on which
authentication scheme resolved the connection).

`Hub/HubNotificationDispatcher.cs` is a plain class (not a static method) holding an
`IHubContext<GastronomyHub>` and exposing one push method per event row in section 6.2
(`TicketStatusChanged`,
`EnrolmentCompleted`, `DeviceRevoked`, `CatalogChanged`, `EventSessionStarted`), each called directly
from the endpoint handler that produces that event (ticket resolution, enrolment
redemption, device
revocation, item availability/deactivation, event session start), building the exact payload shape
that row lists and pushing it to the exact groups section 6.2's table names.

The one station group is site-wide (`stations`), not per location, per section 6.1's explicit
statement that keying it by location would be wrong; do not add a per-location group even though
every other group in the table is scoped narrower.

### Step 13: `ISessionStateQuery`

Write, red first (`Hosting/EventSessionStateQueryTest.cs`), then green,
`Hosting/ISessionStateQuery.cs` and `Hosting/EventSessionStateQuery.cs`:

```csharp
namespace GastronomyApp.Api.Hosting;

public interface ISessionStateQuery
{
    Task<bool> IsSessionActiveAsync(CancellationToken cancellationToken);
}
```

backed by a read of the active `EventSession` through T003's `DbContext`. Per review finding
T005-12, this interface is owned by this task (namespace `GastronomyApp.Api.Hosting`, not
`GastronomyApp.Core.Sessions` as an earlier draft of this document and of T007 both assumed),
because T007 resolves it from `app.Services` on the returned `WebApplication` and never needs to see
`GastronomyApp.Core` for it, and because it needs no domain service, only a read. Register it in
`Build` alongside every other service this task composes.

## 5. Constraints restated

- No static methods or properties anywhere in this task's own code, beyond the ASP.NET Core
  minimal-API extension-method convention, the authentication/hosted-service classes the framework
  instantiates and calls into, and the two pure `ResultEnvelope.ToProblem` overloads named in step 2
  and stated there as a deliberate, narrow exception (root rule 1's framework-metadata-registration
  exception, plus that one recorded addition).
- No empty catch blocks; the one `InfrastructureExceptionMiddleware` (step 3) is the only place this
  task's own code catches an exception for the purpose of producing an HTTP response, and it
  rethrows everything it does not recognise (root rule 2, backend rule 3).
- TDD red-first, every step, with the failing output quoted before production code is written (root
  rule 3). No red proof, no green code.
- No test touches a real database file or a real filesystem path outside `Path.GetTempPath()`
  (backend rule 4). `ApiTestFactory` wires `Data Source=:memory:`.
- No positional tuple access anywhere this task writes (backend rule 8).
- No code comments in any file this task writes, including test files and endpoint files (root rule
  7); the "Assumed from T0..." markers and "review finding" citations required by the brief live in
  this Markdown document, not in source comments.
- Every user-visible string this task's C# code produces (a `messageKey`, never rendered prose) is a
  key, never English or German text baked into a C# string literal that is shown to a client;
  rendering into a language happens on the client per section 5.1.
- Never the em-dash character, and never a hyphen substituted for it, anywhere in this document or
  in any string, identifier, or commit this task produces.
- `ClientOrderId` idempotency, the loopback 404 (not 403), the `DatabaseUnavailable` 503, and the
  exact SignalR group and event tables are copied from the
  spec verbatim; nothing in this task second-guesses a number or a status code the spec states.
- The order status projection is written in exactly one place per changing path: this task's own
  code on acceptance and resolve, through the
  one `OrderStatusCalculator`, per review finding T002-6 quoted in section 2. This task never leaves
  one of its own paths unwritten.

## 6. Verification

Run from the repository root, quote real output:

1. ```powershell
   dotnet build GastronomyApp.slnx
   ```
   Zero warnings, zero errors, across every project including `GastronomyApp.Api` and
   `GastronomyApp.Api.Tests`.

2. ```powershell
   dotnet test backend/GastronomyApp.Api.Tests
   ```
   Every test named in steps 1 through 13 passes, including: enrolment round trip (redeem by QR
   code form and by six-digit form, both success and every failure status in section 5.2's table);
   auth rejection (missing, malformed, unknown, revoked token); idempotent resubmission over HTTP
   returning a byte-identical body to the original 201; order placement writing the order's
   projected status through `OrderStatusCalculator` on the accepting path and a SignalR push
   reaching the placing person's group;
   the loopback admin gate returning 404 for a non-laptop address and succeeding for loopback and
   for the laptop's own bound address; the throwaway `/api/admin/health-check` probe endpoint from
   step 5 is confirmed absent (deleted in step 10, per review finding T005-13).

3. ```powershell
   dotnet test GastronomyApp.slnx
   ```
   Full solution still green, confirming this task did not regress T001's or T002's or T003's
   own test projects (to the extent they exist at review time), and that
   `backend/GastronomyApp.Api.Tests/ScaffoldingSmokeTest.cs` is absent.

## 7. Out of scope

Explicit, not attempted in this task even where an adjacent endpoint makes it look one line away:

- The desktop window and its hosting of the returned `WebApplication` (T007's job; this task only
  returns the built, unstarted app).
- Any frontend file beyond serving the already-built `wwwroot` folder as static files; no Vue
  component, no admin screen, no phone screen.
- `POST /api/admin/catalog/import` (CSV import), `GET /api/admin/export/orders.csv` (CSV export),
  `GET/PUT /api/admin/table-suggestions` and `POST .../from-last-session` (table suggestion
  seeding), `GET /api/admin/log` (log endpoint), `POST /api/admin/backup` (backup), and `GET
  /api/admin/diagnostics`. Listed here as the later wave the brief
  reserves them for; each is a thin endpoint over a T002/T003 capability once one exists, but none
  is needed for the vertical slice and none is stubbed as a placeholder, per root rule 6's ban on
  interim solutions. The consistency review confirms T006 resolves its own dependency on the
  table-suggestions screen by dropping it from its slice rather than by
  this task adding the endpoint (review finding T006-5); this task's scope is unchanged by
  that resolution.
- Every entity, port and service named in section 2 belongs to T002 or
  T003. Where this task finds a gap against section 2's assumptions, it stops and reports the gap
  rather than adding the missing member itself, per root rule 15's project boundary and root rule
  6's "extend the existing home."
- Real PBKDF2 verification and migrations: both consumed
  through the ports assumed in section 2, never reimplemented here.

## 8. Ambiguities and chosen readings

- **The composition entry's exact class and method name** (`GastronomyAppApiApplication.Build`) is
  ratified by the consistency review's ruling 3, including dropping the `args` parameter this
  document originally carried. T007 adopts this signature without modification; there is no longer
  an open question at this seam.
- **Where the `LocationTicket` projection writes happen** is resolved by ruling 4 and stated
  identically in T002 and this document (section 2's quoted sentence): this task
  writes on its own HTTP paths, through the one
  `OrderStatusCalculator`. There is no longer a seam here to reconcile; steps 8 and 10 name where
  each of this task's writes happens.
- **The station SPA shell response** (`GET /station/{accessKey}`) is treated as "serve `index.html`
  with 200" rather than anything station-mode-specific on the backend, because section 5.6 calls it
  "The single page app shell, in station mode" and station mode is a frontend routing concern (T006)
  once the shell loads, not a different file this task serves.
- **Admin CRUD response shapes not spelled out row-by-row in section 5.5** (for example the exact
  JSON field names on `GET /api/admin/items`) are read as mirroring the corresponding Core entity's
  own field names, since section 5.5 gives bodies only as `{name, ...}`-shorthand rather than full
  JSON examples the way sections 5.2-5.4 do. This task's tests assert against whatever T002's
  entities and T003's repositories actually expose rather than a shape invented independently, so
  this reading cannot drift from Core or Infrastructure by construction.
