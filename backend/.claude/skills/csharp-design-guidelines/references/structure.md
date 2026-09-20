# Structure guidelines

Condensed from Microsoft Learn: the Architectural principles and Common web application
architectures chapters of Architect Modern Web Applications with ASP.NET Core and Azure, the .NET
Dependency injection guidelines, and the two application layer chapters of .NET Microservices
Architecture (Designing the microservice application layer and Web API, and Implementing the
microservice application layer using the Web API). The final section is this repository's own.

## Design principles

Each principle carries the official definition, then the checks an agent applies to one class or
file.

**Separation of concerns.** "This principle asserts that software should be separated based on the
kinds of work it performs." Business rules and logic "should reside in a separate project, which
should not depend on other projects in the application."

- **Do** keep a class to one kind of work: choosing what to do, doing it, or presenting it, never two
  of these together.
- **Do not** let a file in the domain project reference an HTTP type, a DbContext or a hub.
- **Do not** put the rule that decides an outcome next to the code that formats it for a screen.

**Encapsulation.** "Application components and layers should be able to adjust their internal
implementation without breaking their collaborators as long as external contracts are not violated."
"Mutable global state is antithetical to encapsulation."

- **Do** change an object's state only through a method or setter that the type itself defines.
- **Do not** expose a collection or field that another class mutates in place.
- **Do not** keep mutable static state; a value read from it in one place cannot be trusted in
  another.

**Dependency inversion.** "The direction of dependency within the application should be in the
direction of abstraction, not implementation details."

- **Do** let a service depend on an interface it owns, and let the implementation live further out.
- **Do not** have the class that holds a rule reference the class that persists the result.
- **Consider** whether a new interface has one implementation and one consumer; if only one side
  exists, the abstraction is not yet earned.

**Explicit dependencies.** "Methods and classes should explicitly require any collaborating objects
they need in order to function correctly." A class that only works "if certain global or
infrastructure components are in place" is "being dishonest with their clients."

- **Do** take every collaborator through the constructor.
- **Do not** reach for a static accessor, a service locator or `GetService` inside a method body.
- **Do not** construct a collaborator with `new` inside a service when the container can supply it.

**Single responsibility.** "Objects should have only one responsibility and that they should have only
one reason to change." At the architecture level, "presentation responsibility should remain in the UI
project, while data access responsibility should be kept within an infrastructure project. Business
logic should be kept in the application core project."

- **Do not** put route mapping, request validation and the database write in one class.
- **Do** add a new class for new behaviour instead of a new branch in an existing one: "adding new
  classes is always safer than changing existing classes."
- **Consider** splitting a class whose constructor takes more than about four or five dependencies.
  The dependency injection guidelines state it plainly: "If a class has many injected dependencies,
  it might be a sign that the class has too many responsibilities and violates the Single
  Responsibility Principle (SRP). Attempt to refactor the class by moving some of its responsibilities
  into new classes."

**Don't repeat yourself.** "The application should avoid specifying behavior related to a particular
concept in multiple places as this practice is a frequent source of errors." "Rather than duplicating
logic, encapsulate it in a programming construct. Make this construct the single authority over this
behavior."

- **Do not** compute a total, a sequence number or a routing decision in a second place; call the
  class that already owns it.
- **Do** move a rule two classes share into one class both take as a dependency.
- **Avoid** merging code that is only coincidentally alike: "Duplication is always preferable to
  coupling to the wrong abstraction."

**Persistence ignorance.** Types that are persisted have "code unaffected by the choice of
persistence technology." Named violations: "a required base class," "a required interface
implementation," "classes responsible for saving themselves," "required parameterless constructor,"
"properties requiring virtual keyword," "persistence-specific required attributes."

- **Do** keep an entity a plain class with no EF Core attribute, base class or `virtual` demanded by
  the mapper; the mapping belongs in a configuration class in the infrastructure project.
- **Do not** give an entity a `Save` method or a reference to the DbContext.
- **Do not** import a persistence namespace into a file under the domain project.

**Bounded contexts.** Complexity is tackled "by breaking it up into separate conceptual modules,"
each free "to choose its own names for concepts within it," with "exclusive access to its own
persistence store."

- **Do** use one word for one concept inside this application, and keep that vocabulary stable across
  its projects.
- **Do not** let a second application, script or tool write to this application's database.
- **Consider** whether a new feature introduces a second meaning for an existing name; if it does,
  the feature needs its own name, not a shared one.

## Clean architecture layering

Clean architecture "puts the business logic and application model at the center of the application.
Instead of having business logic depend on data access or other infrastructure concerns, this
dependency is inverted: infrastructure and implementation details depend on the Application Core."
"Dependencies flow toward the innermost circle," and "the Application Core has no dependencies on
other application layers." "Both the UI and the Infrastructure layers depend on the Application Core,
but not on one another."

What each layer holds, per the official type lists:

- Application Core: entities, aggregates, interfaces, domain services, specifications, custom
  exceptions and guard clauses, domain events and handlers. Interfaces here "include abstractions
  for operations that will be performed using Infrastructure, such as data access, file system
  access, network calls."
- Infrastructure: EF Core types (`DbContext`, `Migration`), data access implementations, and
  "infrastructure-specific services." These "should implement interfaces defined in the Application
  Core, and so Infrastructure should have a reference to the Application Core project."
- UI layer: controllers or endpoints, custom filters, custom middleware, views, view models and
  startup. Its types "should interact with infrastructure strictly through interfaces defined in
  Application Core. No direct instantiation of or static calls to the Infrastructure layer types
  should be allowed in the UI layer." The UI project may reference Infrastructure for wiring, but
  "developers should limit actual references to types in the Infrastructure project to the app's
  composition root."

