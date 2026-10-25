using GastronomyApp.Core.Queries;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Queries;

[TestFixture]
public sealed class FestivalStationQueriesTest
{
  [Test]
  public async Task ActiveAt_StationsOfTwoFestivalsAndOneSwitchedOff_KeepsTheActiveOnesOfTheFestivalAskedFor()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var otherFestivalId = Guid.NewGuid();
    fixture.DbContext.Festivals.Add(new()
                                    {
                                      Id = otherFestivalId,
                                      Name = "Herbstfest",
                                      StartsAtUtc = DateTime.UtcNow.AddDays(30),
                                      EndsAtUtc = DateTime.UtcNow.AddDays(31),
                                      NextOrderNumber = 1,
                                      IsHidden = false
                                    });
    fixture.DbContext.FestivalStations.Add(new()
                                           {
                                             Id = Guid.NewGuid(),
                                             FestivalId = otherFestivalId,
                                             StationId = seeded.KitchenStationId,
                                             NextStationOrderNumber = 1
                                           });
    var bar = await fixture.DbContext.Stations.SingleAsync(station => station.Id == seeded.BarStationId, TestContext.CurrentContext.CancellationToken);
    bar.IsActive = false;
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    await using var readContext = fixture.CreateContext();

    IReadOnlyList<Guid> stationIds = await readContext.FestivalStations.ActiveAt(seeded.FestivalId).Select(link => link.StationId).ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(stationIds, Is.EqualTo(new[] { seeded.KitchenStationId }));
  }
}
