using FakeItEasy;
using GastronomyApp.Desktop.Services;
using GastronomyApp.Desktop.Tests.Logging;
using Serilog.Events;

namespace GastronomyApp.Desktop.Tests.Services;

[TestFixture]
public sealed class AutomaticUpdateCheckerTests
{
  private const string DataFolder = @"C:\ProgramData\GastronomyApp";

  [SetUp]
  public void SetUp()
  {
    _installer = A.Fake<IUpdateInstaller>();
    _settingsStore = A.Fake<ISettingsStore>();
    _settings = new DesktopSettings(5000, DataFolder, null, null);
    A.CallTo(() => _installer.IsInstalled).Returns(true);
    A.CallTo(() => _installer.CheckAndDownloadAsync(A<CancellationToken>._))
     .Returns(new UpdatePreparation.UpToDate());
    A.CallTo(() => _settingsStore.Load()).ReturnsLazily(() => _settings);
    A.CallTo(() => _settingsStore.Save(A<DesktopSettings>._))
     .Invokes(call => _settings = call.GetArgument<DesktopSettings>(0)!);
  }

  private IUpdateInstaller _installer = null!;
  private ISettingsStore _settingsStore = null!;
  private DesktopSettings _settings = null!;

  private AutomaticUpdateChecker CreateChecker()
  {
    return new(_installer, _settingsStore);
  }

  [Test]
  public async Task CheckOnStartupAsync_WhenTheProgramIsNotInstalled_ChecksNothing()
  {
    A.CallTo(() => _installer.IsInstalled).Returns(false);

    await CreateChecker().CheckOnStartupAsync(CancellationToken.None);

    A.CallTo(() => _installer.CheckAndDownloadAsync(A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _settingsStore.Save(A<DesktopSettings>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task CheckOnStartupAsync_WhenTheLastCheckIsLessThanAnHourOld_ChecksNothing()
  {
    _settings = _settings with { LastUpdateCheckUtc = DateTimeOffset.UtcNow.AddMinutes(-30) };

    await CreateChecker().CheckOnStartupAsync(CancellationToken.None);

    A.CallTo(() => _installer.CheckAndDownloadAsync(A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _settingsStore.Save(A<DesktopSettings>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task CheckOnStartupAsync_WhenTheLastCheckIsOlderThanAnHour_ChecksOnceAndStoresTheMoment()
  {
    var previous = DateTimeOffset.UtcNow.AddHours(-2);
    _settings = _settings with { LastUpdateCheckUtc = previous };

    await CreateChecker().CheckOnStartupAsync(CancellationToken.None);

    A.CallTo(() => _installer.CheckAndDownloadAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    Assert.Multiple(() =>
                    {
                      Assert.That(_settings.LastUpdateCheckUtc, Is.GreaterThan(previous));
                      Assert.That(_settings.LastUpdateCheckUtc, Is.GreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1)));
                    });
  }

  [Test]
  public async Task CheckOnStartupAsync_WhenThePreparationFails_DoesNotTryAgainWithinTheHour()
  {
    _settings = _settings with { LastUpdateCheckUtc = DateTimeOffset.UtcNow.AddHours(-2) };
    A.CallTo(() => _installer.CheckAndDownloadAsync(A<CancellationToken>._))
     .Returns(new UpdatePreparation.Failed(new InvalidOperationException("The network is down.")));
    var checker = CreateChecker();

    await checker.CheckOnStartupAsync(CancellationToken.None);
    await checker.CheckOnStartupAsync(CancellationToken.None);

    A.CallTo(() => _installer.CheckAndDownloadAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task CheckOnStartupAsync_WhenTheCheckThrowsUnexpectedly_StillStoresTheMoment()
  {
    _settings = _settings with { LastUpdateCheckUtc = DateTimeOffset.UtcNow.AddHours(-2) };
    A.CallTo(() => _installer.CheckAndDownloadAsync(A<CancellationToken>._))
     .ThrowsAsync(new InvalidOperationException("The network is down."));

    await CreateChecker().CheckOnStartupAsync(CancellationToken.None);

    Assert.That(_settings.LastUpdateCheckUtc, Is.GreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1)));
  }

  [Test]
  public async Task CheckOnStartupAsync_WhenTheSettingsCannotBeRead_DoesNotThrowAndLogsAnError()
  {
    using RecordedLog log = new();
    InvalidOperationException failure = new("The settings file is unreadable.");
    A.CallTo(() => _settingsStore.Load()).Throws(failure);

    Assert.DoesNotThrowAsync(() => CreateChecker().CheckOnStartupAsync(CancellationToken.None));

    Assert.That(log.Entries.Where(entry => entry.Level == LogEventLevel.Error
                                           && ReferenceEquals(entry.Exception, failure)),
                Is.Not.Empty);
  }

  [Test]
  public async Task CheckOnStartupAsync_WhenTheSettingsCannotBeSaved_DoesNotThrowAndLogsAnError()
  {
    using RecordedLog log = new();
    InvalidOperationException failure = new("The settings file is not writable.");
    A.CallTo(() => _settingsStore.Save(A<DesktopSettings>._)).Throws(failure);

    Assert.DoesNotThrowAsync(() => CreateChecker().CheckOnStartupAsync(CancellationToken.None));

    Assert.That(log.Entries.Where(entry => entry.Level == LogEventLevel.Error
                                           && ReferenceEquals(entry.Exception, failure)),
                Is.Not.Empty);
    A.CallTo(() => _installer.CheckAndDownloadAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }
}
