using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Queries;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Queries;

[TestFixture]
public sealed class ActivatableQueriesTest
{
  [Test]
  public async Task Active_ActiveAndSwitchedOffStations_KeepsOnlyTheActiveOnes()
  {
    using SqliteInMemoryFixture fixture = new();
    fixture.DbContext.Stations.AddRange(StationNamed("Grill", 1, true), StationNamed("Kaffee", 2, false), StationNamed("Theke", 3, true));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var names = await fixture.DbContext.Stations.Active().Select(station => station.Name).ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(names, Does.Contain("Grill").And.Contain("Theke"));
                      Assert.That(names, Does.Not.Contain("Kaffee"));
                    });
  }

  private Station StationNamed(string name, int sortOrder, bool isActive)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = name,
             SortOrder = sortOrder,
             IsActive = isActive
           };
  }
}
