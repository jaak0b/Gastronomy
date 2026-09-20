using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class CatalogCategoryNameIndexTest
{
  [Test]
  public async Task Save_NameThatOnlyDiffersInItsCasing_IsRefusedByTheIndex()
  {
    using SqliteInMemoryFixture fixture = new();
    await StoreCategoryAsync(fixture, "Kaffee");

    await using var second = fixture.CreateContext();
    second.CatalogCategories.Add(BuildCategory("kaffee", 2));

    Assert.ThrowsAsync<DbUpdateException>(async () => await second.SaveChangesAsync(TestContext.CurrentContext.CancellationToken));
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
             ColourHex = "#C62828",
             SortOrder = sortOrder,
             IsActive = true
           };
  }
}
