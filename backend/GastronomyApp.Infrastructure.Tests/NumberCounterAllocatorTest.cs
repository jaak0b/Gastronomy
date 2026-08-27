using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class NumberCounterAllocatorTest
{
    [Test]
    public async Task AllocateGlobalOrderNumberAsync_FirstCallForSession_Returns1()
    {
        using SqliteInMemoryFixture fixture = new();
        NumberCounterAllocator allocator = new(fixture.DbContext);

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
            rolledBackValue = await new NumberCounterAllocator(attemptContext)
                .AllocateGlobalOrderNumberAsync(TestContext.CurrentContext.CancellationToken);
            await transaction.RollbackAsync();
            attemptContext.Dispose();
        }

        GastronomyAppDbContext secondContext = fixture.CreateContext();
        int afterRollback = await new NumberCounterAllocator(secondContext)
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
        await new NumberCounterAllocator(beforeRestart).AllocateGlobalOrderNumberAsync(TestContext.CurrentContext.CancellationToken);
        await new NumberCounterAllocator(beforeRestart).AllocateGlobalOrderNumberAsync(TestContext.CurrentContext.CancellationToken);
        beforeRestart.Dispose();

        GastronomyAppDbContext afterRestart = fixture.CreateContext();
        int allocated = await new NumberCounterAllocator(afterRestart)
            .AllocateGlobalOrderNumberAsync(TestContext.CurrentContext.CancellationToken);

        Assert.That(allocated, Is.EqualTo(3));
    }

    [Test]
    public async Task AllocateStationSequenceNumberAsync_TwoStations_CountIndependently()
    {
        using SqliteInMemoryFixture fixture = new();
        NumberCounterAllocator allocator = new(fixture.DbContext);
        Guid eventSessionId = Guid.NewGuid();
        Guid firstStationId = Guid.NewGuid();
        Guid secondStationId = Guid.NewGuid();

        await allocator.AllocateStationSequenceNumberAsync(firstStationId, TestContext.CurrentContext.CancellationToken);
        await allocator.AllocateStationSequenceNumberAsync(firstStationId, TestContext.CurrentContext.CancellationToken);
        int secondStationFirstNumber = await allocator.AllocateStationSequenceNumberAsync(secondStationId, TestContext.CurrentContext.CancellationToken);

        Assert.That(secondStationFirstNumber, Is.EqualTo(1));
    }

    [Test]
    public async Task AllocatePrinterProcessIdAsync_At9999_WrapsTo1AndPersists()
    {
        using SqliteTempFileFixture fixture = new();
        const string printerEndpointKey = "Network|192.168.1.50|9100|";

        GastronomyAppDbContext context = fixture.CreateContext();
        await new NumberCounterAllocator(context).AllocatePrinterProcessIdAsync(printerEndpointKey, TestContext.CurrentContext.CancellationToken);

        NumberCounter counter = await context.NumberCounters
            .SingleAsync(candidate => candidate.CounterKind == NumberCounterKind.PrinterProcessId, TestContext.CurrentContext.CancellationToken);
        counter.NextValue = 9999;
        await context.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

        int lastBeforeWrap = await new NumberCounterAllocator(context).AllocatePrinterProcessIdAsync(printerEndpointKey, TestContext.CurrentContext.CancellationToken);
        context.Dispose();

        GastronomyAppDbContext afterRestart = fixture.CreateContext();
        int afterWrap = await new NumberCounterAllocator(afterRestart).AllocatePrinterProcessIdAsync(printerEndpointKey, TestContext.CurrentContext.CancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(lastBeforeWrap, Is.EqualTo(9999));
            Assert.That(afterWrap, Is.EqualTo(1));
        });
    }
}
