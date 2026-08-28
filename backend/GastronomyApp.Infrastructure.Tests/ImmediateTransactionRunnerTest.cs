using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class ImmediateTransactionRunnerTest
{
  [Test]
  public async Task RunAsync_BodyThrows_LeavesNoTransactionOpenOnTheConnection()
  {
    using SqliteTempFileFixture fixture = new();
    var dbContext = fixture.CreateContext();
    var seeded = await new DomainSeeder().SeedAsync(dbContext, TestContext.CurrentContext.CancellationToken);
    ImmediateTransactionRunner runner = new();

    Assert.That(async () => await runner.RunAsync<int>(dbContext,
                                                       _ => throw new InvalidOperationException("The body failed."),
                                                       TestContext.CurrentContext.CancellationToken),
                Throws.InstanceOf<InvalidOperationException>());

    var transaction = new OrderAcceptanceComposition().Create(dbContext);
    Result<OrderAcceptanceResult, OrderValidationFailure> result = await transaction.AcceptAsync(BuildRequest(seeded),
                                                                                                 TestContext.CurrentContext.CancellationToken);

    Assert.That(result.IsSuccess, Is.True);
  }

  [Test]
  public async Task RunAsync_NestedCallOnTheSameContext_FailsWithAStatedReason()
  {
    using SqliteInMemoryFixture fixture = new();
    ImmediateTransactionRunner runner = new();

    Assert.That(async () => await runner.RunAsync(fixture.DbContext,
                                                  async _ => new TransactionOutcome<int>
                                                             {
                                                               Value = await runner.RunAsync(fixture.DbContext,
                                                                                             _ => Task.FromResult(new TransactionOutcome<int> { Value = 1, ShouldCommit = true }),
                                                                                             TestContext.CurrentContext.CancellationToken),
                                                               ShouldCommit = true
                                                             },
                                                  TestContext.CurrentContext.CancellationToken),
                Throws.InstanceOf<InvalidOperationException>()
                      .With.Message.Contains("already inside"));
  }

  [Test]
  public async Task RunAsync_BodyThrowsWrappedSqliteFailure_ReportsDatabaseUnavailable()
  {
    using SqliteTempFileFixture fixture = new();
    var dbContext = fixture.CreateContext();
    var seeded = await new DomainSeeder().SeedAsync(dbContext, TestContext.CurrentContext.CancellationToken);
    ImmediateTransactionRunner runner = new();

    Assert.That(async () => await runner.RunAsync<int>(dbContext,
                                                       _ => throw new DbUpdateException("An error occurred while saving the entity changes.",
                                                                                        new SqliteException("attempt to write a readonly database", 8)),
                                                       TestContext.CurrentContext.CancellationToken),
                Throws.InstanceOf<InfrastructureException>()
                      .With.Property(nameof(InfrastructureException.Reason))
                      .EqualTo(InfrastructureFailureReason.DatabaseUnavailable)
                      .And.InnerException.InstanceOf<SqliteException>());

    var transaction = new OrderAcceptanceComposition().Create(dbContext);
    Result<OrderAcceptanceResult, OrderValidationFailure> result = await transaction.AcceptAsync(BuildRequest(seeded),
                                                                                                 TestContext.CurrentContext.CancellationToken);

    Assert.That(result.IsSuccess, Is.True);
  }

  private OrderAcceptanceRequest BuildRequest(SeededDomain seeded)
  {
    return new()
           {
             ClientOrderId = Guid.NewGuid(),
             StaffMemberId = seeded.StaffMemberId,
             TableName = "Tisch 12",
             Note = null,
             Items =
             [
               new()
               {
                 CatalogItemId = seeded.SausageItemId, Note = null,
                 UnitPriceCents = 350
               }
             ]
           };
  }

  [Test]
  public void RunAsync_BodyViolatingAUniqueIndex_SurfacesAConflictingChangeRatherThanARawFailure()
  {
    using SqliteInMemoryFixture fixture = new();
    ImmediateTransactionRunner runner = new();
    DateTime now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);

    var failure = Assert.ThrowsAsync<InfrastructureException>(async () => await runner.RunAsync(fixture.DbContext,
                                                                                                async transactionCancellationToken =>
                                                                                                {
                                                                                                  fixture.DbContext.EnrolmentInvitations.Add(BuildUnconsumedInvitation(now));
                                                                                                  fixture.DbContext.EnrolmentInvitations.Add(BuildUnconsumedInvitation(now));
                                                                                                  await fixture.DbContext.SaveChangesAsync(transactionCancellationToken);

                                                                                                  return new TransactionOutcome<bool> { Value = true, ShouldCommit = true };
                                                                                                },
                                                                                                TestContext.CurrentContext.CancellationToken))!;

    Assert.That(failure.Reason, Is.EqualTo(InfrastructureFailureReason.ConflictingChange));
  }

  private EnrolmentInvitation BuildUnconsumedInvitation(DateTime now)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             StaffMemberId = null,
             QrCodeHash = [1],
             QrCodeSalt = [2],
             QrCodeIterations = 1,
             QrCodeAlgorithm = "PBKDF2-HMAC-SHA512",
             CreatedAtUtc = now,
             ExpiresAtUtc = now.AddMinutes(5),
             ConsumedAtUtc = null,
             ConsumedByDeviceId = null
           };
  }
}
