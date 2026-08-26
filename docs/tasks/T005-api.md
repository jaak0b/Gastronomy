# T005: GastronomyApp.Api composition, auth, REST and SignalR for the vertical slice

## 1. Objective

Build `GastronomyApp.Api` into a class library that composes a working `WebApplication`: device
bearer authentication, the loopback-only admin gate, the REST endpoints and the SignalR hub that
the vertical slice needs (enrolment, catalog, order placement, order status back to the phone,
station break-glass, and the admin endpoints the slice cannot work without), the error envelope,
rate limiting, and the wiring from the printer worker's callbacks (assumed from T004) into SignalR
pushes and into ticket and order projection writes. Definition of done: `dotnet test
backend/GastronomyApp.Api.Tests` is green with the integration tests listed in section 6, `dotnet
build GastronomyApp.slnx` is zero warnings, and a `WebApplicationFactory`-style test can enrol a
device, fetch the catalog, place an order twice with the same `clientOrderId` and get back two
byte-identical bodies, and see the mock transport's slip files appear on disk.

This task does not invent business rules. Every status code, payload shape, group name, event name
and rule quoted below is copied from `docs/spec.md` sections 5, 6, 2.8, 2.9, 9, 8.5, 11.2 and 11.3.
Where this task must decide something the spec leaves to an implementer (a C# type name, a folder
layout, a middleware shape), that decision is marked **Api decision**.

## 2. Assumed from earlier tasks

T005 is one of six tasks authored in parallel against the same spec, alongside T002 (Core: domain
model, ports, use cases), T003 (Infrastructure: EF Core store, device token hashing, printer
transports), T004 (the printer worker and `PrinterFleet`), T006 (frontend) and T007 (desktop host).
None of those documents exist yet at the time this one is written, so every signature this task
depends on from them is an assumption, not a fact. A consistency review reconciles the seams. Every
assumption below is written so that a reviewer can grep for the exact member name and correct it in
one place if T002, T003 or T004 lands with a different shape.

### Assumed from T002 (`GastronomyApp.Core`)

- Namespace `GastronomyApp.Core.UseCases`, one class per use case, each with a single public method
  named `ExecuteAsync`, taking a request record and a `CancellationToken`, returning a result record
  or throwing a typed exception derived from `GastronomyApp.Core.DomainException` (assumed base
  type, carrying `Code` and `MessageKey` string properties matching the error envelope in section
  5.1). This task calls, and does not implement:
  - `RedeemEnrolmentInvitationUseCase.ExecuteAsync(RedeemEnrolmentInvitationRequest, CancellationToken)`
    returning `RedeemEnrolmentInvitationResult { DeviceId, DeviceToken, ServerPersonId, ServerPersonName, Language }`.
  - `GetCatalogUseCase.ExecuteAsync(CancellationToken)` returning a `CatalogSnapshot` shaped exactly
    like section 5.3's JSON.
  - `PlaceOrderUseCase.ExecuteAsync(PlaceOrderRequest, CancellationToken)` returning
    `PlaceOrderResult { Order, WasAlreadyAccepted }`, where `WasAlreadyAccepted` is what lets this
    task pick 200 versus 201 without re-deriving idempotency logic that section 5.4 already assigns
    to the unique index on `ClientOrderId`. The use case itself performs the `BEGIN IMMEDIATE`
    transaction and the recompute-from-catalog pricing; this task never computes a total.
  - `GetOrdersForPersonUseCase`, `GetOrderByIdUseCase`, `ResolveTicketUseCase`, `ReprintTicketUseCase`,
    each taking the identifiers section 5.4 names and returning the shapes section 5.4 shows.
  - `CreateEnrolmentInvitationUseCase`, item/location/assignment/printer CRUD use cases, and
    `StartEventSessionUseCase`, matching section 5.5's tables one use case per row group.
  - `AcknowledgeStationTicketUseCase.ExecuteAsync(AcknowledgeStationTicketRequest, CancellationToken)`
    implementing the `canAcknowledge` predicate in section 5.6 as the single source of truth; this
    task never re-evaluates that predicate itself and only ever reads the boolean and the reason key
    the use case returns.
  - `IClock`, `IGuidGenerator` ports Core depends on, which this task's composition wires to
    `DateTime.UtcNow`-backed and `Guid.NewGuid`-backed implementations if T003 does not already
    supply them.
- Domain exceptions carry enough for the error envelope: `Code` (matches section 5.1's `code`
  field), `MessageKey`, and `Parameters` (an `IReadOnlyDictionary<string, string>`). An exception
  type maps to one HTTP status by a convention this task defines in section 4 step 2, because T002
  is assumed to not know about HTTP.

