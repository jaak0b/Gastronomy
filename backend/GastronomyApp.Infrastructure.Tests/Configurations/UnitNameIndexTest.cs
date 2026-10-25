using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Configurations;

[TestFixture]
public sealed class UnitNameIndexTest
{
  [Test]
  public async Task SaveChangesAsync_ASecondUnitWithTheSameNameInOtherCase_IsRejectedByTheUniqueIndex()
  {
    using SqliteInMemoryFixture fixture = new();
    fixture.DbContext.Units.Add(BuildUnit("Portion"));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    await using var second = fixture.CreateContext();
    second.Units.Add(BuildUnit("PORTION"));

    Assert.ThrowsAsync<DbUpdateException>(async () => await second.SaveChangesAsync(TestContext.CurrentContext.CancellationToken));
  }

  private Unit BuildUnit(string name)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = name,
             IsActive = true
           };
  }
}
