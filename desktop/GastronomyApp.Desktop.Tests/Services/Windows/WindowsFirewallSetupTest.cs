using System.Runtime.Versioning;
using FakeItEasy;
using GastronomyApp.Desktop.Services.Windows;

namespace GastronomyApp.Desktop.Tests.Services.Windows;

[TestFixture]
[SupportedOSPlatform("windows")]
public sealed class WindowsFirewallSetupTest
{
  private const string ExecutablePath = @"C:\Program Files\GastronomyApp\GastronomyApp.Desktop.exe";

  private const string ShowRule = "advfirewall firewall show rule name=\"GastronomyApp ordering system\"";

  private const string ShowRuleForBothProfiles = ShowRule + " profile=private,public";

  private const string RuleSettings =
    "action=allow program=\"" + ExecutablePath + "\" "
    + "protocol=TCP profile=private,public remoteip=localsubnet enable=yes";

  private const string AddRule =
    "advfirewall firewall add rule name=\"GastronomyApp ordering system\" dir=in " + RuleSettings;

  private const string SetRule =
    "advfirewall firewall set rule name=\"GastronomyApp ordering system\" dir=in new " + RuleSettings;

  [SetUp]
  public void SetUp()
  {
    _netsh = A.Fake<INetshCommand>();
    A.CallTo(() => _netsh.Run(A<string>._)).Returns(new NetshResult(0, string.Empty));
  }

  private INetshCommand _netsh = null!;

  private WindowsFirewallSetup CreateSetup()
  {
    return new(ExecutablePath, _netsh);
  }

  private void NetshAnswers(string arguments, int exitCode, string errorOutput = "")
  {
    A.CallTo(() => _netsh.Run(arguments)).Returns(new NetshResult(exitCode, errorOutput));
  }

  [Test]
  public void EnsureRuleConfigured_WithoutAnExistingRule_AddsARuleCoveringPrivateAndPublic()
  {
    NetshAnswers(ShowRule, 1);

    CreateSetup().EnsureRuleConfigured();

    A.CallTo(() => _netsh.Run(AddRule)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public void EnsureRuleConfigured_WithAnExistingRule_UpdatesItToCoverBothProfiles()
  {
    NetshAnswers(ShowRule, 0);

    CreateSetup().EnsureRuleConfigured();

    A.CallTo(() => _netsh.Run(SetRule)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public void EnsureRuleConfigured_WhenTheCommandFails_ThrowsStatingTheExitCode()
  {
    NetshAnswers(ShowRule, 1);
    NetshAnswers(AddRule, 87);

    var failure = Assert.Throws<InvalidOperationException>(() => CreateSetup().EnsureRuleConfigured());

    Assert.That(failure!.Message, Does.Contain("87"));
  }

  [Test]
  public void EnsureRuleConfigured_WhenTheCommandFails_ThrowsCarryingWhatNetshReported()
  {
    NetshAnswers(ShowRule, 1);
    NetshAnswers(AddRule, 5, "The requested operation requires elevation.\r\n");

    var failure = Assert.Throws<InvalidOperationException>(() => CreateSetup().EnsureRuleConfigured());

    Assert.That(failure!.Message, Does.Contain("The requested operation requires elevation."));
  }

  [Test]
  public void IsRuleConfigured_WithARuleCoveringOnlyOneProfile_ReportsTheSetupAsIncomplete()
  {
    NetshAnswers(ShowRule, 0);
    NetshAnswers(ShowRuleForBothProfiles, 1);

    Assert.That(CreateSetup().IsRuleConfigured(), Is.False);
  }

  [Test]
  public void IsRuleConfigured_WithARuleCoveringBothProfiles_ReportsTheSetupAsComplete()
  {
    NetshAnswers(ShowRuleForBothProfiles, 0);

    Assert.That(CreateSetup().IsRuleConfigured(), Is.True);
  }

  [Test]
  public void IsRuleConfigured_WithoutAnyRule_ReportsTheSetupAsIncomplete()
  {
    NetshAnswers(ShowRuleForBothProfiles, 1);

    Assert.That(CreateSetup().IsRuleConfigured(), Is.False);
  }
}