### Assumed from T003 (`GastronomyApp.Infrastructure`)

- `GastronomyAppDbContext : DbContext`, constructed from an `IDbContextFactory<GastronomyAppDbContext>`
  or a connection-string-configured `DbContextOptions`, registered by T003's own extension method
  `IServiceCollection AddGastronomyAppInfrastructure(this IServiceCollection services, string
  dataDirectory)` (assumed name). This task calls that one method from its composition root and
  does not construct `DbContext` options itself, so the SQLite file path, `Data Source=:memory:`
  test override, and migration-apply-on-startup behaviour stay owned by T003.
- `IDeviceTokenStore` port with `Task<Device?> VerifyAsync(string tokenLookupId, string secret,
  CancellationToken)` performing the PBKDF2 verification against backend rule 6, returning null for
  an unknown or revoked device. This task's auth handler calls this and this task never touches
  PBKDF2 directly.
- `IPrinterTransport` and `MockPrinterTransport` exist per backend rule 9 and rule 10; this task's
  admin mock-fault endpoint (`POST /api/admin/mock/{locationId}/fault`) is assumed to call a T003
  member `MockPrinterTransport.ArmFault(Guid locationId, MockFault fault, MockFaultMode mode)` or
  an equivalent T004-owned method reached through `IPrinterFleet` (see below); this task does not
  duplicate fault-arming logic and only forwards the call.
- Migrations are produced by T003 against `backend/GastronomyApp.Infrastructure`. This task adds no
  migration and does not reference `Microsoft.EntityFrameworkCore.Design` directly.

### Assumed from T004 (printer worker, `PrinterFleet`)

