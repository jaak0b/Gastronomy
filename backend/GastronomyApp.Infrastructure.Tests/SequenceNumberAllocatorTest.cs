using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class SequenceNumberAllocatorTest
{
  [Test]
  public async Task AllocateGlobalOrderNumberAsync_FirstOrderOfTheFestival_Returns1()
  {
    using SqliteInMemoryFixture fixture = new();
    SequenceNumberAllocator allocator = new(fixture.DbContext);
    var festivalId = await AddFestivalAsync(fixture.DbContext, "Sommerfest");

    var allocated = await allocator.AllocateGlobalOrderNumberAsync(festivalId,
                                                                   TestContext.CurrentContext.CancellationToken);

    Assert.That(allocated, Is.EqualTo(1));
  }

  [Test]
  public async Task AllocateGlobalOrderNumberAsync_SecondFestival_StartsAt1OfItsOwn()
  {
    using SqliteInMemoryFixture fixture = new();
    SequenceNumberAllocator allocator = new(fixture.DbContext);
    var firstFestivalId = await AddFestivalAsync(fixture.DbContext, "Sommerfest");
    var secondFestivalId = await AddFestivalAsync(fixture.DbContext, "Herbstfest");

    await allocator.AllocateGlobalOrderNumberAsync(firstFestivalId, TestContext.CurrentContext.CancellationToken);
    await allocator.AllocateGlobalOrderNumberAsync(firstFestivalId, TestContext.CurrentContext.CancellationToken);
    var secondFestivalFirstNumber =
      await allocator.AllocateGlobalOrderNumberAsync(secondFestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(secondFestivalFirstNumber, Is.EqualTo(1));
  }

  [Test]
  public async Task AllocateGlobalOrderNumberAsync_AfterNRolledBackTransactions_DoesNotSkipNumbers()
  {
    using SqliteTempFileFixture fixture = new();

    var seedingContext = fixture.CreateContext();
    var festivalId = await AddFestivalAsync(seedingContext, "Sommerfest");
    seedingContext.Dispose();

    var rolledBackValue = 0;
    for (var attempt = 0; attempt < 3; attempt++)
    {
      var attemptContext = fixture.CreateContext();
      await using var transaction = await attemptContext.Database.BeginTransactionAsync();
      rolledBackValue = await new SequenceNumberAllocator(attemptContext)
                         .AllocateGlobalOrderNumberAsync(festivalId, TestContext.CurrentContext.CancellationToken);
      await transaction.RollbackAsync();
      attemptContext.Dispose();
    }

    var secondContext = fixture.CreateContext();
    var afterRollback = await new SequenceNumberAllocator(secondContext)
                         .AllocateGlobalOrderNumberAsync(festivalId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(rolledBackValue, Is.EqualTo(1));
                      Assert.That(afterRollback, Is.EqualTo(rolledBackValue));
                    });
  }

  [Test]
  public async Task AllocateGlobalOrderNumberAsync_SimulatedRestart_ContinuesFromPersistedValue()
  {
    using SqliteTempFileFixture fixture = new();

    var beforeRestart = fixture.CreateContext();
    var festivalId = await AddFestivalAsync(beforeRestart, "Sommerfest");
    await new SequenceNumberAllocator(beforeRestart).AllocateGlobalOrderNumberAsync(festivalId, TestContext.CurrentContext.CancellationToken);
    await new SequenceNumberAllocator(beforeRestart).AllocateGlobalOrderNumberAsync(festivalId, TestContext.CurrentContext.CancellationToken);
    beforeRestart.Dispose();

    var afterRestart = fixture.CreateContext();
    var allocated = await new SequenceNumberAllocator(afterRestart)
                     .AllocateGlobalOrderNumberAsync(festivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(allocated, Is.EqualTo(3));
  }

  [Test]
  public async Task AllocateStationOrderNumberAsync_TwoStations_CountIndependently()
  {
    using SqliteInMemoryFixture fixture = new();
    SequenceNumberAllocator allocator = new(fixture.DbContext);
    var festivalId = await AddFestivalAsync(fixture.DbContext, "Sommerfest");
    var firstStationId = await AddStationAsync(fixture.DbContext, festivalId, "Kueche");
    var secondStationId = await AddStationAsync(fixture.DbContext, festivalId, "Theke");

    await allocator.AllocateStationOrderNumberAsync(festivalId, firstStationId, TestContext.CurrentContext.CancellationToken);
    await allocator.AllocateStationOrderNumberAsync(festivalId, firstStationId, TestContext.CurrentContext.CancellationToken);
    var secondStationFirstNumber = await allocator.AllocateStationOrderNumberAsync(festivalId,
                                                                                   secondStationId,
                                                                                   TestContext.CurrentContext.CancellationToken);

    Assert.That(secondStationFirstNumber, Is.EqualTo(1));
  }

  [Test]
  public async Task AllocateStationOrderNumberAsync_TheSameStationAtASecondFestival_StartsAt1Again()
  {
    using SqliteInMemoryFixture fixture = new();
    SequenceNumberAllocator allocator = new(fixture.DbContext);
    var firstFestivalId = await AddFestivalAsync(fixture.DbContext, "Sommerfest");
    var secondFestivalId = await AddFestivalAsync(fixture.DbContext, "Herbstfest");
    var stationId = await AddStationAsync(fixture.DbContext, firstFestivalId, "Kueche");
    await LinkStationAsync(fixture.DbContext, secondFestivalId, stationId);

    await allocator.AllocateStationOrderNumberAsync(firstFestivalId, stationId, TestContext.CurrentContext.CancellationToken);
    await allocator.AllocateStationOrderNumberAsync(firstFestivalId, stationId, TestContext.CurrentContext.CancellationToken);
    var atTheSecondFestival = await allocator.AllocateStationOrderNumberAsync(secondFestivalId,
                                                                              stationId,
                                                                              TestContext.CurrentContext.CancellationToken);

    Assert.That(atTheSecondFestival, Is.EqualTo(1));
  }

  [Test]
  public async Task AllocateGlobalOrderNumberAsync_TwoContextsThatBothReadTheCounterBeforeEitherSaves_RefusesTheSecondSave()
  {
    using SqliteInMemoryFixture fixture = new();
    var festivalId = await AddFestivalAsync(fixture.DbContext, "Sommerfest");
    using var firstContext = fixture.CreateContext();
    using var secondContext = fixture.CreateContext();
    await firstContext.Festivals.FirstAsync(candidate => candidate.Id == festivalId,
                                            TestContext.CurrentContext.CancellationToken);
    await secondContext.Festivals.FirstAsync(candidate => candidate.Id == festivalId,
                                             TestContext.CurrentContext.CancellationToken);

    var firstNumber = await new SequenceNumberAllocator(firstContext)
                       .AllocateGlobalOrderNumberAsync(festivalId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(firstNumber, Is.EqualTo(1));
                      Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
                                                                         await new SequenceNumberAllocator(secondContext)
                                                                          .AllocateGlobalOrderNumberAsync(festivalId,
                                                                                                          TestContext.CurrentContext.CancellationToken));
                    });
  }

  [Test]
  public async Task AllocateStationOrderNumberAsync_TwoContextsThatBothReadTheCounterBeforeEitherSaves_RefusesTheSecondSave()
  {
    using SqliteInMemoryFixture fixture = new();
    var festivalId = await AddFestivalAsync(fixture.DbContext, "Sommerfest");
    var stationId = await AddStationAsync(fixture.DbContext, festivalId, "Kueche");
    using var firstContext = fixture.CreateContext();
    using var secondContext = fixture.CreateContext();
    await firstContext.FestivalStations.FirstAsync(candidate => candidate.FestivalId == festivalId,
                                                   TestContext.CurrentContext.CancellationToken);
    await secondContext.FestivalStations.FirstAsync(candidate => candidate.FestivalId == festivalId,
                                                    TestContext.CurrentContext.CancellationToken);

    var firstNumber = await new SequenceNumberAllocator(firstContext)
                       .AllocateStationOrderNumberAsync(festivalId, stationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(firstNumber, Is.EqualTo(1));
                      Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
                                                                         await new SequenceNumberAllocator(secondContext)
                                                                          .AllocateStationOrderNumberAsync(festivalId,
                                                                                                           stationId,
                                                                                                           TestContext.CurrentContext.CancellationToken));
                    });
  }

  private async Task<Guid> AddFestivalAsync(GastronomyAppDbContext dbContext, string name)
  {
    var festivalId = Guid.NewGuid();

    dbContext.Festivals.Add(new()
                            {
                              Id = festivalId,
                              Name = name,
                              StartsAtUtc = DateTime.UtcNow.AddDays(-1),
                              EndsAtUtc = DateTime.UtcNow.AddDays(1),
                              NextOrderNumber = 1,
                              IsHidden = false
                            });

    await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return festivalId;
  }

  private async Task<Guid> AddStationAsync(GastronomyAppDbContext dbContext, Guid festivalId, string name)
  {
    var stationId = Guid.NewGuid();

    dbContext.Stations.Add(new()
                           {
                             Id = stationId,
                             Name = name,
                             SortOrder = 1,
                             IsActive = true
                           });

    await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    await LinkStationAsync(dbContext, festivalId, stationId);

    return stationId;
  }

  private async Task LinkStationAsync(GastronomyAppDbContext dbContext, Guid festivalId, Guid stationId)
  {
    dbContext.FestivalStations.Add(new()
                                   {
                                     Id = Guid.NewGuid(),
                                     FestivalId = festivalId,
                                     StationId = stationId,
                                     NextStationOrderNumber = 1
                                   });

    await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }
}
