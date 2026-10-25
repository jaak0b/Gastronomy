# Pin how many SQL statements a read runs

Some reads have to run as exactly one statement: a read many devices repeat on every hub event has
to be cheap, and a read whose parts must agree with each other needs one snapshot. A test pins the
count so a later `Include`, `AsSplitQuery` or extra lookup fails at once instead of slowing the
festival down. The unit that is measured is the read method of the area's service, because that is what an endpoint
calls and what the devices repeat.

## When to use it

For a read of a service that has to stay one statement (or another fixed number). Behaviour still
belongs in the service's ordinary tests. This test only counts the statements.

## Steps

1. Seed what the read loads in full: every collection it projects, in the same parent, so a query
   that splits or multiplies rows would show it.
2. Create a separate read context on the fixture's connection with a `QueryCounter`
   (`TestSupport/QueryCounter.cs`, a `DbCommandInterceptor` that counts every executed reader):

   ```csharp
   QueryCounter counter = new();
   await using GastronomyAppDbContext readContext = new(new DbContextOptionsBuilder<GastronomyAppDbContext>().UseSqlite(fixture.Connection).AddInterceptors(counter).Options);
   ```

3. Run the read method of the service with that context as its `IGastronomyDatabase`, and nothing else on it.
4. Assert the number: `Assert.That(counter.QueryCount, Is.EqualTo(1));`.

## Example

`TablePlanServiceTest.ReadRunningFestivalAsync_APlanWithOrdersAtTwoStationsAndStationShapes_ReadsThePlanInOneStatement`
pins the phone's table plan read to one statement. The service under test gets the read context; the
`RunningFestivalLookup` it asks for the running festival gets the fixture's own context, so the
counter sees the plan statement alone. The statement is a `ProjectToType` over the shown areas, with
the item flags of each table mapped in `Core/Mapping/TablePlanMapping.cs`.

Run it on its own:
`dotnet test backend/GastronomyApp.Infrastructure.Tests/GastronomyApp.Infrastructure.Tests.csproj --filter "FullyQualifiedName~Services.TablePlanServiceTest"`.
