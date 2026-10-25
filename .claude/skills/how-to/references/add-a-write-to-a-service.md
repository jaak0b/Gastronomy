# Add a write to an area's service

A write changes data. It is a method of the area's service in `GastronomyApp.Core/Services` that
loads through `IGastronomyDatabase`, applies the rules and saves. The structure and its reasons are
on the `layers-and-projects` page of `architecture`.

## Steps

1. **Look for what exists.** Open `Core/Queries/<Entity>Queries.cs` before writing a filter, and ask
   whether this logic already exists somewhere, in the queries or in the area's service. Use what is
   there.
2. **The request record.** Add or change it in `Contracts` as `change-a-wire-contract` describes. Its
   shape is checked there by DataAnnotations; the domain rules are checked in the service.
3. **The method.** Add it to the area's `<Area>Service`; a new area gets its service. It loads the
   rows it changes (tracked, no `AsNoTracking`), checks each rule and returns `ErrorOr<T>`. A
   refusal is an `Error` from a factory in `Core/Refusals`, as `add-a-refusal` describes, never a
   nullable value standing for a refusal. The rules live here and never in an entity class. A rule
   that other actions of the area also need is a private method of the service, never a copy. After
   the rules pass, change the rows and call `SaveChangesAsync`. A value the person entered is either
   kept exactly or refused with a message that names the problem.
4. **Loading that is complex and repeated.** When several actions load the same data for a write,
   such as an order with its items, that load goes into a repository. Otherwise the service loads
   it itself. Never add a repository for one entity or a method for one caller.
5. **What it returns.** The record the endpoint answers with, produced by Mapster from the saved
   entity, or nothing for a 204.
6. **The endpoint.** Map the route in `Api/Endpoints` with `MapPost`, `MapPut`, `MapPatch` or
   `MapDelete`, call the service directly and return `ApiAnswer<T>`, `CreatedAnswer<T>` or
   `NoContentAnswer`. The calling device's identity is an endpoint parameter handed to the service as
   a plain parameter.
7. **No transaction and no announcement of its own.** `RequestTransactionFilter` opens one
   transaction for the request and commits or rolls it back, so the service only saves. The entity
   carries `[Raises(...)]`, and the save interceptor sends the announcement after the commit (the
   `one-transaction-per-request` and `announcements` pages). Do not use `ExecuteUpdateAsync`,
   `ExecuteDeleteAsync` or raw SQL, because they bypass the interceptor.
8. **When a service grows too large,** split it by sub-topic (placing an order and settling it, for
   example), never into one class per action.

## Tests

- A service test against the real DbContext on in-memory SQLite for each rule: assert
  `RefusalMessageKey()`, `FirstError.NumericType` and `RefusalMetadata(...)` for a refusal, that the
  rows are stored as the method promises, and that a refused request stores nothing. FakeItEasy stays
  for other ports such as the clock.
- The endpoint test in `GastronomyApp.Api.Tests/Endpoints` for the route, the status and the body of
  a refusal.

Run single files, for example
`dotnet test backend/GastronomyApp.Core.Tests --filter "FullyQualifiedName~<Area>ServiceTest"`.
