using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class FestivalStationRepositoryTest
{
  [Test]
  public async Task FindLinkAsync_AStationTakingPart_CarriesItsNextStationOrderNumber()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    FestivalStationRepository repository = new(fixture.DbContext);

    var link = await repository.FindLinkAsync(seeded.FestivalId, seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(link?.NextStationOrderNumber, Is.EqualTo(1));
  }

  [Test]
  public async Task FindLinkAsync_AStationThatIsNotAtTheFestival_ReturnsNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    FestivalStationRepository repository = new(fixture.DbContext);

    Assert.That(await repository.FindLinkAsync(seeded.FestivalId, Guid.NewGuid(), TestContext.CurrentContext.CancellationToken), Is.Null);
  }
}
