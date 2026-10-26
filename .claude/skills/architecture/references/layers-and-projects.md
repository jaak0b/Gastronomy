# How the projects are layered

## Decision

The product is one executable on one laptop. Three source trees ship together: the frontend build is
embedded, the Api library is hosted in the same process, and the operator double-clicks the desktop
app. Nothing is installed besides it: no runtime, no separate web server, no reverse proxy, no
separate deployment of the frontend and no CDN.

| Folder | Role |
|---|---|
| `backend/` | .NET 10 class libraries. `GastronomyApp.Contracts` holds the wire records, `GastronomyApp.Api` configures the ASP.NET Core web application (REST, SignalR, SQLite, static frontend) and is hosted by the desktop app. |
| `desktop/` | `GastronomyApp.Desktop`, the **only executable**: an Avalonia window that hosts the web application in-process. A launcher and a status light, never a second admin UI. |
| `frontend/` | Vue 3 + TypeScript + Vite + Pinia: the waiter's phone app, the station tablet page and the admin. Built into `backend/GastronomyApp.Api/wwwroot` and embedded in the executable, so the laptop serves it. |

## Backend

ASP.NET Core on .NET 10. `GastronomyApp.Api` is a class library that configures and returns the web
application, and the desktop app hosts it in its own process. That one process serves the REST API,
the SignalR hub and the built frontend from `wwwroot`, on one port.

- **SQLite through EF Core**, in one file. A backup is a file copy, which is what a volunteer can
  actually do.
- **SignalR** for every push from the laptop: the configuration changed, the orders changed, a
  device enrolled or was revoked. Clients never poll for state the laptop already knows has changed;
  the `announcements` page holds the mechanism.
- **REST** for commands and queries.

| Project | Role |
|---|---|
| `GastronomyApp.Contracts` | The wire records the API receives and sends, grouped into folders by area (`Admin/Catalog`, `Admin/Festivals`, `Admin/Ingredients`, `Admin/Staff`, `Admin/Stations`, `Admin/Units`, `Enrolment`, `Orders`, `OpenItems`, `Catalog`, `Session`, `Stations`, `TablePlan`, `Events`, `Validation`), each folder its own sub-namespace under `GastronomyApp.Contracts`. `ApiError`, `ProblemDescription` and `Enums/` stay at the root, and `Validation/` holds the shared refusal message keys and the `RequiredText` attribute. References nothing; every other project references it. |
| `GastronomyApp.Core` | Entities, enums, one service per area, the database interface `IGastronomyDatabase`, the shared query pieces in `Queries`, the refusals in `Refusals` and the ports the other projects implement. References `Contracts`, EF Core and Mapster. It never sees the concrete DbContext, SQLite, ASP.NET Core or SignalR. |
| `GastronomyApp.Infrastructure` | References `Core` and `Contracts`. The DbContext that implements `IGastronomyDatabase`, SQLite, the migrations, the entity configurations, the save interceptor that derives the announcements and the hashing of device tokens and enrolment codes (`Pbkdf2SecretHasher` behind `ISecretHasher`). |
| `GastronomyApp.Api` | Class library: REST endpoints, SignalR hub, authentication, static frontend, hosting and the composition root. Names shared across the Api (authentication schemes, device claim types, hub events, hub groups, rate limit policies) are `public const string` members of nested static classes inside `GastronomyApp.Api.Names`, never injected instances. It never touches the DbContext outside the composition root. Hosted by `GastronomyApp.Desktop`. |
| `*.Tests` | Unit tests against Core, integration tests against Infrastructure and the API. `Microsoft.Extensions.TimeProvider.Testing`'s `FakeTimeProvider` stands in for `System.TimeProvider` wherever a test needs to control the clock. |

The dependencies point inward: `Contracts` <- `Core` <- `Infrastructure` <- `Api`. The reasons for the
layering and the checks a file must pass are in the C# skill, section "Clean architecture layering"
of `structure.md`; this page does not repeat them.

Core logs the outcome of what it did through `Microsoft.Extensions.Logging.Abstractions`
(`ILogger<T>` in the constructor): an enrolment invitation created, an invitation redeemed, a
handed-over device signed out, items settled.

## The database interface

