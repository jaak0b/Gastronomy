# Add a read to an area's service

A read answers a question and changes nothing. It is a method of the area's service in
`GastronomyApp.Core/Services` that queries `IGastronomyDatabase` and returns a record from
`GastronomyApp.Contracts`. The structure and its reasons are on the `layers-and-projects` page of
`architecture`.

## Steps

1. **Look for what exists.** Open `Core/Queries/<Entity>Queries.cs` (and `ActivatableQueries` or
   `HideableQueries` when the entity is activatable or hideable) before writing a filter, and look in
   the area's service for the same question. Does this logic already exist somewhere? If it does,
   use it.
2. **The record.** Add or change the response record in `Contracts` as the `change-a-wire-contract`
   page describes.
3. **The method.** Add it to the area's `<Area>Service`; a new area gets its service. Query the
   table, call `AsNoTracking`, apply the shared pieces, order by a value the data carries (a name, a
   sort order, never the id) and project with `ProjectToType<TView>()`:

   ```csharp
   return await database.Ingredients
       .AsNoTracking()
       .Active()
       .OrderBy(ingredient => ingredient.Name)
       .ProjectToType<IngredientView>()
       .ToListAsync(cancellationToken);
   ```

   Return a list with `ToListAsync`, never a collection expression. Compute a derived figure (a
   sum, a stock level) in the query each time, never from a stored copy. A read never calls
   `SaveChangesAsync` and never adds or removes a row.
4. **A read that can refuse.** When the read can say no (an unknown id), return `ErrorOr<T>` with a
   factory from `Core/Refusals`; see `add-a-refusal`.
5. **A piece used a second time.** When the condition you just wrote already exists in another
   action, move it into the `<Entity>Queries` class of that entity and use it from both. A rule that
   only actions of this area share becomes a private method of the service instead. A query used once
   stays in its method. Test the extension against in-memory SQLite, as the `query-building-blocks`
   page says.
3a. **The mapping.** Mapster infers what the names match (`Unit.Name` flattens into `UnitName`). Anything
   else goes into the area's `Core/Mapping/<Area>Mapping.cs` as a query expression, with no custom
   method inside it, because EF Core must translate it.
6. **The endpoint.** Map the route in `Api/Endpoints` and call the service directly, giving the lambda
   the answer type as its return type (`async Task<ApiAnswer<TView>> (...) => await service.ListAsync(...)`),
   which is how `ApiAnswer<TView>` converts the result and describes the route in OpenAPI. No handler class and no mapping file in `Api` sit between them.
7. **Statement count.** A read that many devices repeat on every hub event gets a statement count
   test, as `count-sql-statements` describes.

## Tests

- A service test against the real DbContext on in-memory SQLite: seed rows the read must return and
  rows it must leave out, and assert both, so EF Core proves it can translate the query.
- The endpoint test in `GastronomyApp.Api.Tests/Endpoints` for the route and the status. A GET has no
  request transaction, so a read that tries to save fails here.

Run single files, for example
`dotnet test backend/GastronomyApp.Core.Tests --filter "FullyQualifiedName~<Area>ServiceTest"`.
