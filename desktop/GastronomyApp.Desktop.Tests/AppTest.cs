using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using FakeItEasy;

namespace GastronomyApp.Desktop.Tests;

[TestFixture]
public sealed class AppExitTests
{
  [AvaloniaTest]
  public void ExitOnceTheDispatcherRuns_HoldsTheShutdownUntilTheMainLoopProcessesIt()
  {
    var lifetime = A.Fake<IClassicDesktopStyleApplicationLifetime>();
    var app = (App)Application.Current!;

    app.ExitOnceTheDispatcherRuns(lifetime);

    A.CallTo(() => lifetime.Shutdown(A<int>._)).MustNotHaveHappened();
  }

  [AvaloniaTest]
  public void ExitOnceTheDispatcherRuns_WhenTheMainLoopProcessesIt_ShutsTheLifetimeDown()
  {
    var lifetime = A.Fake<IClassicDesktopStyleApplicationLifetime>();
    var app = (App)Application.Current!;

    app.ExitOnceTheDispatcherRuns(lifetime);
    Dispatcher.UIThread.RunJobs();

    A.CallTo(() => lifetime.Shutdown(A<int>._)).MustHaveHappenedOnceExactly();
  }
}
