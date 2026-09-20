using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
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
  public async Task CountUnfulfilledItemsAsync_AStationWithOneItemStillOpen_CountsOnlyThatOne()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    await SeedOneOpenAndOneDoneItemAsync(fixture, seeded);

    FestivalStationRepository repository = new(fixture.DbContext);

    Assert.That(await repository.CountUnfulfilledItemsAsync(seeded.FestivalId, seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken), Is.EqualTo(1));
  }

  [Test]
  public async Task CountUnfulfilledItemsAsync_AStationNobodyOrderedFrom_CountsNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    FestivalStationRepository repository = new(fixture.DbContext);

    Assert.That(await repository.CountUnfulfilledItemsAsync(seeded.FestivalId, seeded.BarStationId, TestContext.CurrentContext.CancellationToken), Is.EqualTo(0));
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

  private async Task SeedOneOpenAndOneDoneItemAsync(SqliteInMemoryFixture fixture, SeededDomain seeded)
  {
    DateTime now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);
    var orderId = Guid.NewGuid();
    var stationOrderId = Guid.NewGuid();

    fixture.DbContext.Orders.Add(new()
                                 {
                                   Id = orderId,
                                   FestivalId = seeded.FestivalId,
                                   ClientOrderId = Guid.NewGuid(),
                                   GlobalOrderNumber = 1,
                                   StaffMemberId = seeded.StaffMemberId,
                                   TableName = "Tisch 1",
                                   CreatedAtUtc = now
                                 });

    fixture.DbContext.StationOrders.Add(new()
                                        {
                                          Id = stationOrderId,
                                          OrderId = orderId,
                                          FestivalId = seeded.FestivalId,
                                          StationId = seeded.KitchenStationId,
                                          StationOrderNumber = 1,
                                          DeliveryMode = DeliveryMode.Together,
                                          IsHiddenFromAsItComesQueue = false
                                        });

    fixture.DbContext.OrderItems.Add(BuildItem(stationOrderId, seeded.SausageItemId, null));
    fixture.DbContext.OrderItems.Add(BuildItem(stationOrderId, seeded.SausageItemId, now));

    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private OrderItem BuildItem(Guid stationOrderId, Guid catalogItemId, DateTime? fulfilledAtUtc)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             StationOrderId = stationOrderId,
             CatalogItemId = catalogItemId,
             ItemName = "Bratwurst",
             UnitPriceCents = 350,
             Note = null,
             FulfilledAtUtc = fulfilledAtUtc
           };
  }
}