Mapped onto this repository:

| Layer | Project | Holds |
|---|---|---|
| UI | `GastronomyApp.Api` | Endpoints, contracts, hub, auth, error handling, hosting, service registration (the composition root). |
| Application Core | `GastronomyApp.Core` | Entities, enums, results, services, and the ports the other two projects implement or call. |
| Infrastructure | `GastronomyApp.Infrastructure` | The DbContext, entity configurations, migrations, repositories, the clock, the connection factory and the failure translator. |

`GastronomyApp.Core` references no other project. `GastronomyApp.Infrastructure` references `Core`
only. `GastronomyApp.Api` references both, and its use of `Infrastructure` types stays in
`ApiServiceRegistration` and the hosting files.

Tests an agent runs on one file:

- **Do** check that the project the file sits in matches the work it does: a rule or a decision in
  `Core`, a query or a mapping in `Infrastructure`, a route or a wire shape in `Api`.
- **Do** check that the namespace matches the folder and the project, so the file's location says
  what its namespace says.
- **Do not** accept a `using` of an ASP.NET Core, EF Core or SQLite namespace in a `Core` file.
- **Do not** accept a `using` of an `Api` namespace in `Core` or `Infrastructure`.
- **Do not** accept an `Infrastructure` type named outside the composition root in `Api`.

## Endpoint shape

The application layer is "usually your Web API library," and business rules do not live in it: "all
the domain logic should be contained in the domain classes," "but not within the command handler,
which is a class from the application layer." A lean entry point takes "just a few dependencies
instead of many," and the code that hands the request on "is almost one line."

- **Do** let an endpoint do four things and nothing more: map a route, bind and validate the
  request shape, call one application service, and turn that service's result into an HTTP
  response.
- **Do not** put a business rule, a calculation or a database call in an endpoint; those live in the
  service and the repository behind it.
- **Do not** let an endpoint call two services in sequence to complete one action; if the action
  needs both, one service owns that sequence.
- **Avoid** an endpoint whose lambda or method body runs past a screen; that length is a service that
  has not been named yet.

## House rules

These rules are this repository's own additions. They are not taken from the Microsoft Learn pages
above.

- **Do** put one class, record, struct, interface or enum in each file, and name the file after the
  type. This follows StyleCop analyzers SA1402 (file may only contain a single type) and SA1649
  (file name should match first type name). A private nested type stays with its owner.
- **Do not** create a static class as a helper bucket. This one is official: the static class design
  rules in [type-design.md](type-design.md), section Static class design, say to use static classes
  sparingly and never as a miscellaneous bucket. A stateless operation that a service needs becomes a
  method on that service, or a small class the service takes as a dependency.
- **Do not** use the conditional operator (`condition ? a : b`); write an `if` with an `else`, or two
  early returns. `??`, `??=` and `?.` stay. **Do** build a list from a query with `.ToList()` or
  `.ToArray()`, never with a spread collection expression (`[.. items.Select(...)]`); a collection
  expression is only for a literal list of values. Neither of these can be enforced by the cleanup.
- **Do** put exactly one kind of type in a folder, name the folder after that kind, and give the file
  the namespace of its project plus its folder, so no folder is a catch-all and no folder is written
  an exception.
- **Do** place a new file in the folder that names both its kind and its concept, so the folder alone
  says what the file is. In `GastronomyApp.Core`: `Entities` for persisted aggregates, `Enums` for
  domain enums shared beyond one area, `Ports` for interfaces only, `Requests` for the records a
  caller hands to a service, `ReadModels` for what a service or a repository returns for reading
  including intermediate values a service computes and hands on, `Results` for the outcome of a write
  or a verification and for failure records with their reason enums, `Exceptions` for exception types,
  and `Services` for classes only. In `GastronomyApp.Api`: `Endpoints` for the route mapping classes
  only, `Handlers` for the classes an endpoint calls, `Mapping` for the Mapster `IRegister` classes and
  the configuration that compiles them, `Announcers` for the classes that tell the devices, `Responders`
  for the classes that write a response body, `Contracts` for wire records only, `Auth` for
  authentication with `Auth/Filters` for the endpoint filters and `Auth/Callers` for the caller records,
  `Names` for the classes that hold nothing but the constant names of hub events, hub groups, rate limit
  policies, authentication schemes and device claims, `Values` for the records the Api passes around
  inside itself rather than over the wire, and `Hub`, `Hosting`, `ErrorHandling`, `Options` and
  `RateLimiting` for what their names say. In
  `GastronomyApp.Infrastructure`:
  `Persistence` for the context, the connection factory and the transaction runner, `Repositories`,
  `Configurations`, `Security`, `ErrorHandling` and `Values` for the records its own classes hand each
  other. `Projections` holds the Mapster `IRegister`
  classes that declare how entities and query rows become read models, and `QueryRows` holds the
  intermediate records a query materialises on the way there, so `Repositories` keeps to repositories
  alone. In `GastronomyApp.Desktop`: one folder per
  concern (`Hosting`, `Updates`, `Settings`, `Setup`, `Platform`, `Localization`, `Logging`,
  `ViewModels`, `Views`), with `Enums` for its enums, `Events` for its event argument classes and `Values` for
  its records, and never a `Services` catch-all. In a test project a support type that is not
  a fixture lives under `TestSupport` and the fixture folders mirror the production folders.
