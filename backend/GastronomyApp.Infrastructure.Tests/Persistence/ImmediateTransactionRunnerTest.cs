using FakeItEasy;
using GastronomyApp.Contracts;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Exceptions;
using GastronomyApp.Core.Results;
using GastronomyApp.Infrastructure.Enums;
using GastronomyApp.Infrastructure.ErrorHandling;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GastronomyApp.Infrastructure.Tests.Persistence;

public sealed class ImmediateTransactionRunnerTest
{
  [Test]
  public void RunAsync_NullBody_ThrowsArgumentNullException()
  {
    using SqliteInMemoryFixture fixture = new();
    var runner = RunnerOn(fixture.DbContext);

    Assert.That(() => runner.RunAsync<int>(null!, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public async Task RunAsync_BodyThrows_LeavesNoTransactionOpenOnTheConnection()
  {
    using SqliteTempFileFixture fixture = new();
    var dbContext = fixture.CreateContext();
    var seeded = await new DomainSeeder().SeedAsync(dbContext, TestContext.CurrentContext.CancellationToken);
    var runner = RunnerOn(dbContext);

    Assert.That(async () => await runner.RunAsync<int>(_ => throw new InvalidOperationException("The body failed."), TestContext.CurrentContext.CancellationToken), Throws.InstanceOf<InvalidOperationException>());

    var acceptanceService = new OrderAcceptanceComposition().Create(dbContext);
    Result<Order, OrderValidationFailure> result = await acceptanceService.AcceptAsync(BuildRequest(seeded), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    Assert.That(result.IsSuccess, Is.True);
  }

  [Test]
  public void RunAsync_NestedCallOnTheSameContext_FailsWithAStatedReason()
  {
    using SqliteInMemoryFixture fixture = new();
    var runner = RunnerOn(fixture.DbContext);

    Assert.That(async () => await runner.RunAsync(async _ => new TransactionOutcome<int>
                                                             {
                                                               Value = await runner.RunAsync(_ => Task.FromResult(new TransactionOutcome<int>
                                                                                                                  {
                                                                                                                    Value = 1,
                                                                                                                    ShouldCommit = true
                                                                                                                  }),
                                                                                             TestContext.CurrentContext.CancellationToken),
                                                               ShouldCommit = true
                                                             },
                                                  TestContext.CurrentContext.CancellationToken),
                Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("already inside"));
  }

  [Test]
  public async Task RunAsync_BodyThrowsWrappedSqliteFailure_ReportsDatabaseUnavailable()
  {
    using SqliteTempFileFixture fixture = new();
    var dbContext = fixture.CreateContext();
    var seeded = await new DomainSeeder().SeedAsync(dbContext, TestContext.CurrentContext.CancellationToken);
    var runner = RunnerOn(dbContext);

    Assert.That(async () => await runner.RunAsync<int>(_ => throw new DbUpdateException("An error occurred while saving the entity changes.", new SqliteException("attempt to write a readonly database", 8)), TestContext.CurrentContext.CancellationToken),
                Throws.InstanceOf<InfrastructureException>().With.Property(nameof(InfrastructureException.Reason)).EqualTo(InfrastructureFailureReason.DatabaseUnavailable).And.InnerException.InstanceOf<SqliteException>());

    var acceptanceService = new OrderAcceptanceComposition().Create(dbContext);
    Result<Order, OrderValidationFailure> result = await acceptanceService.AcceptAsync(BuildRequest(seeded), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    Assert.That(result.IsSuccess, Is.True);
  }

  [Test]
  public void RunAsync_BodyViolatingAUniqueIndex_SurfacesAConflictingChangeRatherThanARawFailure()
  {
    using SqliteInMemoryFixture fixture = new();
    var runner = RunnerOn(fixture.DbContext);
    DateTime now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);

    var failure = Assert.ThrowsAsync<InfrastructureException>(async () => await runner.RunAsync(async transactionCancellationToken =>
                                                                                                {
                                                                                                  fixture.DbContext.EnrolmentInvitations.Add(BuildUnconsumedInvitation(now));
                                                                                                  fixture.DbContext.EnrolmentInvitations.Add(BuildUnconsumedInvitation(now));
                                                                                                  await fixture.DbContext.SaveChangesAsync(transactionCancellationToken);

                                                                                                  return new TransactionOutcome<bool>
                                                                                                         {
                                                                                                           Value = true,
                                                                                                           ShouldCommit = true
                                                                                                         };
                                                                                                },
                                                                                                TestContext.CurrentContext.CancellationToken))!;

    Assert.That(failure.Reason, Is.EqualTo(InfrastructureFailureReason.ConflictingChange));
  }

  [Test]
  public void RunAsync_EveryAttemptLosesTheRowsItRead_GivesUpWithAConcurrentWriteException()
  {
    using SqliteInMemoryFixture fixture = new();
    var runner = RunnerOn(fixture.DbContext);
    var attempts = 0;

    Assert.That(async () => await runner.RunAsync<int>(_ =>
                                                       {
                                                         attempts++;

                                                         throw new DbUpdateConcurrencyException();
                                                       },
                                                       TestContext.CurrentContext.CancellationToken),
                Throws.InstanceOf<ConcurrentWriteException>());

    Assert.That(attempts, Is.EqualTo(5));
  }

  [Test]
  public async Task RunAsync_TheFirstAttemptLosesTheRowsItRead_RunsTheBodyAgainAndReturnsItsValue()
  {
    using SqliteInMemoryFixture fixture = new();
    var runner = RunnerOn(fixture.DbContext);
    var attempts = 0;

    var value = await runner.RunAsync(_ =>
                                      {
                                        attempts++;

                                        if (attempts == 1)
                                          throw new DbUpdateConcurrencyException();

                                        return Task.FromResult(new TransactionOutcome<int>
                                                               {
                                                                 Value = 7,
                                                                 ShouldCommit = true
                                                               });
                                      },
                                      TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(value, Is.EqualTo(7));
                      Assert.That(attempts, Is.EqualTo(2));
                    });
  }

  [Test]
  public void RunAsync_AnAttemptLosesTheRowsItRead_WritesAWarningNamingTheAttempt()
  {
    using SqliteInMemoryFixture fixture = new();
    ILogger<ImmediateTransactionRunner> logger = A.Fake<ILogger<ImmediateTransactionRunner>>();
    ImmediateTransactionRunner runner = new(fixture.DbContext, new(), logger);

    Assert.That(async () => await runner.RunAsync<int>(_ => throw new DbUpdateConcurrencyException(), TestContext.CurrentContext.CancellationToken), Throws.InstanceOf<ConcurrentWriteException>());

    A.CallTo(logger).Where(call => call.Method.Name == nameof(ILogger.Log) && call.GetArgument<LogLevel>(0) == LogLevel.Warning).MustHaveHappened(4, Times.Exactly);
  }

  private ImmediateTransactionRunner RunnerOn(GastronomyAppDbContext dbContext)
  {
    return new(dbContext, new(), NullLogger<ImmediateTransactionRunner>.Instance);
  }

  private PlaceOrderRequest BuildRequest(SeededDomain seeded)
  {
    return new()
           {
             ClientOrderId = Guid.NewGuid(),
             TableName = "Tisch 12",
             Items =
             [
               new()
               {
                 CatalogItemId = seeded.SausageItemId,
                 Note = null,
                 UnitPriceCents = 350
               }
             ]
           };
  }

  private EnrolmentInvitation BuildUnconsumedInvitation(DateTime now)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             QRCodeHash = [1],
             QRCodeSalt = [2],
             QRCodeIterations = 1,
             QRCodeAlgorithm = "PBKDF2-HMAC-SHA512",
             CreatedAtUtc = now,
             ExpiresAtUtc = now.AddMinutes(5),
             ConsumedAtUtc = null,
             ConsumedByDeviceId = null
           };
  }
}
