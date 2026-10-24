using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Configurations;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Configurations;

[TestFixture]
public sealed class AreaConfigurationTest
{
  [Test]
  public void Configure_NullBuilder_ThrowsArgumentNullException()
  {
    AreaConfiguration configuration = new();

    Assert.That(() => configuration.Configure(null!), Throws.ArgumentNullException);
  }

  [Test]
  public async Task Save_ANameAHiddenAreaOfTheSameFestivalCarriesInOtherCapitals_IsRefusedByTheIndex()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.Areas.Add(BuildArea(seeded.FestivalId, "Garten", true));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var second = fixture.CreateContext();
    second.Areas.Add(BuildArea(seeded.FestivalId, "garten", false));

    Assert.ThrowsAsync<DbUpdateException>(async () => await second.SaveChangesAsync(TestContext.CurrentContext.CancellationToken));
  }

  [Test]
  public async Task Save_TheSameNameAtTwoFestivals_StoresBoth()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var otherFestivalId = Guid.NewGuid();
    fixture.DbContext.Festivals.Add(new()
                                    {
                                      Id = otherFestivalId,
                                      Name = "Herbstfest",
                                      StartsAtUtc = new(2030, 10, 1, 12, 0, 0, DateTimeKind.Utc),
                                      EndsAtUtc = new(2030, 10, 2, 12, 0, 0, DateTimeKind.Utc),
                                      NextOrderNumber = 1,
                                      IsHidden = false
                                    });
    fixture.DbContext.Areas.Add(BuildArea(seeded.FestivalId, "Garten", false));
    fixture.DbContext.Areas.Add(BuildArea(otherFestivalId, "Garten", false));

    Assert.DoesNotThrowAsync(async () => await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken));
  }

  private Area BuildArea(Guid festivalId, string name, bool isHidden)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             FestivalId = festivalId,
             Name = name,
             SortOrder = 1,
             IsHidden = isHidden
           };
  }
}
