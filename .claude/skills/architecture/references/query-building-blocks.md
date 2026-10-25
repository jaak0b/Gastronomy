# Shared query pieces

## Decision

A filter, an ordering or a projection step that more than one action of an area service needs is an `IQueryable`
extension method in `GastronomyApp.Core/Queries`. The folder is the catalogue: nobody keeps a list of
the pieces by hand, and an agent looks in `Core/Queries/<Entity>Queries.cs` before writing a filter.

The reason is that the same condition written in two places drifts apart. One place forgets that
a deactivated station is not offered any more, and a screen shows a row another screen hides.

## Rules

- **One static class per entity type**, named `<Entity>Queries`. An extension on `IQueryable<Station>`
  lives only in `StationQueries`, never in a class named after a service or a screen.
- **Behaviour shared by several entities goes through a small interface** on the entities and one class
  `<Interface>Queries`. Only `Active<T>()` exists so far; `HideableQueries.Shown<T>()` waits for its first use. Two exist:
  - `IActivatable { bool IsActive { get; set; } }` with `ActivatableQueries.Active<T>() where T : class, IActivatable`,
    for stations, items, categories, ingredients, units and staff members.
  - `IHideable` with `HideableQueries.Shown<T>()`, for festival, area and table.
  - `IFestivalScoped : IEntity { Guid FestivalId }`, for area and table. It has no query extension: `ActivationService.ShowAsync` and `FestivalRowHiding.HideAsync` use it to look a row up inside the festival of the route, so a row of another festival is not found.
  - `INamed : IEntity { string Name }` (`IEntity` is `{ Guid Id }`, implemented by every entity with a single Guid key, and `ISortable` and `IDeviceOwner` derive from it too) with `NamedQueries.Namesakes<T>(name, excludedId)`, for units,
    ingredients, categories, areas and tables (an area or a table is looked up inside its festival first,
    then `Namesakes` is applied). It compares in SQL, so the column's
    `NOCASE` collation decides which names are the same; an edit passes its own id as `excludedId`,
    a create passes `null`.
- **A piece may take the other set it joins as a parameter.** `OrderItemQueries.ConsumedIngredients(items, recipeLines)` joins order items to recipe lines and projects `Results/ConsumedIngredient`; the festival stock list and the sold-out marker both group it.
- **An interface appears only when at least two entities share the same flag with the same meaning.**
  A flag with another meaning gets no interface, and an entity never carries both `IsActive` and `IsHidden`.
- **A piece is extracted on its second use.** A query used once stays in its service.
- **A piece is called on the top-level query only.** EF Core cannot translate a custom extension method
  inside a lambda, so a filter inside a Mapster expression is written out in `Core/Mapping`.
- **A piece returns `IQueryable`**, so it composes with the rest of the query and EF Core translates it
  into SQL. It does not load rows and does not call `SaveChangesAsync`.

## Example

```csharp
public static class ActivatableQueries
{
    public static IQueryable<T> Active<T>(this IQueryable<T> rows) where T : class, IActivatable =>
        rows.Where(row => row.IsActive);
}
```

## Tests

Each extension has a test that runs it against the database (in-memory SQLite), because only the
database proves that EF Core can translate it. The test seeds rows that the piece must keep and rows
it must drop, and asserts both.
