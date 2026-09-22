using ErrorOr;
using GastronomyApp.Api.Tests.TestSupport;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Enums;
using GastronomyApp.Infrastructure.ErrorHandling;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Filters;

public sealed class RequestTransactionFilterTest
{
  private TransactionProbeHost _host = null!;

  [SetUp]
  public async Task SetUp()
  {
    _host = await new TransactionProbeHostBuilder().StartAsync();
  }

  [TearDown]
  public async Task TearDown()
  {
    await _host.DisposeAsync();
  }

  [TestCase("", 409)]
  [TestCase("/created", 409)]
  [TestCase("/no-content", 409)]
  public async Task Post_TheFirstServiceCallWritesAndTheSecondRefuses_LeavesNothingInTheDatabaseAndAnswersTheRefusal(string route, int expectedStatusCode)
  {
    _host.Probe.Handle = async (database, _, cancellationToken) =>
                         {
                           await WriteCategoryAsync(database, "Getraenke", cancellationToken);

                           return Error.Custom(expectedStatusCode, "the.second.call.refused", "The second service call refused the change.");
                         };

    using var response = await _host.Client.PostAsync($"/probe{route}", null, TestContext.CurrentContext.CancellationToken);
    var storedCategories = await StoredCategoriesAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(storedCategories, Is.Zero);
                      Assert.That((int)response.StatusCode, Is.EqualTo(expectedStatusCode));
                    });
  }

  [TestCase("", 200)]
  [TestCase("/created", 201)]
  [TestCase("/no-content", 204)]
  public async Task Post_TheHandlerAnswersWithAValue_CommitsTheWriteAndAnswersItsStatus(string route, int expectedStatusCode)
  {
    _host.Probe.Handle = async (database, _, cancellationToken) => await WriteCategoryAsync(database, "Getraenke", cancellationToken);

    using var response = await _host.Client.PostAsync($"/probe{route}", null, TestContext.CurrentContext.CancellationToken);
    var storedCategories = await StoredCategoriesAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(storedCategories, Is.EqualTo(1));
                      Assert.That((int)response.StatusCode, Is.EqualTo(expectedStatusCode));
                    });
  }

  [Test]
  public async Task Post_TheHandlerThrows_LeavesNothingInTheDatabase()
  {
    _host.Probe.Handle = async (database, _, cancellationToken) =>
                         {
                           await WriteCategoryAsync(database, "Getraenke", cancellationToken);

                           throw new InvalidOperationException("The handler failed after it had written.");
                         };

    using var response = await _host.Client.PostAsync("/probe", null, TestContext.CurrentContext.CancellationToken);
    var storedCategories = await StoredCategoriesAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(storedCategories, Is.Zero);
                      Assert.That(_host.Probe.Failure, Is.InstanceOf<InvalidOperationException>());
                    });
  }

  [Test]
  public async Task Post_TheFirstAttemptLosesTheRowsItRead_RunsTheHandlerAgainAndCommitsTheSecondAttempt()
  {
    var attempts = 0;

    _host.Probe.Handle = async (database, _, cancellationToken) =>
                         {
                           attempts++;

                           if (attempts == 1)
                             throw new DbUpdateConcurrencyException();

                           return await WriteCategoryAsync(database, "Getraenke", cancellationToken);
                         };

    using var response = await _host.Client.PostAsync("/probe", null, TestContext.CurrentContext.CancellationToken);
    var storedCategories = await StoredCategoriesAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(attempts, Is.EqualTo(2));
                      Assert.That(storedCategories, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task Post_EveryAttemptLosesTheRowsItRead_GivesUpAfterFiveAttempts()
  {
    var attempts = 0;

    _host.Probe.Handle = (_, _, _) =>
                         {
                           attempts++;

                           throw new DbUpdateConcurrencyException();
                         };

    using var response = await _host.Client.PostAsync("/probe", null, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(attempts, Is.EqualTo(5));
                      Assert.That((_host.Probe.Failure as InfrastructureException)?.Reason, Is.EqualTo(InfrastructureFailureReason.ConflictingChange));
                    });
  }

  [Test]
  public async Task Post_TheHandlerWaitsForTheCommit_RunsThatStepOnceAfterTheCommit()
  {
    var runsOfTheStep = 0;

    _host.Probe.Handle = async (database, afterCommitActions, cancellationToken) =>
                         {
                           await afterCommitActions.RunWhenCommittedAsync(_ =>
                                                                          {
                                                                            runsOfTheStep++;

                                                                            return Task.CompletedTask;
                                                                          },
                                                                          cancellationToken);

                           return await WriteCategoryAsync(database, "Getraenke", cancellationToken);
                         };

    using var response = await _host.Client.PostAsync("/probe", null, TestContext.CurrentContext.CancellationToken);

    Assert.That(runsOfTheStep, Is.EqualTo(1));
  }

  [Test]
  public async Task Post_TheFirstAttemptWaitsForTheCommitAndLosesTheRowsItRead_RunsThatStepOnlyForTheCommittedAttempt()
  {
    var attempts = 0;
    var runsOfTheStep = 0;

    _host.Probe.Handle = async (database, afterCommitActions, cancellationToken) =>
                         {
                           attempts++;

                           await afterCommitActions.RunWhenCommittedAsync(_ =>
                                                                          {
                                                                            runsOfTheStep++;

                                                                            return Task.CompletedTask;
                                                                          },
                                                                          cancellationToken);

                           if (attempts == 1)
                             throw new DbUpdateConcurrencyException();

                           return await WriteCategoryAsync(database, "Getraenke", cancellationToken);
                         };

    using var response = await _host.Client.PostAsync("/probe", null, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(attempts, Is.EqualTo(2));
                      Assert.That(runsOfTheStep, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task Post_TheHandlerRefuses_LeavesTheStepWaitingForTheCommitUnrun()
  {
    var runsOfTheStep = 0;

    _host.Probe.Handle = async (_, afterCommitActions, cancellationToken) =>
                         {
                           await afterCommitActions.RunWhenCommittedAsync(_ =>
                                                                          {
                                                                            runsOfTheStep++;

                                                                            return Task.CompletedTask;
                                                                          },
                                                                          cancellationToken);

                           return Error.Custom(409, "the.handler.refused", "The handler refused the change.");
                         };

    using var response = await _host.Client.PostAsync("/probe", null, TestContext.CurrentContext.CancellationToken);

    Assert.That(runsOfTheStep, Is.Zero);
  }

  [Test]
  public async Task Post_TheHandlerWritesTwoRowsUnderOneUniqueIndex_SurfacesAConflictingChange()
  {
    _host.Probe.Handle = async (database, _, cancellationToken) =>
                         {
                           await WriteCategoryAsync(database, "Getraenke", cancellationToken);

                           return await WriteCategoryAsync(database, "Getraenke", cancellationToken);
                         };

    using var response = await _host.Client.PostAsync("/probe", null, TestContext.CurrentContext.CancellationToken);

    Assert.That((_host.Probe.Failure as InfrastructureException)?.Reason, Is.EqualTo(InfrastructureFailureReason.ConflictingChange));
  }

  [Test]
  public async Task Post_ADatabaseFileNothingMayWriteTo_SurfacesAnUnavailableDatabase()
  {
    _host.Probe.Handle = async (database, _, cancellationToken) => await WriteCategoryAsync(database, "Getraenke", cancellationToken);

    SqliteConnection.ClearAllPools();
    File.SetAttributes(_host.DatabasePath, FileAttributes.ReadOnly);

    try
    {
      using var response = await _host.Client.PostAsync("/probe", null, TestContext.CurrentContext.CancellationToken);

      Assert.That((_host.Probe.Failure as InfrastructureException)?.Reason, Is.EqualTo(InfrastructureFailureReason.DatabaseUnavailable));
    }
    finally
    {
      SqliteConnection.ClearAllPools();
      File.SetAttributes(_host.DatabasePath, FileAttributes.Normal);
    }
  }

  [Test]
  public async Task Get_ARequestThatOnlyReads_OpensNoTransaction()
  {
    using var response = await _host.Client.GetAsync("/probe", TestContext.CurrentContext.CancellationToken);

    Assert.That(await response.Content.ReadAsStringAsync(TestContext.CurrentContext.CancellationToken), Is.EqualTo(nameof(AutoTransactionBehavior.WhenNeeded)));
  }

  private async Task<int> StoredCategoriesAsync()
  {
    await using var reader = _host.CreateContext();

    return await reader.CatalogCategories.AsNoTracking().CountAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<Guid> WriteCategoryAsync(GastronomyAppDbContext database, string name, CancellationToken cancellationToken)
  {
    CatalogCategory category = new()
    {
      Id = Guid.NewGuid(),
      Name = name,
      ColourHex = "#336699",
      SortOrder = 1,
      IsActive = true
    };

    database.CatalogCategories.Add(category);
    await database.SaveChangesAsync(cancellationToken);

    return category.Id;
  }
}
