using System.Data.Common;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class DatabaseUnavailableTest
{
    [Test]
    public async Task AcceptAsync_ReadOnlyDataDirectory_ThrowsInfrastructureExceptionDatabaseUnavailable()
    {
        string path = Path.Combine(Path.GetTempPath(), $"gastronomyapp-test-{Guid.NewGuid():N}.db");
        SeededDomain seeded;

        using (SqliteConnection setupConnection = new($"Data Source={path}"))
        {
            setupConnection.Open();
            using GastronomyAppDbContext setupContext = ContextOn(setupConnection);
            await setupContext.Database.MigrateAsync(TestContext.CurrentContext.CancellationToken);
            seeded = await new DomainSeeder().SeedAsync(setupContext, TestContext.CurrentContext.CancellationToken);
        }

        SqliteConnection.ClearAllPools();
        File.SetAttributes(path, FileAttributes.ReadOnly);

        try
        {
            using SqliteConnection readOnlyConnection = new($"Data Source={path}");
            readOnlyConnection.Open();
            using GastronomyAppDbContext readOnlyContext = ContextOn(readOnlyConnection);
            OrderAcceptanceTransaction transaction = new OrderAcceptanceComposition().Create(readOnlyContext);

            Assert.That(
                async () => await transaction.AcceptAsync(
                    BuildRequest(seeded),
                    TestContext.CurrentContext.CancellationToken),
                Throws.InstanceOf<InfrastructureException>()
                    .With.Property(nameof(InfrastructureException.Reason))
                    .EqualTo(InfrastructureFailureReason.DatabaseUnavailable)
                    .And.InnerException.InstanceOf<SqliteException>());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }
    }

    [Test]
    public async Task AcceptAsync_ConcurrentWriterHoldsLockPastBusyTimeout_ThrowsInfrastructureExceptionDatabaseUnavailable()
    {
        using SqliteTempFileFixture fixture = new();
        GastronomyAppDbContext seedContext = fixture.CreateContext();
        SeededDomain seeded = await new DomainSeeder().SeedAsync(seedContext, TestContext.CurrentContext.CancellationToken);

        GastronomyAppDbContext blockingContext = fixture.CreateContext();
        DbConnection blockingConnection = blockingContext.Database.GetDbConnection();
        await ExecuteAsync(blockingConnection, "BEGIN IMMEDIATE");

        try
        {
            GastronomyAppDbContext blockedContext = fixture.CreateContext();
            await ExecuteAsync(blockedContext.Database.GetDbConnection(), "PRAGMA busy_timeout = 200");
            OrderAcceptanceTransaction transaction = new OrderAcceptanceComposition().Create(blockedContext);

            Assert.That(
                async () => await transaction.AcceptAsync(
                    BuildRequest(seeded),
                    TestContext.CurrentContext.CancellationToken),
                Throws.InstanceOf<InfrastructureException>()
                    .With.Property(nameof(InfrastructureException.Reason))
                    .EqualTo(InfrastructureFailureReason.DatabaseUnavailable));
        }
        finally
        {
            await ExecuteAsync(blockingConnection, "ROLLBACK");
        }
    }

    private async Task ExecuteAsync(DbConnection connection, string statement)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = statement;
        await command.ExecuteNonQueryAsync(TestContext.CurrentContext.CancellationToken);
    }

    private GastronomyAppDbContext ContextOn(SqliteConnection connection)
    {
        return new GastronomyAppDbContext(new DbContextOptionsBuilder<GastronomyAppDbContext>()
            .UseSqlite(connection)
            .Options);
    }

    private OrderAcceptanceRequest BuildRequest(SeededDomain seeded)
    {
        return new OrderAcceptanceRequest
        {
            ClientOrderId = Guid.NewGuid(),
            StaffMemberId = seeded.StaffMemberId,
            DeviceId = seeded.DeviceId,
            TableLabel = "Tisch 12",
            Note = null,
            Lines =
            [
                new OrderAcceptanceLineRequest { CatalogItemId = seeded.SausageItemId, Quantity = 1, Note = null },
            ],
        };
    }
}
