using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Queries;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Queries;

[TestFixture]
public sealed class NamedQueriesTest
{
  [Test]
  public async Task Namesakes_TheNameInOtherCapitals_KeepsOnlyTheRowsOtherThanTheExcludedOne()
  {
    using SqliteInMemoryFixture fixture = new();
    var gram = UnitNamed("Gramm");
    fixture.DbContext.Units.AddRange(gram, UnitNamed("Liter"), UnitNamed("Äpfel"));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var namesakes = await fixture.DbContext.Units.Namesakes("GRAMM", null).Select(unit => unit.Name).ToListAsync(TestContext.CurrentContext.CancellationToken);
    var namesakesOfOthers = await fixture.DbContext.Units.Namesakes("gramm", Guid.NewGuid()).Select(unit => unit.Name).ToListAsync(TestContext.CurrentContext.CancellationToken);
    var namesakesWithoutItself = await fixture.DbContext.Units.Namesakes("gramm", gram.Id).ToListAsync(TestContext.CurrentContext.CancellationToken);
    var umlautInOtherCase = await fixture.DbContext.Units.Namesakes("äpfel", null).ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(namesakes, Is.EqualTo(new[] { "Gramm" }));
                      Assert.That(namesakesOfOthers, Is.EqualTo(new[] { "Gramm" }));
                      Assert.That(namesakesWithoutItself, Is.Empty);
                      Assert.That(umlautInOtherCase, Is.Empty);
                    });
  }

  private Unit UnitNamed(string name)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = name,
             IsActive = true
           };
  }
}
