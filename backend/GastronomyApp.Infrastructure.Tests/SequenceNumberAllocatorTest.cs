using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class SequenceNumberAllocatorTest
{
  [Test]
  public async Task AllocateGlobalOrderNumberAsync_FirstCallForSession_Returns1()
  {
    using SqliteInMemoryFixture fixture = new();
    SequenceNumberAllocator allocator = new(fixture.DbContext);

    var allocated = await allocator.AllocateGlobalOrderNumberAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(allocated, Is.EqualTo(1));
  }

  [Test]
  public async Task AllocateGlobalOrderNumberAsync_AfterNRolledBackTransactions_DoesNotSkipNumbers()
  {
    using SqliteTempFileFixture fixture = new();
    var eventSessionId = Guid.NewGuid();

    var rolledBackValue = 0;
    for (var attempt = 0; attempt < 3; attempt++)
    {
      var attemptContext = fixture.CreateContext();
      await using var transaction = await attemptContext.Database.BeginTransactionAsync();
      rolledBackValue = await new SequenceNumberAllocator(attemptContext)
                         .AllocateGlobalOrderNumberAsync(TestContext.CurrentContext.CancellationToken);
      await transaction.RollbackAsync();
      attemptContext.Dispose();
    }

    var secondContext = fixture.CreateContext();
    var afterRollback = await new SequenceNumberAllocator(secondContext)
                         .AllocateGlobalOrderNumberAsync(TestContext.CurrentContext.CancellationToken);

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
    var eventSessionId = Guid.NewGuid();

    var beforeRestart = fixture.CreateContext();
    await new SequenceNumberAllocator(beforeRestart).AllocateGlobalOrderNumberAsync(TestContext.CurrentContext.CancellationToken);
    await new SequenceNumberAllocator(beforeRestart).AllocateGlobalOrderNumberAsync(TestContext.CurrentContext.CancellationToken);
    beforeRestart.Dispose();

    var afterRestart = fixture.CreateContext();
    var allocated = await new SequenceNumberAllocator(afterRestart)
                     .AllocateGlobalOrderNumberAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(allocated, Is.EqualTo(3));
  }

  [Test]
  public async Task AllocateStationOrderNumberAsync_TwoStations_CountIndependently()
  {
    using SqliteInMemoryFixture fixture = new();
    SequenceNumberAllocator allocator = new(fixture.DbContext);
    var firstStationId = await AddStationAsync(fixture.DbContext, "Kueche");
    var secondStationId = await AddStationAsync(fixture.DbContext, "Theke");

    await allocator.AllocateStationOrderNumberAsync(firstStationId, TestContext.CurrentContext.CancellationToken);
    await allocator.AllocateStationOrderNumberAsync(firstStationId, TestContext.CurrentContext.CancellationToken);
    var secondStationFirstNumber = await allocator.AllocateStationOrderNumberAsync(secondStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(secondStationFirstNumber, Is.EqualTo(1));
  }

  private async static Task<Guid> AddStationAsync(GastronomyAppDbContext dbContext, string name)
  {
    var stationId = Guid.NewGuid();

    dbContext.Stations.Add(new()
                           {
                             Id = stationId,
                             Name = name,
                             SortOrder = 1,
                             IsActive = true,
                             NextStationOrderNumber = 1
                           });

    await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return stationId;
  }
}
