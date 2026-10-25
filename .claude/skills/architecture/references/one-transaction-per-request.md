# One request, one transaction

## Decision

Every request that writes is one database transaction, opened and closed in one place, so a request
either stores everything it asked for or nothing. There is no transaction runner, and no service
opens a transaction of its own.

## Laptop

`RequestTransactionFilter` (`GastronomyApp.Api/Filters`) sees every request. On a POST, PUT, PATCH
or DELETE route it opens `BEGIN IMMEDIATE`, commits when the endpoint answered with a value, and rolls back
when the service refused or when the request threw. The steps collected in `IAfterCommitActions`
(the hub events of the `announcements` page among them) are drained only after that commit. A write
that loses its rows to another writer is tried again, and when every attempt loses that race the
request answers one refusal, `Refusal.Storage.ConflictingChange()`, as 409.

## Reads cannot save

On every other request, `RequestTransactionFilter` calls `MarkReadOnly()` on the request's DbContext,
and `SaveChangesAsync` throws from then on. The switch is one-way: nothing clears it. A read that
saved would commit outside the request transaction, and nobody would hear of it. Tests and code
outside a request never mark the context. The synchronous `SaveChanges()` (both overloads) always
throws and says to use `SaveChangesAsync`.

## Tests

`GastronomyApp.Api.Tests/Filters/RequestTransactionFilterTest`, which includes a GET that tries to save.