`IGastronomyDatabase` is one interface in `Core` with a `DbSet<T>` property per table, `Set<T>()` for
the shared actions that work over any entity with a given flag, and `SaveChangesAsync`. The DbContext in `Infrastructure` implements it. EF Core is itself the
abstraction over the database, so a repository in front of it only repeated its API one method per
caller, and every new question meant a new method and a new pass-through. A service in `Core` asks
its question to `IGastronomyDatabase` directly with LINQ, and the database engine stays in
`Infrastructure` where the interface is implemented.

## One service per area

Each area has one service in `Core/Services`, named `<Area>Service` (`IngredientService`,
`UnitService`, `OrderService`). It holds every action of that area, its reads and its writes. A rule
that several actions of the area share is a private method of that one class, so nothing is copied
between actions. Two services for one area are merged into one when the area is converted. A service
is split only when it grows too large, and then by sub-topic (placing an order and settling it, for
example), never into one class per action.

The endpoint in `Api` calls the service directly. There is no handler class between them and no
per-area mapping file in `Api`, because each job a handler did has one home:

- **Mapping an entity to a record** happens in the service: `ProjectToType<TView>()` for a read,
  Mapster on the saved entity for the answer of a write. Mapster's configuration for an area is one
  `IRegister` class in `Core/Mapping` (`IngredientMapping`), found by a scan of the Core assembly
  when `MappingConfiguration` builds the configuration; it holds only what Mapster cannot infer from
  names, such as a filter written as a query expression.
- **Turning `ErrorOr` into the HTTP answer** is written once, centrally, for all endpoints, in the
  answer types of `Api/Answers`.
- **The calling device's identity** stays an endpoint parameter and is handed to the service as a plain
  parameter.

A business rule lives in the service and never in an entity class, which carries only data and
navigation properties.

- **A read** queries `IGastronomyDatabase` with `AsNoTracking` and projects with Mapster
  `ProjectToType<TView>()` into the `Contracts` record, so the mapping is configured once per record
  and turns into the SQL query. It never adds or removes a row. There is no repository for reads.
- **A write** loads through `IGastronomyDatabase`, applies its rules and saves. The request filter
  commits, and the save interceptor announces what was written (the `one-transaction-per-request` and
  `announcements` pages).
- **A repository** exists only where loading the data for a write is complex and the same load is
  repeated across actions, such as an order with its items or a table with its orders. It is never one per
  entity and never one method per caller.
- **An endpoint the frontend does not call** does not exist. When an area is converted, every such
  endpoint is removed completely.

The steps are on the `add-a-read-to-a-service` and `add-a-write-to-a-service` pages of `how-to`.

## Shared query pieces

A filter or an ordering that several actions need is an `IQueryable` extension method in
`Core/Queries`, extracted when it is needed a second time. The rules, the interfaces behind them and
how they are tested are on the `query-building-blocks` page.

## Switching a row on and off

Turning a row back on is the same for most entities that carry the flag: find it, refuse when it
does not exist, set the flag, save. An entity whose activation has rules of its own (an article
refuses while its category is switched off or its recipe holds a deactivated ingredient) keeps its
activate in its area's service. The rule-free case is written once, in `ActivationService` in `Core/Services`,
as one generic action over `IActivatable` (activate) and one over `IHideable` (show), with a second show over `IHideable` and `IFestivalScoped` for a row that belongs to a festival of the route (area, table). Turning a row
off is not shared: each entity refuses for its own reason (a unit an ingredient still uses, a station
with items not yet handed out, an area with open items), so deactivate and hide stay actions of the
area's own service. The one exception is the flow that area and table hide identically (find inside the festival, nothing to do when already hidden, refuse only while the festival runs and items are still owed), which is `FestivalRowHiding` and takes each one's item filter and refusals.

The routes stay one pair per entity, so the generated types name every operation and a refusal
belongs to a route a reader can find. The frontend's shared admin list offers only the operations
whose route exists for that list.

## Conversion in progress

Every area is converted to this structure; open items and settling were the last. New code is
written the way this page describes. `IFestivalRepository` is the one repository left: it serves
`RunningFestivalLookup` and the desktop's `HostFestivalReader`.

## Frontend

The frontend's own layering (a framework-free `core/` under every surface) is frontend hard rule 1.
How the wire records reach it is the `the-wire-boundary` page.
