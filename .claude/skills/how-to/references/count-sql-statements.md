# Pin how many SQL statements a read runs

Some reads have to run as exactly one statement: a read many devices repeat on every hub event has
to be cheap, and a read whose parts must agree with each other needs one snapshot. A test pins the
count so a later `Include`, `AsSplitQuery` or extra lookup fails at once instead of slowing the
festival down.

## When to use it

For a repository read that has to stay one statement (or another fixed number), in
`GastronomyApp.Infrastructure.Tests`. Behaviour still belongs in the read's ordinary tests. This
test only counts the statements.

## Steps

1. Seed what the read loads in full: every collection it projects, in the same parent, so a query
   that splits or multiplies rows would show it.
2. Create a separate read context on the fixture's connection with a `QueryCounter`
   (`TestSupport/QueryCounter.cs`, a `DbCommandInterceptor` that counts every executed reader):

   ```csharp
   QueryCounter counter = new();
   await using GastronomyAppDbContext readContext = new(new DbContextOptionsBuilder<GastronomyAppDbContext>().UseSqlite(fixture.Connection).AddInterceptors(counter).Options);
   ```

3. Call the repository method on that context, and nothing else on it.
4. Assert the number: `Assert.That(counter.QueryCount, Is.EqualTo(1));`.

## Example

`TablePlanRepositoryTest.FindShownAreasWithItemFlagsAsync_APlanWithOrdersAtTwoStationsAndStationShapes_ReadsItInOneStatement`
pins the phone's table plan read to one statement.

Run it on its own:
`dotnet test backend/GastronomyApp.Infrastructure.Tests/GastronomyApp.Infrastructure.Tests.csproj --filter "FullyQualifiedName~Repositories.TablePlanRepositoryTest"`.
