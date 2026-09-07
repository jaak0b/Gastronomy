using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class CatalogCategoryNameIndexTest
{
  private const string SmallUmlautU = "ü";
  private const string CapitalUmlautU = "Ü";

  private readonly CatalogCategoryNaming _naming = new();

  [Test]
  public async Task Save_NameThatOnlyDiffersInItsCasing_IsRefusedJustAsTheNamingServiceSaysItIs()
  {
    using SqliteInMemoryFixture fixture = new();
    await StoreCategoryAsync(fixture, "Kaffee");

    await using var second = fixture.CreateContext();
    second.CatalogCategories.Add(BuildCategory("kaffee", 2));

    Assert.Multiple(() =>
                    {
                      Assert.ThrowsAsync<DbUpdateException>(async () => await second.SaveChangesAsync(TestContext.CurrentContext.CancellationToken));
                      Assert.That(_naming.Normalized("kaffee"), Is.EqualTo(_naming.Normalized("Kaffee")));
                    });
  }

  [Test]
  public async Task Save_NameThatOnlyDiffersInTheCasingOfAnUmlaut_IsRefusedJustAsTheNamingServiceSaysItIs()
  {
    using SqliteInMemoryFixture fixture = new();
    var name = $"Gr{SmallUmlautU}tze";
    var nameInCapitals = $"GR{CapitalUmlautU}TZE";
    await StoreCategoryAsync(fixture, name);

    await using var second = fixture.CreateContext();
    second.CatalogCategories.Add(BuildCategory(nameInCapitals, 2));

    Assert.Multiple(() =>
                    {
                      Assert.ThrowsAsync<DbUpdateException>(async () => await second.SaveChangesAsync(TestContext.CurrentContext.CancellationToken));
                      Assert.That(_naming.Normalized(nameInCapitals), Is.EqualTo(_naming.Normalized(name)));
                    });
  }

  private async Task StoreCategoryAsync(SqliteInMemoryFixture fixture, string name)
  {
    await using var context = fixture.CreateContext();
    context.CatalogCategories.Add(BuildCategory(name, 1));
    await context.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private CatalogCategory BuildCategory(string name, int sortOrder)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = name,
             NormalizedName = _naming.Normalized(name),
             ColourHex = "#C62828",
             SortOrder = sortOrder,
             IsActive = true
           };
  }
}