- `IPrinterFleet` port, registered as a singleton hosted service (T004's own `IHostedService`
  implementation, assumed name `PrinterFleetHostedService`), exposing:
  - `IObservable<TicketStatusChangedNotification>` or, if T004 chose a plainer shape, an event
    `event EventHandler<TicketStatusChangedNotification> TicketStatusChanged` where
    `TicketStatusChangedNotification` carries every field section 6.2's `TicketStatusChanged` row
    lists (`OrderId, GlobalOrderNumber, TicketId, LocationId, LocationName, SequenceNumber, Status,
    FailureReason, PrinterHasPaper, MessageKey, Parameters`).
  - The equivalent notification shape for `PrinterStatusChanged`
    (`LocationId, LocationName, IsOnline, IsPaperEnd, IsPaperNearEnd, IsCoverOpen, IsFaulty,
    WaitingTicketCount, LastDetail`).
  - `Task ReconnectAsync(Guid locationId, CancellationToken)` for `POST
    /api/admin/printers/{locationId}/reconnect`, returning the set of location ids that were
    cleared (section 5.5's "the response names those locations").
  - `Task TestPrintAsync(Guid locationId, CancellationToken)` for the test-print endpoint.
  - **This task assumes T004's worker, not this task, writes `LocationTicket.Status` and
    `PrintJob`/`PrintAttempt` rows, and that `OrderStatusCalculator` (assumed to live in
    `GastronomyApp.Core`, called from within the same transaction per section 3.1) runs inside
    T004's own transaction.** T005's job on receiving a `TicketStatusChanged` notification is
    exactly two things and no more: push the SignalR events named in section 6.2, and, only for the
    endpoints that ask for a fresh read (`GET /api/orders/mine`, `GET
    /api/station/{accessKey}/tickets`), read the already-written projection back out through T002's
    query use cases. **If T004 lands with the write happening in T005 instead, this section is the
    seam to correct**, and this task's own tests (section 6) would need the ticket-and-order write to
    move into the same integration test transaction assertions; until then this task treats the
    projection write as already done by the time its notification handler runs.
  - Order acceptance calls into `IPrinterFleet` to enqueue one `PrintJob` per newly created
    `LocationTicket`, through an assumed `Task EnqueueAsync(Guid ticketId, PrintJobKind kind,
    CancellationToken)`. `PlaceOrderUseCase` (T002) is assumed to call this itself as part of
    accepting the order, in the same unit of work, rather than this task calling it after the use
    case returns; if that assumption is wrong and T002 leaves enqueueing to the caller, this task's
    order endpoint must call `EnqueueAsync` once per ticket in `PlaceOrderResult.Order.Tickets`
    immediately after a 201, and never after a 200 that found an existing order.

Every one of the assumptions above is restated at the call site in section 4 with a comment in this
document (not in code, per root rule 7) naming which assumption it depends on, so a reviewer
reconciling T002/T003/T004 against T005 can find every seam by searching this file for "Assumed
from T0".

## 3. Layout after completion

```
backend/GastronomyApp.Api/
  GastronomyApp.Api.csproj                 (edited: adds SignalR, rate limiting are part of
                                             Microsoft.AspNetCore.App already referenced in T001)
  GastronomyAppApiApplication.cs           (the composition entry, see step 1)
  Options/
    ApiHostOptions.cs
  Auth/
    DeviceAuthenticationHandler.cs
    DeviceAuthenticationSchemeOptions.cs
    LoopbackAdminAuthorizationMiddleware.cs
    StationAccessKeyMiddleware.cs
  ErrorHandling/
    ApiError.cs
    DomainExceptionMiddleware.cs
  RateLimiting/
    RateLimitPolicies.cs
  Endpoints/
    EnrolmentEndpoints.cs
    SessionEndpoints.cs
    CatalogEndpoints.cs
    OrderEndpoints.cs
    PrinterStatusEndpoints.cs
    AdminLocationEndpoints.cs
    AdminItemEndpoints.cs
    AdminServerPeopleEndpoints.cs
    AdminEnrolmentEndpoints.cs
    AdminPrinterEndpoints.cs
    AdminMockFaultEndpoints.cs
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

`ScaffoldingSmokeTest.cs` from T001 is deleted in the same commit that adds the first real fixture
to `GastronomyApp.Api.Tests`, per T001's own instruction and root rule 3.

## 4. Ordered steps

TDD red-first for every step below: write the failing test named in that step, run it, quote the
red output, then write the production code, then quote the green output. Steps are ordered so that
each one's tests can compile against what the previous step built; do not reorder them to "batch"
similar work, because a later step's test depends on an earlier step's endpoint existing.

### Step 0: Preconditions

Confirm T001's scaffold builds (`dotnet build GastronomyApp.slnx`). Confirm `GastronomyApp.Core`
and `GastronomyApp.Infrastructure` contain the members section 2 assumes; if a referenced type or
method does not exist yet because T002 or T003 has not landed, do not stub it inside
`GastronomyApp.Api`. Stop and report which member is missing, because a stub here duplicates a
concept T002/T003 already own (root rule 6) and becomes the interim solution root rule 6 forbids.

Add the NuGet packages this task needs beyond T001's list, to `Directory.Packages.props` and this
project, pinned to the latest stable version at execution time:

```
Microsoft.AspNetCore.SignalR.Client   (test project only, for the hub integration tests)
Microsoft.AspNetCore.Mvc.Testing      (test project only, for WebApplicationFactory)
```

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
it is not a field here.

Write `GastronomyAppApiApplication.cs`. **This is the public signature the desktop host (T007)
calls; no static method, per root rule 1, so the desktop instantiates this class:**

```csharp
namespace GastronomyApp.Api;

public sealed class GastronomyAppApiApplication
{
    public WebApplication Build(ApiHostOptions options, string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls($"http://{options.BindAddress}:{options.Port}");
        // ... service registration, see steps 2-9 below
        var app = builder.Build();
        // ... middleware and endpoint mapping, see steps 2-9 below
        return app;
    }
}
```

`Build` returns the `WebApplication` unstarted. Starting it (`app.RunAsync()`), awaiting shutdown,
and disposing it belong to the caller (T007's desktop host and this task's own
`WebApplicationFactory`-based tests), never to this method, because a composition step that also
runs the server cannot be used by a test that wants to inject a different `IHostEnvironment` or
swap the printer transport for the mock without an actual bound socket. `args` is threaded through
only because `WebApplication.CreateBuilder(string[])` asks for it; this task's tests pass
`Array.Empty<string>()`.

Verify green: `dotnet test backend/GastronomyApp.Api.Tests --filter
FullyQualifiedName~GastronomyAppApiApplicationTest`.

### Step 2: The error envelope and domain exception mapping

Red: `ErrorHandling/ApiErrorTest.cs` (unit test, no host needed) asserting `ApiError` serializes to
exactly the shape in section 5.1:

```json
{ "code": "PrinterOutOfPaper", "messageKey": "ticket.paperEnd", "parameters": { "station": "Küche" }, "details": null }
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

`Details` is populated only by the middleware, and only when the request that produced the error
carried the admin claim from step 4, per section 5.5's "Where the technical detail may and may not
go." Never populated for a device-authenticated or station-key-authenticated caller.

Red: `ErrorHandling/DomainExceptionMiddlewareTest.cs`, an integration test through
`WebApplicationFactory` hitting a throwaway test-only endpoint that throws a known `Core`
`DomainException` subtype, asserting the response status and body. **Api decision**: the mapping
from a `DomainException.Code` to an HTTP status is a fixed table this task owns, because T002 is
assumed to know nothing about HTTP:

| `Code` prefix or exact value | HTTP status |
|---|---|
| `Validation*` (400-shaped: empty lines, quantity out of range, missing table label, missing/duplicate code form) | 400 |
| `Unauthorized`, unknown/revoked token | 401 |
| `Forbidden` (wrong `ServerPersonId` on resolve/reprint/order-by-id) | 403 |
| `NotFound*` | 404 |
| `Conflict*` (duplicate `clientOrderId` with different content, ticket already resolved, invitation outstanding conflicts) | 409 |
| `Gone*` (invitation consumed/expired/replaced) | 410 |
| `UnprocessableEntity*` (unknown item id, missing station choice, six-digit retired, empty `locationIds`) | 422 |
| `DatabaseUnavailable` | 503 |
| anything else | 500, and the response still carries the envelope shape with `code: "InternalError"`, `messageKey: "review.sendFailed"`, `details` populated for admin only |

Write `ErrorHandling/DomainExceptionMiddleware.cs` implementing this table. Root rule 2 (no
silently swallowed errors) binds here directly: the middleware never returns an empty 500, it always
returns the envelope, and it rethrows (crashing the process, which the desktop host's own error
window in section 10.1 is the stated response to) for anything that is not a `DomainException` and
not one of the specific infra failures step 3 below maps, because guessing an envelope for an
unclassified exception would hide a bug behind a message nobody wrote (root rule 2's "a failure that
has no message key is a failure nobody wrote a sentence for").

### Step 3: `DatabaseUnavailable` mapping

Red: `ErrorHandling/DomainExceptionMiddlewareTest.cs`, add a case where the throwaway endpoint
throws the exception type T003 is assumed to throw for a write that cannot complete (assumed
`GastronomyApp.Infrastructure.DatabaseUnavailableException`, unwrapped from whatever ADO.NET/EF
exception T003 catches). Assert 503, `code: "DatabaseUnavailable"`, `messageKey:
"review.sendFailedDatabase"`, per section 5.1.

Green: extend the middleware to catch this specific Infrastructure exception type alongside
`DomainException` and map it through the same table row above. **If T003 does not expose a single
exception type for this and instead throws the raw ADO.NET exception, this is the seam to correct**:
the middleware would then need a T003-supplied predicate (`IDatabaseUnavailableClassifier` or
similar) rather than a `catch` on a concrete Infrastructure type, to avoid this task depending on
SQLite-specific exception shapes that root rule 6 says belong in Infrastructure.

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
        // call deviceTokenStore.VerifyAsync, build a ClaimsPrincipal with
        // ClaimTypes.NameIdentifier = device.ServerPersonId, a "device_id" claim = device.Id,
        // and a "language" claim = device.Language, per section 2.9's "scoped by ServerPersonId"
        // and section 5.4's per-person endpoint scoping.
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

`LastSeenAtUtc` is updated by this handler on every successful authentication, because section 5.5's
admin server-people list (`when it was last seen`) reads it and nothing else in this task's scope
writes it; this task assumes T003's `IDeviceTokenStore.VerifyAsync` performs that write itself as
part of verification (co-locating "verify" and "record seen" avoids a second write path to the same
row), and if T003 lands without it, this handler adds a second call after a successful verify.

### Step 5: Loopback-only admin gate and the station access key

Red: `Auth/LoopbackAdminGateTest.cs`. Using `WebApplicationFactory`'s `TestServer`, which exposes
`HttpContext.Connection.RemoteIpAddress`, assert: a request to `/api/admin/health-check` (a
throwaway probe endpoint under the real admin prefix, deleted once a real admin endpoint exists in
step 8) from `127.0.0.1` succeeds; from an address configured as one of the host's own bound
addresses succeeds; from any other address returns 404, not 401 or 403, per section 5.1. Assert the
served `/admin` page itself (once the frontend's `wwwroot/admin` shell exists; until then assert
only that `/admin` is reachable from every address per "the admin page is served from every
address") returns 200 regardless of caller address, only the `/api/admin/...` prefix is gated.

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
`/station/{accessKey}`, resolving the key against the `ProductionLocation` (or a dedicated access
key store, per T002/T003's actual shape) and attaching the resolved location set to
`HttpContext.Items`, or short-circuiting 404.

### Step 6: Rate limiting

Red: `RateLimiting/RateLimitTest.cs` asserting the 21st `/api/enrolment/redeem` request from one
address inside a minute returns 429 with `messageKey: "review.tooManyRequests"`, and the 601st
device-scoped request from one device inside a minute returns 429, per section 5.1's exact numbers
(20/min per address, 600/min per device).

Green: `RateLimiting/RateLimitPolicies.cs`, using `Microsoft.AspNetCore.RateLimiting`'s
`AddFixedWindowLimiter`, one policy keyed by `HttpContext.Connection.RemoteIpAddress` for the
enrolment endpoint and one keyed by the authenticated device id for every other device-scoped
endpoint. The 429 body still goes through the error envelope from step 2, so the rate limiter's
`OnRejected` callback writes an `ApiError` rather than the framework default body.

### Step 7: Enrolment, session and catalog endpoints (the front half of the slice)

Each endpoint below is its own red-then-green sub-step against `ApiTestFactory` (a
`WebApplicationFactory<GastronomyAppApiApplication>`-shaped test fixture this step creates first,
wiring an in-memory SQLite `GastronomyAppDbContext` per backend rule 4 and the `MockPrinterTransport`
into a temp directory per T003's assumed registration). Write the failing integration test for each
row of the table below before writing the minimal-review endpoint code that makes it pass; do not
write all six endpoints and then all six tests.

| Endpoint | Test asserts |
|---|---|
| `POST /api/enrolment/redeem` | 200 body shape from section 5.2, `deviceToken` present once; 400 both-or-neither code forms; 404 wrong six digits; 410 consumed/expired invitation; 422 after ten wrong six-digit attempts |
| `GET /api/session` | 200 shape from section 5.2 with device auth; 401 without |
| `PUT /api/session/language` | 204, and a following `GET /api/session` reflects the change |
| `GET /api/catalog` | Shape from section 5.3 exactly, sold-out items present with `isAvailable: false`, deactivated items absent |

Write `Endpoints/EnrolmentEndpoints.cs`, `Endpoints/SessionEndpoints.cs`,
`Endpoints/CatalogEndpoints.cs`, each a `static class` with one `MapGroup`-returning extension
method (`public static IEndpointRouteBuilder MapEnrolmentEndpoints(this IEndpointRouteBuilder
routes)`), which is the ASP.NET Core minimal-API convention and falls under the same
framework-metadata-registration exception as Step 4's handler: the method itself holds no state and
is never called except once, from `GastronomyAppApiApplication.Build`, wiring `app.MapGroup(...)`.

Every endpoint method body calls exactly one T002 use case and maps its result or its thrown
`DomainException` through step 2's middleware; no endpoint recomputes a rule the use case already
owns (root rule 6).

### Step 8: Order endpoints, idempotency and printer status (the load-bearing half)

Red first, one test per row:

| Endpoint | Test asserts |
|---|---|
| `POST /api/orders`, first submission | 201, body shape from section 5.4, tickets array with `sequenceNumber` per location |
| `POST /api/orders`, same `clientOrderId` resubmitted | 200, **response body byte-for-byte identical to the original 201 body except the status code**, no second row in `Order` or `LocationTicket`, no second call reaches `IPrinterFleet.EnqueueAsync` |
| `POST /api/orders`, same `clientOrderId`, different body | 409 |
| `POST /api/orders`, unknown item id | 422 |
| `POST /api/orders`, sold-out or deactivated item id present in lines | 201 (accepted, not rejected, per section 5.4) |
| `POST /api/orders`, `expectedTotalCents` absent/zero/wrong | 201, stored total is the backend-computed one, response echoes both totals |
| `GET /api/orders/mine` | Scoped by `ServerPersonId`, not `DeviceId`; a re-enrolled device (same person, new device id) still sees the same list |
| `GET /api/orders/{orderId}` | 404 for a different person's order, 200 for the owner, 200 for an admin caller |
| `POST /api/orders/{orderId}/tickets/{ticketId}/resolve` | 200 moves ticket per body, 403 wrong person, 409 already resolved |
| `POST /api/orders/{orderId}/tickets/{ticketId}/reprint` | 202, 409 if a job is already running for that ticket |
| `GET /api/printers/status` | Shape from section 5.4 |

The byte-for-byte assertion for the 200-versus-201 case is written as a literal string/JSON-document
equality on the two response bodies with the status code and any `Date`-style transport header
excluded from the comparison, not as a looser "same fields" assertion, because section 5.4 states
the rule as "byte for byte the body of the 201 it repeats."

Green: `Endpoints/OrderEndpoints.cs` and `Endpoints/PrinterStatusEndpoints.cs`. The order-creation
handler's only branching is on `PlaceOrderResult.WasAlreadyAccepted` (assumed from T002, section 2)
to pick 200 versus 201; it performs no idempotency check of its own, because section 5.4 assigns
that to the unique index inside the use case's transaction and duplicating it here would be the
second producer of a figure root rule 6 forbids.

### Step 9: Station break-glass endpoints

Red first: `StationEndpointsTest.cs` covering the table in section 5.6 and the `canAcknowledge`
truth table quoted in that section verbatim:

- `GET /station/{accessKey}` returns the SPA shell in station mode (this task returns the same
  `index.html` T006's build produces; asserting only that the response is 200 with `text/html`, not
  asserting frontend content, which is out of this task's scope).
- `GET /api/station/{accessKey}/locations` lists active locations with a per-location can-print flag.
- `GET /api/station/{accessKey}/tickets` lists every ticket whose status is one of `Queued, Blocked,
  Printing, Unknown, Failed` (not `Printed, PrintedOnTestPrinter, HandledOnPaper`), oldest sequence
  number first, each row carrying `canAcknowledge` and the reason key, `ReprintCount`, and every
  field section 5.6 lists ("the slip number, the order number, the table label, every line with its
  quantity and any line note, the order note, the time the order was taken").
- `?locationId=` filters; omitted defaults to the key's own location.
- `POST .../tickets/{ticketId}/acknowledge`: 200 moving to `HandledOnPaper` when `canAcknowledge` was
  true; 409 `station.takeRefused` at a healthy station; 409 for a `Printing` ticket under every one
  of the six station-cannot-print conditions (the test enumerates all six from section 5.6's boxed
  rule: `IsFaulty`, `IsOnline` false, `IsPaperEnd`, `IsCoverOpen`, `IsInErrorState`, `IsEnabled`
  false); 409 `station.alreadyTaken` on a second acknowledgement.
- Unknown or regenerated key: 404 on every one of the above.

Green: `Endpoints/StationEndpoints.cs`. **This endpoint's handler never evaluates
`canAcknowledge` itself**; it calls T002's `AcknowledgeStationTicketUseCase` (or, for the listing
endpoint, a query use case that already stamps `canAcknowledge` per ticket) and only serializes what
comes back, per the assumption in section 2 and per section 5.6's own words ("Each ticket carries
the decision rather than the page recomputing it").

### Step 10: Admin endpoints in scope for the slice

Red first, one test per endpoint group, against the loopback gate from step 5 (every admin test
calls through a `TestServer` client whose `RemoteIpAddress` is set to loopback):

- Locations: `GET/POST /api/admin/locations`, `PUT /api/admin/locations/{id}`, `POST
  .../deactivate`, `POST .../regenerate-access-key`, `GET .../station-card`.
- Items: `GET/POST /api/admin/items`, `PUT /api/admin/items/{id}`, `POST .../availability`, `POST
  .../deactivate`. (CSV import, `POST /api/admin/catalog/import`, is explicitly out of scope, see
  section 7.)
- Assignments: covered by the `locationIds[]` body on the item endpoints above; there is no separate
  assignment endpoint per section 5.5's table.
- Server people and enrolment: `GET /api/admin/server-people`, `PUT .../{id}`, `POST
  .../revoke-device`, `POST .../deactivate`, `POST /api/admin/enrolment/invitations`.
- Printers: `GET /api/admin/printers`, `PUT /api/admin/printers/{locationId}`, `POST
  .../test-print`, `POST .../reconnect`. (`POST /api/admin/printers/discover` is out of scope, see
  section 7.)
- Mock fault: `POST /api/admin/mock/{locationId}/fault`, all seven fault values, both modes, 422 when
  the location's transport is not `Mock`.
- Event session: `GET /api/admin/event-session`, `POST /api/admin/event-session` with every refusal
  guard from section 2.3 the use case is assumed to enforce (this task tests only that the guard's
  409 and its blocking-conditions body reach the caller, not that the guard logic is correct, which
  is T002's own test).
- `GET /api/admin/orders`, `POST .../tickets/{ticketId}/resolve`, `POST .../tickets/{ticketId}/reprint`
  (the laptop-side mirrors of the phone endpoints from step 8).

Green: one `Endpoints/Admin*Endpoints.cs` file per group as laid out in section 3. Every one of
these calls a T002 use case; none re-implements a guard.

`GET /api/admin/orders/{id}/print-history`, `GET /api/admin/export/orders.csv`, `POST
/api/admin/backup`, `GET /api/admin/diagnostics`, `GET /api/admin/log`, `PUT/GET
/api/admin/table-suggestions`, `POST .../from-last-session`, `POST /api/admin/catalog/import`, and
`POST /api/admin/printers/discover` are **not** written in this task. Section 7 restates this.

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
authentication scheme resolved the connection). `Hub/HubNotificationDispatcher.cs` is what step 13
below wires to `IPrinterFleet`'s notifications and to the use cases that produce `OrderAccepted`,
`EnrolmentCompleted`, `DeviceRevoked`, `CatalogChanged`, and `EventSessionStarted`; it is a plain
class (not a static method) holding an `IHubContext<GastronomyHub>` and exposing one push method per
event row in section 6.2, each building the exact payload shape that row lists and sending to the
exact groups that row lists.

The one station group is site-wide (`stations`), not per location, per section 6.1's explicit
statement that keying it by location would be wrong; do not add a per-location group even though
every other group in the table is scoped narrower.

### Step 13: Wiring T004's callbacks into the hub and into pushes

Red: an integration test (folds into `OrderEndpointsTest.cs` or a new
`PrinterCallbackWiringTest.cs`) that places an order against the real `IPrinterFleet` wired to
`MockPrinterTransport`, asserts a slip file appears in the mock's folder for each ticket's location,
and asserts a connected SignalR client in `person:{placingPerson}` receives `TicketStatusChanged`
with `status: "PrintedOnTestPrinter"` (matching section 7.6's transport-split row) without polling,
inside a reasonable timeout.

Green: in `GastronomyAppApiApplication.Build`, after resolving `IPrinterFleet` from the service
provider, subscribe `HubNotificationDispatcher`'s push methods to `IPrinterFleet`'s
`TicketStatusChanged` and `PrinterStatusChanged` notifications. **Per the assumption in section 2,
this subscription only pushes; it never writes `LocationTicket.Status`, `PrintJob` rows, or runs
`OrderStatusCalculator` itself**, because that write is assumed to already be inside T004's own
transaction by the time the notification fires. If the reviewer finds T004 does not write the
projection itself, this is the one place in this task that would need to grow a database write, and
it would need to happen inside the same transaction T004 opens, per root rule 6's "extend the
concept's existing home."

Order acceptance's `OrderAccepted` push happens from the order endpoint itself (step 8), immediately
after a genuine 201 (never after a 200 that found an existing order, because nothing new happened),
not from the printer callback path, since accepting an order and printing its first ticket are two
different events per section 6.2's table.

### Step 14: Full slice integration test

Write (or extend) one test that walks the whole vertical slice in one method, red first as a
compile-only stub, then green once every prior step exists: create an admin location and printer
(Mock transport), create a catalog item assigned to it, create an enrolment invitation, redeem it,
fetch the catalog, place an order, assert the 201 shape, assert a slip file exists, assert
`GET /api/orders/mine` shows it, assert `GET /api/printers/status` reports the location. This is not
a new rule; it is the proof that every earlier step's endpoint composes with every other one.

## 5. Constraints restated

- No static methods or properties anywhere in this task's own code, beyond the ASP.NET Core
  minimal-API extension-method convention and the authentication/hosted-service classes the
  framework instantiates and calls into (root rule 1's framework-metadata-registration exception).
- No empty catch blocks; every failure this task's own code catches either rethrows, maps through
  the error envelope, or is a specific, named, tested case (root rule 2, backend rule 3).
- TDD red-first, every step, with the failing output quoted before production code is written (root
  rule 3). No red proof, no green code.
- No test touches a real database file or a real filesystem path outside `Path.GetTempPath()`
  (backend rule 4). `ApiTestFactory` wires `Data Source=:memory:` and a temp mock-slip folder.
- No positional tuple access anywhere this task writes (backend rule 8).
- No code comments in any file this task writes, including test files and endpoint files (root rule
  7); the "Assumed from T0..." markers required by the brief live in this Markdown document, not in
  source comments.
- Every user-visible string this task's C# code produces (a `messageKey`, never rendered prose) is a
  key, never English or German text baked into a C# string literal that is shown to a client;
  rendering into a language happens on the client per section 5.1, except the slip text, which is
  T004's and T003's concern, not this task's.
- Never the em-dash character, and never a hyphen substituted for it, anywhere in this document or
  in any string, identifier, or commit this task produces.
- `ClientOrderId` idempotency, the loopback 404 (not 403), the `DatabaseUnavailable` 503, the
  `canAcknowledge` truth table, and the exact SignalR group and event tables are copied from the
  spec verbatim; nothing in this task second-guesses a number or a status code the spec states.

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
   Every test named in steps 1 through 14 passes, including: enrolment round trip (redeem by QR
   code form and by six-digit form, both success and every failure status in section 5.2's table);
   auth rejection (missing, malformed, unknown, revoked token); idempotent resubmission over HTTP
   returning a byte-identical body to the original 201; order placement producing mock slip files
   and a SignalR push reaching the placing person's group; break-glass acknowledge returning 409 on
   a healthy station's queued ticket and on every `Printing` ticket regardless of station condition;
   the loopback admin gate returning 404 for a non-laptop address and succeeding for loopback and
   for the laptop's own bound address.

3. ```powershell
   dotnet test GastronomyApp.slnx
   ```
   Full solution still green, confirming this task did not regress T001's or T002's/T003's/T004's
   own test projects (to the extent they exist at review time).

## 7. Out of scope

Explicit, not attempted in this task even where an adjacent endpoint makes it look one line away:

- The desktop window and its hosting of the returned `WebApplication` (T007's job; this task only
  returns the built, unstarted app).
- Any frontend file beyond serving the already-built `wwwroot` folder as static files; no Vue
  component, no admin screen, no phone screen.
- `POST /api/admin/catalog/import` (CSV import), `GET /api/admin/export/orders.csv` (CSV export),
  `GET/PUT /api/admin/table-suggestions` and `POST .../from-last-session` (table suggestion
  seeding), `GET /api/admin/log` (log endpoint), `POST /api/admin/backup` (backup), `GET
  /api/admin/diagnostics`, `GET /api/admin/orders/{id}/print-history`, and `POST
  /api/admin/printers/discover` (printer discovery). Listed here as the later wave the brief
  reserves them for; each is a thin endpoint over a T002/T003 capability once one exists, but none
  is needed for the vertical slice and none is stubbed as a placeholder, per root rule 6's ban on
  interim solutions.
- The Pi agent and `AgentPrinterTransport`; this task only wires `MockPrinterTransport` in its own
  tests and exercises `IPrinterTransport` through the T004 port, never a concrete transport beyond
  the mock.
- Any change to `GastronomyApp.Core` or `GastronomyApp.Infrastructure` source. Where this task finds
  a gap against the assumptions in section 2, it stops and reports the gap rather than adding the
  missing member itself, per root rule 15's project boundary and root rule 6's "extend the existing
  home."
- Real PBKDF2 verification, migrations, or the printer worker's own state machine: all consumed
  through the ports assumed in section 2, never reimplemented here.

## 8. Ambiguities and chosen readings

- **The composition entry's exact class and method name** (`GastronomyAppApiApplication.Build`) is
  not stated anywhere in the spec; section 10.1 only says "It configures and returns the web
  application." Chosen reading: an instance method, per root rule 1, taking `ApiHostOptions` plus
  the process `args` `WebApplication.CreateBuilder` itself wants, returning an unstarted
  `WebApplication`. If T007 needs a different shape (for example, a factory delegate instead of a
  class), reconciling that is this task's own seam to fix, not a change to Core or Infrastructure.
- **Whether `LocationTicket`/`PrintJob` projection writes happen inside T004's transaction or must
  be triggered from this task's notification handler** is the single largest seam against T004
  (section 2's callback assumption). This document takes the reading that keeps section 3.1's rule
  ("`Order.Status` is... written in the same transaction as the ticket change that caused it") true
  without T005 opening a second transaction against a row T004 just wrote, since two writers to one
  projection is exactly what root rule 6 forbids.
- **The station SPA shell response** (`GET /station/{accessKey}`) is treated as "serve `index.html`
  with 200" rather than anything station-mode-specific on the backend, because section 5.6 calls it
  "The single page app shell, in station mode" and station mode is a frontend routing concern (T006)
  once the shell loads, not a different file this task serves.
- **Admin CRUD response shapes not spelled out row-by-row in section 5.5** (for example the exact
  JSON field names on `GET /api/admin/items`) are read as mirroring the corresponding Core domain
  record's own field names, since section 5.5 gives bodies only as `{name, ...}`-shorthand rather
  than full JSON examples the way sections 5.2-5.4 do. This task's tests assert against whatever
  T002's result records actually expose rather than a shape invented independently, so this reading
  cannot drift from Core by construction.
