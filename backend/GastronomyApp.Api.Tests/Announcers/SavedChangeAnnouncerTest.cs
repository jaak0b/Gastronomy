using FakeItEasy;
using GastronomyApp.Api.Announcers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Tests.Announcers;

[TestFixture]
public sealed class SavedChangeAnnouncerTest
{
  [SetUp]
  public void SetUp()
  {
    _logger = A.Fake<ILogger<SavedChangeAnnouncer>>();
    _programIsQuitting = new();

    var lifetime = A.Fake<IHostApplicationLifetime>();
    A.CallTo(() => lifetime.ApplicationStopping).Returns(_programIsQuitting.Token);

    _announcement = new(lifetime, _logger);
  }

  [TearDown]
  public void TearDown()
  {
    _programIsQuitting.Dispose();
  }

  private SavedChangeAnnouncer _announcement = null!;
  private ILogger<SavedChangeAnnouncer> _logger = null!;
  private CancellationTokenSource _programIsQuitting = null!;

  [Test]
  public void TellTheDevicesWithoutFailingTheSavedChangeAsync_NullCallback_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _announcement.TellTheDevicesWithoutFailingTheSavedChangeAsync(null!), Throws.ArgumentNullException);
  }

  [Test]
  public async Task TellTheDevicesWithoutFailingTheSavedChangeAsync_TheAdminsBrowserGoesAway_TellsThemWithATokenOfItsOwn()
  {
    using CancellationTokenSource adminRequest = new();
    var tokenUsedForThePush = adminRequest.Token;

    await _announcement.TellTheDevicesWithoutFailingTheSavedChangeAsync(async cancellationToken =>
                                                                        {
                                                                          await adminRequest.CancelAsync();
                                                                          tokenUsedForThePush = cancellationToken;
                                                                        });

    Assert.That(tokenUsedForThePush.IsCancellationRequested, Is.False);
  }

  [Test]
  public void TellTheDevicesWithoutFailingTheSavedChangeAsync_TheDevicesCannotBeTold_DoesNotFailTheSavedChange()
  {
    Assert.That(async () => await _announcement.TellTheDevicesWithoutFailingTheSavedChangeAsync(_ => throw new InvalidOperationException("The connection to the station tablets broke.")), Throws.Nothing);
  }

  [Test]
  public async Task TellTheDevicesWithoutFailingTheSavedChangeAsync_TheDevicesCannotBeTold_WritesTheReasonToTheLogAsAnError()
  {
    await _announcement.TellTheDevicesWithoutFailingTheSavedChangeAsync(_ => throw new InvalidOperationException("The connection to the station tablets broke."));

    A.CallTo(_logger).Where(call => call.Method.Name == nameof(ILogger.Log) && call.GetArgument<LogLevel>(0) == LogLevel.Error && call.GetArgument<Exception?>(3) != null && Rendered(call.GetArgument<object>(2)).Contains("station tablets", StringComparison.Ordinal)).MustHaveHappened();
  }

  [Test]
  public async Task TellTheDevicesWithoutFailingTheSavedChangeAsync_TheProgramQuitsWhileTheyAreBeingTold_LeavesNoErrorInTheLog()
  {
    await QuitTheProgramWhileTheDevicesAreBeingToldAsync();

    A.CallTo(_logger).Where(call => call.Method.Name == nameof(ILogger.Log) && call.GetArgument<LogLevel>(0) == LogLevel.Error).MustNotHaveHappened();
  }

  [Test]
  public async Task TellTheDevicesWithoutFailingTheSavedChangeAsync_TheProgramQuitsWhileTheyAreBeingTold_WritesTheReasonAsInformation()
  {
    await QuitTheProgramWhileTheDevicesAreBeingToldAsync();

    A.CallTo(_logger).Where(call => call.Method.Name == nameof(ILogger.Log) && call.GetArgument<LogLevel>(0) == LogLevel.Information && Rendered(call.GetArgument<object>(2)).Contains("was quitting", StringComparison.Ordinal)).MustHaveHappenedOnceExactly();
  }

  private async Task QuitTheProgramWhileTheDevicesAreBeingToldAsync()
  {
    await _announcement.TellTheDevicesWithoutFailingTheSavedChangeAsync(async cancellationToken =>
                                                                        {
                                                                          await _programIsQuitting.CancelAsync();
                                                                          cancellationToken.ThrowIfCancellationRequested();
                                                                        });
  }

  private string Rendered(object? state)
  {
    return state?.ToString() ?? string.Empty;
  }
}
