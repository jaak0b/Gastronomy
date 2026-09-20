using FakeItEasy;
using GastronomyApp.Desktop.Services;

namespace GastronomyApp.Desktop.Tests.Services;

[TestFixture]
public sealed class UpdateOnQuitTests
{
  [SetUp]
  public void SetUp()
  {
    _installer = A.Fake<IUpdateInstaller>();
    _gate = A.Fake<IUpdateInstallGate>();
    A.CallTo(() => _gate.CanInstallNowAsync(A<CancellationToken>._)).Returns(true);
  }

  private IUpdateInstaller _installer = null!;
  private IUpdateInstallGate _gate = null!;

  private UpdateOnQuit CreateUpdateOnQuit()
  {
    return new(_installer, _gate);
  }

  [Test]
  public async Task PrepareAsync_WithNothingDownloaded_NeverAsksTheGateAndInstallsNothing()
  {
    A.CallTo(() => _installer.HasDownloadedUpdate).Returns(false);

    await CreateUpdateOnQuit().PrepareAsync(CancellationToken.None);

    A.CallTo(() => _gate.CanInstallNowAsync(A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _installer.InstallOnQuit(A<bool>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task PrepareAsync_WhenTheGateAllows_InstallsOnQuitWithoutRestart()
  {
    A.CallTo(() => _installer.HasDownloadedUpdate).Returns(true);

    await CreateUpdateOnQuit().PrepareAsync(CancellationToken.None);

    A.CallTo(() => _installer.InstallOnQuit(false)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task PrepareAsync_WhenTheGateRefuses_InstallsNothing()
  {
    A.CallTo(() => _installer.HasDownloadedUpdate).Returns(true);
    A.CallTo(() => _gate.CanInstallNowAsync(A<CancellationToken>._)).Returns(false);

    await CreateUpdateOnQuit().PrepareAsync(CancellationToken.None);

    A.CallTo(() => _installer.InstallOnQuit(A<bool>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task PrepareAsync_AfterAnInstallWasRequested_BypassesAGateThatRefuses()
  {
    A.CallTo(() => _installer.HasDownloadedUpdate).Returns(true);
    A.CallTo(() => _gate.CanInstallNowAsync(A<CancellationToken>._)).Returns(false);
    var updateOnQuit = CreateUpdateOnQuit();
    updateOnQuit.RequestInstallDespiteFestival();

    await updateOnQuit.PrepareAsync(CancellationToken.None);

    A.CallTo(() => _installer.InstallOnQuit(false)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task PrepareAsync_WithoutARequestAndWithTheGateRefusing_AsksTheGateAndInstallsNothing()
  {
    A.CallTo(() => _installer.HasDownloadedUpdate).Returns(true);
    A.CallTo(() => _gate.CanInstallNowAsync(A<CancellationToken>._)).Returns(false);

    await CreateUpdateOnQuit().PrepareAsync(CancellationToken.None);

    A.CallTo(() => _gate.CanInstallNowAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _installer.InstallOnQuit(A<bool>._)).MustNotHaveHappened();
  }
}
