using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace GastronomyApp.Api.Tests.Logging;

public sealed class RecordedLog : IDisposable
{
  private readonly Logger _logger;
  private readonly ILogger _previousLogger;
  private readonly Recorder _recorder = new();

  public RecordedLog()
  {
    _previousLogger = Log.Logger;
    _logger = new LoggerConfiguration().MinimumLevel.Verbose()
                                       .WriteTo.Sink(_recorder)
                                       .CreateLogger();
    Log.Logger = _logger;
  }

  public IReadOnlyList<LogEvent> Entries => _recorder.Snapshot();

  public IReadOnlyList<string> RenderedMessages => [.. Entries.Select(entry => entry.RenderMessage())];

  public void Dispose()
  {
    Log.Logger = _previousLogger;
    _logger.Dispose();
  }

  private sealed class Recorder : ILogEventSink
  {
    private readonly List<LogEvent> _entries = [];

    public void Emit(LogEvent logEvent)
    {
      lock (_entries)
      {
        _entries.Add(logEvent);
      }
    }

    public IReadOnlyList<LogEvent> Snapshot()
    {
      lock (_entries)
      {
        return [.. _entries];
      }
    }
  }
}
