# Where a request is checked

## Decision

A request's **shape** is checked on its contract record, and a **domain rule** in
`GastronomyApp.Core`.

## Laptop

The shape is validated by DataAnnotations attributes on the contract records in
`GastronomyApp.Contracts`, checked by the built-in minimal API validation (`AddValidation` in
`ApiServiceRegistration`). A required field, a string length, a numeric range, a non-empty list and a
well-formed colour live there and nowhere else. How a refused shape is answered is on the
`add-a-refusal` page of `how-to`.

Domain rules stay in `GastronomyApp.Core` and return `ErrorOr<T>` from the ErrorOr package: anything
that needs the database (no running festival, an item that is not on the menu, a name another row
already holds, an item already handed out, an unknown table), the uniqueness of the order item ids
in one settlement, the production minutes an article may carry, a festival's period and its overlap,
and the reason a settlement needs when the amount paid is below the price the database holds.

## Tests

`GastronomyApp.Api.Tests/ErrorHandling/RequestShapeRefusalWriterTest` for a refused shape; each
domain rule in the service test of its area.
