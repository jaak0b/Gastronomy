using FakeItEasy;
using GastronomyApp.Desktop.Services;

namespace GastronomyApp.Desktop.Tests.Services;

[TestFixture]
public sealed class ElevatedSetupStepsTest
{
  [SetUp]
  public void SetUp()
  {
    _firewall = A.Fake<IFirewallSetup>();
    _dataFolder = A.Fake<IDataFolderSetup>();
  }

  private IFirewallSetup _firewall = null!;
  private IDataFolderSetup _dataFolder = null!;

  private ElevatedSetupSteps CreateSteps()
  {
    return new(_firewall, _dataFolder);
  }

  [Test]
  public void RunAll_WhenNothingFails_ReportsThatEveryStepSucceeded()
  {
    A.CallTo(() => _dataFolder.Exists()).Returns(false);

    var report = CreateSteps().RunAll();

    Assert.That(report.EveryStepSucceeded, Is.True);
    A.CallTo(() => _dataFolder.CreateWithUsersModifyGrant()).MustHaveHappenedOnceExactly();
  }

  [Test]
  public void RunAll_WhenTheFolderIsAlreadyThere_GrantsTheRightsOnTheExistingFolder()
  {
    A.CallTo(() => _dataFolder.Exists()).Returns(true);

    CreateSteps().RunAll();

    A.CallTo(() => _dataFolder.GrantUsersModifyOnExisting()).MustHaveHappenedOnceExactly();
    A.CallTo(() => _dataFolder.CreateWithUsersModifyGrant()).MustNotHaveHappened();
  }

  [Test]
  public void RunAll_WhenTheFirewallStepFails_StillMakesTheDataFolderWritable()
  {
    InvalidOperationException firewallFailure = new("Configuring the inbound firewall rule failed.");
    A.CallTo(() => _firewall.EnsureRuleConfigured()).Throws(firewallFailure);
    A.CallTo(() => _dataFolder.Exists()).Returns(false);

    var report = CreateSteps().RunAll();

    Assert.Multiple(() =>
                    {
                      Assert.That(report.NetworkAccessFailure, Is.SameAs(firewallFailure));
                      Assert.That(report.DataFolderFailure, Is.Null);
                      Assert.That(report.EveryStepSucceeded, Is.False);
                    });
    A.CallTo(() => _dataFolder.CreateWithUsersModifyGrant()).MustHaveHappenedOnceExactly();
  }

  [Test]
  public void RunAll_WhenTheDataFolderStepFails_ReportsItWithoutLosingTheFirewallResult()
  {
    UnauthorizedAccessException dataFolderFailure = new("The folder rights could not be changed.");
    A.CallTo(() => _dataFolder.Exists()).Returns(true);
    A.CallTo(() => _dataFolder.GrantUsersModifyOnExisting()).Throws(dataFolderFailure);

    var report = CreateSteps().RunAll();

    Assert.Multiple(() =>
                    {
                      Assert.That(report.NetworkAccessFailure, Is.Null);
                      Assert.That(report.DataFolderFailure, Is.SameAs(dataFolderFailure));
                      Assert.That(report.EveryStepSucceeded, Is.False);
                    });
    A.CallTo(() => _firewall.EnsureRuleConfigured()).MustHaveHappenedOnceExactly();
  }

  [Test]
  public void RunAll_WhenBothStepsFail_ReportsBothReasons()
  {
    InvalidOperationException firewallFailure = new("Configuring the inbound firewall rule failed.");
    UnauthorizedAccessException dataFolderFailure = new("The folder rights could not be changed.");
    A.CallTo(() => _firewall.EnsureRuleConfigured()).Throws(firewallFailure);
    A.CallTo(() => _dataFolder.Exists()).Returns(false);
    A.CallTo(() => _dataFolder.CreateWithUsersModifyGrant()).Throws(dataFolderFailure);

    var report = CreateSteps().RunAll();

    Assert.Multiple(() =>
                    {
                      Assert.That(report.NetworkAccessFailure, Is.SameAs(firewallFailure));
                      Assert.That(report.DataFolderFailure, Is.SameAs(dataFolderFailure));
                    });
  }
}
