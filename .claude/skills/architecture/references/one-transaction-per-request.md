# One request, one transaction

## Decision

Every request that writes is one database transaction, opened and closed in one place, so a request
either stores everything it asked for or nothing. There is no transaction runner, and no service
opens a transaction of its own.

## Laptop

`RequestTransactionFilter` (`GastronomyApp.Api/Filters`) runs on every POST, PUT, PATCH and DELETE
route. It opens `BEGIN IMMEDIATE`, commits when the endpoint answered with a value, and rolls back
when the service refused or when the request threw. The steps collected in `IAfterCommitActions`
(the hub events of the `announcements` page among them) are drained only after that commit. A write
that loses its rows to another writer is tried again, and when every attempt loses that race the
request answers one refusal, `Refusal.Storage.ConflictingChange()`, as 409.

## Tests

`GastronomyApp.Api.Tests/Filters/RequestTransactionFilterTest`.
