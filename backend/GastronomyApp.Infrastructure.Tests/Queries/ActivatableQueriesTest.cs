using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Queries;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Queries;

[TestFixture]
public sealed class ActivatableQueriesTest
{
  [Test]
  public async Task Active_ActiveAndSwitchedOffUnits_KeepsOnlyTheActiveOnes()
  {
    using SqliteInMemoryFixture fixture = new();
    fixture.DbContext.Units.AddRange(UnitNamed("Gramm", true), UnitNamed("Kilogramm", false), UnitNamed("Liter", true));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var names = await fixture.DbContext.Units.Active().Select(unit => unit.Name).ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(names, Does.Contain("Gramm").And.Contain("Liter"));
                      Assert.That(names, Does.Not.Contain("Kilogramm"));
                    });
  }

  private Unit UnitNamed(string name, bool isActive)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = name,
             IsActive = isActive
           };
  }
}
