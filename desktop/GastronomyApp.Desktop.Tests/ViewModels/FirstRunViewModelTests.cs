using FakeItEasy;
using GastronomyApp.Desktop.Localization;
using GastronomyApp.Desktop.Services;
using GastronomyApp.Desktop.ViewModels;

namespace GastronomyApp.Desktop.Tests.ViewModels;

[TestFixture]
public sealed class FirstRunViewModelTests
{
  private IFirewallSetup _firewall = null!;
  private IDataFolderSetup _dataFolder = null!;
  private IElevatedSetupLauncher _elevatedSetup = null!;
  private IDesktopTextProvider _text = null!;

  [SetUp]
  public void SetUp()
  {
    _firewall = A.Fake<IFirewallSetup>();
    _dataFolder = A.Fake<IDataFolderSetup>();
    _elevatedSetup = A.Fake<IElevatedSetupLauncher>();
    _text = new DesktopTextProvider();
  }

  private FirstRunViewModel CreateViewModel(bool firewallConfigured, bool dataFolderReady)
  {
    A.CallTo(() => _firewall.IsRuleConfigured()).Returns(firewallConfigured);
    A.CallTo(() => _dataFolder.Exists()).Returns(dataFolderReady);
    A.CallTo(() => _dataFolder.CurrentUserCanWrite()).Returns(dataFolderReady);

    return new FirstRunViewModel(_firewall, _dataFolder, _elevatedSetup, _text);
  }

  [Test]
  public void Evaluate_WhenBothTheRuleAndTheFolderAreInPlace_ShowsNothingAndStartsNormally()
  {
    FirstRunViewModel viewModel = CreateViewModel(firewallConfigured: true, dataFolderReady: true);

    viewModel.Evaluate();

    Assert.Multiple(() =>
    {
      Assert.That(viewModel.IsSetupOffered, Is.False);
      Assert.That(viewModel.ReadyToStart, Is.True);
      Assert.That(viewModel.DeclinedText, Is.Null);
    });
  }

  [Test]
  public void Evaluate_WhenOnlyTheFirewallRuleIsMissing_OffersTheOneTimeSetup()
  {
    FirstRunViewModel viewModel = CreateViewModel(firewallConfigured: false, dataFolderReady: true);

    viewModel.Evaluate();

    Assert.Multiple(() =>
    {
      Assert.That(viewModel.IsSetupOffered, Is.True);
      Assert.That(viewModel.Title, Is.EqualTo(_text.Get("desktop.firstRun.title")));
      Assert.That(viewModel.Body, Is.EqualTo(_text.Get("desktop.firstRun.body")));
    });
  }

  [Test]
  public void Evaluate_WhenOnlyTheDataFolderIsMissing_OffersTheOneTimeSetup()
  {
    FirstRunViewModel viewModel = CreateViewModel(firewallConfigured: true, dataFolderReady: false);

    viewModel.Evaluate();

    Assert.Multiple(() =>
    {
      Assert.That(viewModel.IsSetupOffered, Is.True);
      Assert.That(viewModel.Title, Is.EqualTo(_text.Get("desktop.firstRun.title")));
      Assert.That(viewModel.Body, Is.EqualTo(_text.Get("desktop.firstRun.body")));
    });
  }

  [Test]
  public void Evaluate_WhenTheFolderExistsButCannotBeWrittenTo_OffersTheOneTimeSetup()
  {
    A.CallTo(() => _firewall.IsRuleConfigured()).Returns(true);
    A.CallTo(() => _dataFolder.Exists()).Returns(true);
    A.CallTo(() => _dataFolder.CurrentUserCanWrite()).Returns(false);
    FirstRunViewModel viewModel = new(_firewall, _dataFolder, _elevatedSetup, _text);

    viewModel.Evaluate();

    Assert.That(viewModel.IsSetupOffered, Is.True);
  }

  [Test]
  public void Evaluate_WhenBothAreMissing_OffersTheOneTimeSetup()
  {
    FirstRunViewModel viewModel = CreateViewModel(firewallConfigured: false, dataFolderReady: false);

    viewModel.Evaluate();

    Assert.Multiple(() =>
    {
      Assert.That(viewModel.IsSetupOffered, Is.True);
      Assert.That(viewModel.ReadyToStart, Is.False);
    });
  }

  [Test]
  public void Decline_WhenTheOfferIsDismissed_SaysWhatStillWorksAndRunsNoSetup()
  {
    FirstRunViewModel viewModel = CreateViewModel(firewallConfigured: false, dataFolderReady: false);
    viewModel.Evaluate();

    viewModel.Decline();

    Assert.Multiple(() =>
    {
      Assert.That(viewModel.DeclinedText, Is.EqualTo(_text.Get("desktop.firstRun.declined")));
      Assert.That(viewModel.ReadyToStart, Is.True);
      Assert.That(viewModel.IsSetupOffered, Is.False);
    });
    A.CallTo(() => _elevatedSetup.RunElevatedSetupAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task RunSetupAsync_WhenTheElevationIsAccepted_ClosesTheOfferAndStarts()
  {
    A.CallTo(() => _elevatedSetup.RunElevatedSetupAsync(A<CancellationToken>._))
        .Returns(ElevatedSetupOutcome.Completed);
    FirstRunViewModel viewModel = CreateViewModel(firewallConfigured: false, dataFolderReady: false);
    viewModel.Evaluate();

    await viewModel.RunSetupAsync();

    Assert.Multiple(() =>
    {
      Assert.That(viewModel.IsSetupOffered, Is.False);
      Assert.That(viewModel.ReadyToStart, Is.True);
      Assert.That(viewModel.DeclinedText, Is.Null);
    });
  }

  [Test]
  public async Task RunSetupAsync_WhenTheElevationIsDeclined_SaysWhatStillWorksAndStartsAnyway()
  {
    A.CallTo(() => _elevatedSetup.RunElevatedSetupAsync(A<CancellationToken>._))
        .Returns(ElevatedSetupOutcome.ElevationDeclined);
    FirstRunViewModel viewModel = CreateViewModel(firewallConfigured: false, dataFolderReady: false);
    viewModel.Evaluate();

    await viewModel.RunSetupAsync();

    Assert.Multiple(() =>
    {
      Assert.That(viewModel.DeclinedText, Is.EqualTo(_text.Get("desktop.firstRun.declined")));
      Assert.That(viewModel.ReadyToStart, Is.True);
      Assert.That(viewModel.IsSetupOffered, Is.False);
    });
  }
}
