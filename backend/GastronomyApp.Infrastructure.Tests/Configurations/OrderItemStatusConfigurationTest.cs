using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Configurations;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Configurations;

[TestFixture]
public sealed class OrderItemStatusConfigurationTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    DomainSeeder seeder = new();
    _seeded = await seeder.SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var tableId = await seeder.SeedTableAsync(_fixture.DbContext, _seeded.FestivalId, "Tisch 7", TestContext.CurrentContext.CancellationToken);
    await new TablePlanSeeder().AddOrderAtKitchenAndBarAsync(_fixture.DbContext, _seeded, tableId, 1, _handedOutAtUtc);
    _fixture.DbContext.ChangeTracker.Clear();
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private readonly DateTime _handedOutAtUtc = new(2026, 8, 27, 18, 45, 0, DateTimeKind.Utc);

  private SqliteInMemoryFixture _fixture = null!;
  private SeededDomain _seeded = null!;

  [Test]
  public void Configure_NullBuilder_ThrowsArgumentNullException()
  {
    OrderItemStatusConfiguration configuration = new();

    Assert.That(() => configuration.Configure(null!), Throws.ArgumentNullException);
  }

  [Test]
  public async Task Save_TheSeededOrder_LeavesNoItemWithoutACurrentStatusOfItsOwn()
  {
    await using var readContext = _fixture.CreateContext();
    List<OrderItem> items = await readContext.OrderItems.AsNoTracking().ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(items, Has.Count.EqualTo(2));
                      Assert.That(items.Select(item => item.CurrentStatus), Is.All.Not.Null);
                      Assert.That(items.Select(item => item.CurrentStatus!.OrderItemId), Is.EqualTo(items.Select(item => item.Id)));
                    });
  }

  [Test]
  public async Task Save_AStatusNamingBothAWaiterAndAStation_IsRefusedByTheDatabase()
  {
    var refused = await SaveFollowerOfTheOpenBarItemAsync(_seeded.StaffMemberId, _seeded.BarStationId);

    Assert.That(refused.InnerException!.Message, Does.Contain("CK_OrderItemStatuses_ExactlyOneCreator"));
  }

  [Test]
  public async Task Save_AStatusNamingNeitherAWaiterNorAStation_IsRefusedByTheDatabase()
  {
    var refused = await SaveFollowerOfTheOpenBarItemAsync(null, null);

    Assert.That(refused.InnerException!.Message, Does.Contain("CK_OrderItemStatuses_ExactlyOneCreator"));
  }

  [Test]
  public async Task Save_ASecondStatusFollowingTheSameOne_IsRefusedByTheDatabase()
  {
    var refused = await SaveFollowerOfTheHandedOutKitchenItemsOpenStatusAsync();

    Assert.That(refused.InnerException!.Message, Does.Contain("UNIQUE"));
  }

  [Test]
  public async Task Save_DeletingAnOrder_RemovesItsItemsAndTheirWholeHistory()
  {
    await using (var deleteContext = _fixture.CreateContext())
    {
      deleteContext.Orders.Remove(await deleteContext.Orders.SingleAsync(TestContext.CurrentContext.CancellationToken));
      await deleteContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    }

    await using var readContext = _fixture.CreateContext();

    Assert.Multiple(async () =>
                    {
                      Assert.That(await readContext.OrderItems.CountAsync(TestContext.CurrentContext.CancellationToken), Is.Zero);
                      Assert.That(await readContext.OrderItemStatuses.CountAsync(TestContext.CurrentContext.CancellationToken), Is.Zero);
                    });
  }

  private async Task<DbUpdateException> SaveFollowerOfTheOpenBarItemAsync(Guid? staffMemberId, Guid? stationId)
  {
    await using var writeContext = _fixture.CreateContext();
    var barItem = await writeContext.OrderItems.SingleAsync(item => item.StationOrder.StationId == _seeded.BarStationId, TestContext.CurrentContext.CancellationToken);
    writeContext.OrderItemStatuses.Add(new()
                                       {
                                         Id = Guid.NewGuid(),
                                         OrderItemId = barItem.Id,
                                         PreviousStatusId = barItem.CurrentStatusId,
                                         Status = OrderItemState.HandedOut,
                                         CreatedAtUtc = _handedOutAtUtc,
                                         CreatedByStaffMemberId = staffMemberId,
                                         CreatedByStationId = stationId
                                       });

    return Assert.ThrowsAsync<DbUpdateException>(async () => await writeContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken))!;
  }

  private async Task<DbUpdateException> SaveFollowerOfTheHandedOutKitchenItemsOpenStatusAsync()
  {
    await using var writeContext = _fixture.CreateContext();
    var openStatus = await writeContext.OrderItemStatuses.SingleAsync(status => status.Status == OrderItemState.Open && status.OrderItem.StationOrder.StationId == _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    writeContext.OrderItemStatuses.Add(new()
                                       {
                                         Id = Guid.NewGuid(),
                                         OrderItemId = openStatus.OrderItemId,
                                         PreviousStatusId = openStatus.Id,
                                         Status = OrderItemState.HandedOut,
                                         CreatedAtUtc = _handedOutAtUtc.AddMinutes(1),
                                         CreatedByStationId = _seeded.KitchenStationId
                                       });

    return Assert.ThrowsAsync<DbUpdateException>(async () => await writeContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken))!;
  }
}
