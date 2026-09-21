using FakeItEasy;
using GastronomyApp.Api.Hub;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Tests.Hub;

[TestFixture]
public sealed class AnnouncementGuardTest
{
  [SetUp]
  public void SetUp()
  {
    _logger = A.Fake<ILogger<AnnouncementGuard>>();
    _programIsQuitting = new();

    var lifetime = A.Fake<IHostApplicationLifetime>();
    A.CallTo(() => lifetime.ApplicationStopping).Returns(_programIsQuitting.Token);

    _guard = new(lifetime, _logger);
  }

  [TearDown]
  public void TearDown()
  {
    _programIsQuitting.Dispose();
  }

  private AnnouncementGuard _guard = null!;
  private ILogger<AnnouncementGuard> _logger = null!;
  private CancellationTokenSource _programIsQuitting = null!;

  [Test]
  public void TellTheDevicesWithoutFailingTheSavedChangeAsync_NullCallback_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _guard.TellTheDevicesWithoutFailingTheSavedChangeAsync(null!, CancellationToken.None), Throws.ArgumentNullException);
  }

  [Test]
  public async Task TellTheDevicesWithoutFailingTheSavedChangeAsync_TheDevicesWereTold_SaysSo()
  {
    var wereTold = await _guard.TellTheDevicesWithoutFailingTheSavedChangeAsync(_ => Task.CompletedTask, CancellationToken.None);

    Assert.That(wereTold, Is.True);
  }

  [Test]
  public async Task TellTheDevicesWithoutFailingTheSavedChangeAsync_TheDevicesCannotBeTold_SaysTheyWereNotTold()
  {
    var wereTold = await _guard.TellTheDevicesWithoutFailingTheSavedChangeAsync(_ => throw new InvalidOperationException("The connection to the station tablets broke."), CancellationToken.None);

    Assert.That(wereTold, Is.False);
  }

  [Test]
  public void TellTheDevicesWithoutFailingTheSavedChangeAsync_TheDevicesCannotBeTold_DoesNotFailTheSavedChange()
  {
    Assert.That(async () => await _guard.TellTheDevicesWithoutFailingTheSavedChangeAsync(_ => throw new InvalidOperationException("The connection to the station tablets broke."), CancellationToken.None), Throws.Nothing);
  }

  [Test]
  public async Task TellTheDevicesWithoutFailingTheSavedChangeAsync_TheDevicesCannotBeTold_WritesTheReasonToTheLogAsAnError()
  {
    await _guard.TellTheDevicesWithoutFailingTheSavedChangeAsync(_ => throw new InvalidOperationException("The connection to the station tablets broke."), CancellationToken.None);

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
    await _guard.TellTheDevicesWithoutFailingTheSavedChangeAsync(async cancellationToken =>
                                                                 {
                                                                   await _programIsQuitting.CancelAsync();
                                                                   throw new OperationCanceledException(cancellationToken);
                                                                 },
                                                                 CancellationToken.None);
  }

  private string Rendered(object? state)
  {
    return state?.ToString() ?? string.Empty;
  }
}
