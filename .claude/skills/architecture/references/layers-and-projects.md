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
| `GastronomyApp.Contracts` | The wire records the API receives and sends, grouped into folders by area (`Admin/Catalog`, `Admin/Festivals`, `Admin/Ingredients`, `Admin/Staff`, `Admin/Stations`, `Admin/Units`, `Enrolment`, `Orders`, `OpenItems`, `Catalog`, `Session`, `Stations`, `TablePlan`, `Events`, `Validation`), each folder its own sub-namespace under `GastronomyApp.Contracts`. `ApiError`, `ProblemDescription` and `Enums/` stay at the root, and `Validation/` holds the shared refusal message keys and the `RequiredText` attribute. References nothing; every other project references it. Core takes its request records as input; only Api produces its response records, mapped from entities with Mapster. |
| `GastronomyApp.Core` | Domain models, ports, use cases. No framework dependencies. |
| `GastronomyApp.Infrastructure` | EF Core SQLite, device token store, enrolment invitations. The DbContext holds only its sets and the model configuration; queries live in the repositories, and a query several repositories share is an extension method on `IQueryable` in the `Persistence` folder. |
| `GastronomyApp.Api` | Class library: REST endpoints, SignalR hub, static frontend, composition root. Names shared across the Api (authentication schemes, device claim types, hub events, hub groups, rate limit policies) are `public const string` members of nested static classes inside `GastronomyApp.Api.Names`, never injected instances. Hosted by `GastronomyApp.Desktop`. |
| `*.Tests` | Unit tests against Core, integration tests against Infrastructure and the API. `Microsoft.Extensions.TimeProvider.Testing`'s `FakeTimeProvider` stands in for `System.TimeProvider` wherever a test needs to control the clock. |

Core logs the outcome of what it did through `Microsoft.Extensions.Logging.Abstractions`
(`ILogger<T>` in the constructor): an enrolment invitation created, an invitation redeemed, a
handed-over device signed out, items settled. Core takes no other framework dependency: no ASP.NET
Core and no EF Core.

## Frontend

The frontend's own layering (a framework-free `core/` under every surface) is frontend hard rule 1.
How the wire records reach it is the `the-wire-boundary` page.
