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
- **`FestivalStationQueries.ActiveAt(festivalId)`** keeps the festival's links whose station is switched on. `ItemOrderability`, `CatalogService` and `EstimateService` use it.
- **A piece may take the other set it joins as a parameter.** `OrderItemQueries.ConsumedIngredients(items, recipeLines)` joins order items to recipe lines and projects `Results/ConsumedIngredient`; the festival stock list and the sold-out marker both group it.
- **An interface appears only when at least two entities share the same flag with the same meaning.**
  A flag with another meaning gets no interface, and an entity never carries both `IsActive` and `IsHidden`.
- **A piece is extracted on its second use.** A query used once stays in its service.
- **Each entity has exactly one static extension class, `Core/Queries/<Entity>Queries.cs`.** It holds the entity's `IQueryable` filters and its checks as extension methods, and nothing else: no `Expression` property, field or method returning one. A check that EF Core must translate inside a query (a `Where`, a nested `Any`, an `Include` filter or a projection) is an extension method marked `[Projectable]` from EntityFrameworkCore.Projectables, written once as an ordinary expression body; the same method also works on a loaded entity. The
  same holds for every other type with checks of its own: an enum gets its own class
  (`OrderItemStateQueries` for `OrderItemState`), and a flag several entities share lives in
  `<Interface>Queries.cs`.
  `OrderItemQueries.IsOpen`, `IsHandedOut`, `IsCancelled`, `IsStillOrdered` (not cancelled, the
  one place that says so), `IsSettled`, `IsToSettle`, `AwaitsCancellationAcknowledgement`, `State`
  and `StateSetAtUtc` are such checks: `WhereOpen` uses
  one at the top level, `StationOrderQueries` nests them in `Items.Any(...)`, `StationQueueMapping`
  and `OpenItemMapping` call them in a Mapster mapping, and the services call them on loaded items.
  `OrderQueries.Items` (every item of every station order of an order) carries them over to a
  whole order: `HasItemsToHandOut`, `IsPartlyHandedOut`, `HasItemsToSettle` and `IsPartlySettled`
  each ask `order.Items().Any(...)`, and a `[Projectable]` that calls another one is expanded inside
  it, so the four still translate: `TablePlanMapping` counts a table's orders with them in SQL, and
  `OrderQueries.Status` combines two of them, both on a loaded order for `OrderMapping` and in SQL
  for the table search of `OpenItemsService`. A `[Projectable]` body is one expression: a choice
  between values is a `switch` expression on constant patterns ending in a discard arm, because
  version 4.0.0 rewrites no other pattern and no `when` guard. `OrderItemQueries.MatchesTableSearch`
  takes the search's optional payment status and include-cancelled option, and an option that is
  `null` keeps every item. A check that only ever runs in memory needs no `[Projectable]`:
  `OrderItemQueries.WasHandedOutWhenCancelled` reads, for a cancelled item, the status the
  cancellation followed (`CurrentStatus.PreviousStatus`) on items the table search loaded with that
  status included. An acknowledged item answers false, because `OrderItemLifecycle` allows an
  acknowledgement only after cancelling an open item. The check throws an
  `InvalidOperationException` naming `OrderItemStatus.PreviousStatus` when the chain was not
  loaded. The package is turned
  on once, with `UseProjectables()` in `GastronomyAppDbContext.OnConfiguring`, which every way of
  building the context's options passes through.
- **Two `[Projectable]` methods in one class never share a name.** Version 4.0.0, the last one for
  EF Core 9, names its generated file after the class and the method only, so an overload breaks the
  build of `Core` (`CS8785`). The checks on the `OrderItemState` enum therefore live in their own
  class, `OrderItemStateQueries`.
- **`OrderItem.CurrentStatus` is auto-included** (`Navigation(...).AutoInclude()` in
  `OrderItemConfiguration`), so every query that loads an order item, directly or through an
  `Include` of `Items`, brings its current status along, and no query includes it by hand. Almost
  every read of an item asks for its state, so the status travels with the item wherever it is
  loaded and no query can forget it. `OrderItemStatus.PreviousStatus` is not auto-included: EF Core refuses it as a
  cycle, because it would follow the whole history. The station queue's two loads that decide in
  memory whether a cancellation still waits for its acknowledgement (the queue read and the lookup
  of the selected items; the done view does not need it) include it explicitly
  (`Include(item => item.CurrentStatus!.PreviousStatus)`), and `OrderItemLifecycle` throws an
  `InvalidOperationException` naming `OrderItemStatus.PreviousStatus` when it is missing.
- **A filter returns `IQueryable`**, so it composes with the rest of the query and EF Core translates it
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
it must drop, and asserts both. A `[Projectable]` check is proved the same way at each place EF
Core meets it: a top-level `Where`, nested in an `Any`, and in a projection, where the test also
asserts that the generated SQL reads the status table, because EF Core would otherwise evaluate a
final projection in memory without a word (`Queries/OrderItemQueriesTest`).
