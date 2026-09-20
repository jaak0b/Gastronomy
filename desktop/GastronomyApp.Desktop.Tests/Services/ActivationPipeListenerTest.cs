using GastronomyApp.Desktop.Services.Windows;
using GastronomyApp.Desktop.Tests.Logging;
using Serilog.Events;

namespace GastronomyApp.Desktop.Tests.Services;

[TestFixture]
public sealed class ActivationPipeListenerTests
{

  private static string UnusedPipeName()
  {
    return $"GastronomyApp.Desktop.Tests.Activation.{Guid.NewGuid():N}";
  }

  [Test]
  public async Task ListenAsync_WhenTheProgramIsClosing_EndsWithoutReportingAFailure()
  {
    using RecordedLog log = new();
    ActivationPipeListener listener = new(UnusedPipeName());
    using CancellationTokenSource closing = new();

    var listening = listener.ListenAsync(closing.Token);
    await closing.CancelAsync();
    await listening;

    Assert.That(log.Entries.Where(entry => entry.Level >= LogEventLevel.Warning),
                Is.Empty,
                "The expected end of the listener must not be reported as a failure.");
  }

  [Test]
  public async Task ListenAsync_WhenTheProgramIsClosing_SaysInTheLogThatTheListenerStopped()
  {
    using RecordedLog log = new();
    ActivationPipeListener listener = new(UnusedPipeName());
    using CancellationTokenSource closing = new();

    var listening = listener.ListenAsync(closing.Token);
    await closing.CancelAsync();
    await listening;

    Assert.That(log.Entries.Select(entry => entry.RenderMessage()),
                Has.Some.Contains("listener"));
  }

  [Test]
  public async Task ListenAsync_WhenTheListenerCannotRun_ReportsTheFailureInsteadOfEndingInSilence()
  {
    using RecordedLog log = new();
    ActivationPipeListener listener = new(string.Empty);
    using CancellationTokenSource closing = new();

    await listener.ListenAsync(closing.Token);

    Assert.That(log.Entries.Where(entry => entry.Level == LogEventLevel.Error
                                           && entry.Exception is not null),
                Is.Not.Empty);
  }
}
