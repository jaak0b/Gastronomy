using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

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

  [Test]
  public async Task RemoveLink_AStationLeavingTheFestival_TakesTheLinkOutWhenTheChangesAreSaved()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    FestivalStationRepository repository = new(fixture.DbContext);

    var link = (await repository.FindLinkAsync(seeded.FestivalId, seeded.BarStationId, TestContext.CurrentContext.CancellationToken))!;

    repository.RemoveLink(link);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();

    Assert.That(await readContext.FestivalStations.AnyAsync(candidate => candidate.Id == link.Id, TestContext.CurrentContext.CancellationToken), Is.False);
  }

  [Test]
  public async Task FindAssignmentsAtStationAsync_AStationThatPreparesOneItem_NamesThatAssignment()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    FestivalStationRepository repository = new(fixture.DbContext);

    IReadOnlyList<ItemStationAssignment> assignments = await repository.FindAssignmentsAtStationAsync(seeded.FestivalId, seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(assignments.Select(assignment => assignment.CatalogItemId), Is.EqualTo(new[] { seeded.SausageItemId }));
  }

  [Test]
  public void RemoveAssignments_NullAssignments_ThrowsArgumentNullException()
  {
    using SqliteInMemoryFixture fixture = new();

    FestivalStationRepository repository = new(fixture.DbContext);

    Assert.That(() => repository.RemoveAssignments(null!), Throws.ArgumentNullException);
  }
}
