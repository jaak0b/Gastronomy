using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class SequenceNumberAllocatorTest
{
    [Test]
    public async Task AllocateGlobalOrderNumberAsync_FirstCallForSession_Returns1()
    {
        using SqliteInMemoryFixture fixture = new();
        SequenceNumberAllocator allocator = new(fixture.DbContext);

        int allocated = await allocator.AllocateGlobalOrderNumberAsync(TestContext.CurrentContext.CancellationToken);

        Assert.That(allocated, Is.EqualTo(1));
    }

    [Test]
    public async Task AllocateGlobalOrderNumberAsync_AfterNRolledBackTransactions_DoesNotSkipNumbers()
    {
        using SqliteTempFileFixture fixture = new();
        Guid eventSessionId = Guid.NewGuid();

        int rolledBackValue = 0;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            GastronomyAppDbContext attemptContext = fixture.CreateContext();
            await using IDbContextTransaction transaction = await attemptContext.Database.BeginTransactionAsync();
            rolledBackValue = await new SequenceNumberAllocator(attemptContext)
                .AllocateGlobalOrderNumberAsync(TestContext.CurrentContext.CancellationToken);
            await transaction.RollbackAsync();
            attemptContext.Dispose();
        }

        GastronomyAppDbContext secondContext = fixture.CreateContext();
        int afterRollback = await new SequenceNumberAllocator(secondContext)
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
        Guid eventSessionId = Guid.NewGuid();

        GastronomyAppDbContext beforeRestart = fixture.CreateContext();
        await new SequenceNumberAllocator(beforeRestart).AllocateGlobalOrderNumberAsync(TestContext.CurrentContext.CancellationToken);
        await new SequenceNumberAllocator(beforeRestart).AllocateGlobalOrderNumberAsync(TestContext.CurrentContext.CancellationToken);
        beforeRestart.Dispose();

        GastronomyAppDbContext afterRestart = fixture.CreateContext();
        int allocated = await new SequenceNumberAllocator(afterRestart)
            .AllocateGlobalOrderNumberAsync(TestContext.CurrentContext.CancellationToken);

        Assert.That(allocated, Is.EqualTo(3));
    }

    [Test]
    public async Task AllocateStationOrderNumberAsync_TwoStations_CountIndependently()
    {
        using SqliteInMemoryFixture fixture = new();
        SequenceNumberAllocator allocator = new(fixture.DbContext);
        Guid firstStationId = await AddStationAsync(fixture.DbContext, "Kueche");
        Guid secondStationId = await AddStationAsync(fixture.DbContext, "Theke");

        await allocator.AllocateStationOrderNumberAsync(firstStationId, TestContext.CurrentContext.CancellationToken);
        await allocator.AllocateStationOrderNumberAsync(firstStationId, TestContext.CurrentContext.CancellationToken);
        int secondStationFirstNumber = await allocator.AllocateStationOrderNumberAsync(secondStationId, TestContext.CurrentContext.CancellationToken);

        Assert.That(secondStationFirstNumber, Is.EqualTo(1));
    }

    [Test]
    public async Task AllocatePrinterJobIdAsync_At9999_WrapsTo1AndPersists()
    {
        using SqliteTempFileFixture fixture = new();

        GastronomyAppDbContext context = fixture.CreateContext();
        await new SequenceNumberAllocator(context).AllocatePrinterJobIdAsync(TestContext.CurrentContext.CancellationToken);

        SequenceCounters counters = await context.SequenceCounters
            .SingleAsync(TestContext.CurrentContext.CancellationToken);
        counters.NextPrinterJobId = 9999;
        await context.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

        int lastBeforeWrap = await new SequenceNumberAllocator(context).AllocatePrinterJobIdAsync(TestContext.CurrentContext.CancellationToken);
        context.Dispose();

        GastronomyAppDbContext afterRestart = fixture.CreateContext();
        int afterWrap = await new SequenceNumberAllocator(afterRestart).AllocatePrinterJobIdAsync(TestContext.CurrentContext.CancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(lastBeforeWrap, Is.EqualTo(9999));
            Assert.That(afterWrap, Is.EqualTo(1));
        });
    }

    private static async Task<Guid> AddStationAsync(GastronomyAppDbContext dbContext, string name)
    {
        Guid stationId = Guid.NewGuid();

        dbContext.Stations.Add(new Station
        {
            Id = stationId,
            Name = name,
            SortOrder = 1,
            IsActive = true,
            NextStationOrderNumber = 1,
        });

        await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

        return stationId;
    }
}
