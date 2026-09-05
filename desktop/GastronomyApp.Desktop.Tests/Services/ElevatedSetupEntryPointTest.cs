using FakeItEasy;
using GastronomyApp.Desktop.Services;
using Serilog;

namespace GastronomyApp.Desktop.Tests.Services;

[TestFixture]
public sealed class ElevatedSetupEntryPointTest
{
  private const int FinishedExitCode = 0;
  private const int StepFailedExitCode = 2;

  [SetUp]
  public void SetUp()
  {
    _dataDirectory = Path.Combine(Path.GetTempPath(), "GastronomyAppSetup" + Guid.NewGuid().ToString("N"));
    _firewall = A.Fake<IFirewallSetup>();
    _dataFolder = A.Fake<IDataFolderSetup>();
  }

  [TearDown]
  public void TearDown()
  {
    Log.CloseAndFlush();

    if (Directory.Exists(_dataDirectory))
    {
      Directory.Delete(_dataDirectory, true);
    }

    if (File.Exists(_dataDirectory))
    {
      File.Delete(_dataDirectory);
    }
  }

  private string _dataDirectory = null!;
  private IFirewallSetup _firewall = null!;
  private IDataFolderSetup _dataFolder = null!;

  private int Run()
  {
    ElevatedSetupSteps steps = new(_firewall, _dataFolder);

    return new ElevatedSetupEntryPoint(new ApplicationLog(), _dataDirectory, steps).Run();
  }

  private string ReadWhatWasLogged()
  {
    Log.CloseAndFlush();

    var folder = Path.Combine(_dataDirectory, "logs");

    return string.Concat(Directory.EnumerateFiles(folder).Select(File.ReadAllText));
  }

  [Test]
  public void Run_WhenBothStepsSucceed_ReportsSuccessToTheParentProcess()
  {
    A.CallTo(() => _dataFolder.Exists()).Returns(false);

    Assert.That(Run(), Is.EqualTo(FinishedExitCode));
  }

  [Test]
  public void Run_WhenTheFirewallStepFails_ReportsTheFailureAndLogsWhatWindowsSaid()
  {
    A.CallTo(() => _firewall.EnsureRuleConfigured())
     .Throws(new InvalidOperationException("Configuring the inbound firewall rule failed with exit code 5: "
                                           + "The requested operation requires elevation."));
    A.CallTo(() => _dataFolder.Exists()).Returns(false);

    var exitCode = Run();

    Assert.Multiple(() =>
                    {
                      Assert.That(exitCode, Is.EqualTo(StepFailedExitCode));
                      Assert.That(ReadWhatWasLogged(), Does.Contain("The requested operation requires elevation."));
                    });
  }

  [Test]
  public void Run_WhenOnlyTheDataFolderStepFails_ReportsTheFailureAndNamesTheFolder()
  {
    A.CallTo(() => _dataFolder.Exists()).Returns(true);
    A.CallTo(() => _dataFolder.GrantUsersModifyOnExisting())
     .Throws(new UnauthorizedAccessException("The folder rights could not be changed."));

    var exitCode = Run();
    var logged = ReadWhatWasLogged();

    Assert.Multiple(() =>
                    {
                      Assert.That(exitCode, Is.EqualTo(StepFailedExitCode));
                      Assert.That(logged, Does.Contain("The folder rights could not be changed."));
                      Assert.That(logged, Does.Contain(_dataDirectory));
                    });
  }

  [Test]
  public void Run_WhenTheSetupCannotEvenBePrepared_ReportsTheFailureInsteadOfCrashing()
  {
    File.WriteAllText(_dataDirectory, string.Empty);

    Assert.That(Run(), Is.EqualTo(StepFailedExitCode));
  }
}
