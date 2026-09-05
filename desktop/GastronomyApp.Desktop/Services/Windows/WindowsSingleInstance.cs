using System.IO.Pipes;
using Serilog;

namespace GastronomyApp.Desktop.Services.Windows;

public sealed class ActivationPipeListener
{
  private const string ActivationSignal = "activate";
  private readonly string _pipeName;

  public ActivationPipeListener(string pipeName)
  {
    _pipeName = pipeName;
  }

  public event Action? ActivationRequested;

  public async Task ListenAsync(CancellationToken cancellationToken)
  {
    try
    {
      await ReceiveActivationSignalsAsync(cancellationToken);
    }
    catch (Exception failure)
    {
      ReportStopped(failure);

      return;
    }

    ReportStopped(null);
  }

  private void ReportStopped(Exception? failure)
  {
    if (failure is null or OperationCanceledException or ObjectDisposedException)
    {
      Log.Information("The listener for a second start stopped because the program is closing.");

      return;
    }

    Log.Error(failure,
              "The listener for a second start stopped. Starting the program again will no longer "
              + "bring the open window to the front.");
  }

  private async Task ReceiveActivationSignalsAsync(CancellationToken cancellationToken)
  {
    while (!cancellationToken.IsCancellationRequested)
    {
      using NamedPipeServerStream server = new(_pipeName,
                                               PipeDirection.In,
                                               NamedPipeServerStream.MaxAllowedServerInstances);

      await server.WaitForConnectionAsync(cancellationToken);

      using StreamReader reader = new(server);
      var signal = await reader.ReadLineAsync(cancellationToken);

      if (signal == ActivationSignal)
      {
        ActivationRequested?.Invoke();
      }
    }
  }
}

public sealed class SingleInstanceCoordinator : ISingleInstance, IDisposable
{
  private const string MutexName = @"Global\GastronomyApp.Desktop.SingleInstance";
  private const string PipeName = "GastronomyApp.Desktop.Activation";
  private const string ActivationSignal = "activate";
  private const int ConnectAttempts = 5;
  private const int ConnectAttemptMilliseconds = 400;
  private readonly ActivationPipeListener _listener = new(PipeName);
  private CancellationTokenSource? _listening;

  private Mutex? _mutex;

  public void Dispose()
  {
    Release();
  }

  public event Action? ActivationRequested
  {
    add => _listener.ActivationRequested += value;
    remove => _listener.ActivationRequested -= value;
  }

  public SingleInstanceOutcome AcquireOrSignalExisting()
  {
    _mutex = new(true, MutexName, out var acquired);

    if (!acquired)
    {
      _mutex.Dispose();
      _mutex = null;
      SignalExisting();

      return SingleInstanceOutcome.SignaledExistingAndShouldExit;
    }

    _listening = new();
    _ = _listener.ListenAsync(_listening.Token);

    return SingleInstanceOutcome.AcquiredPrimary;
  }

  public void Release()
  {
    _listening?.Cancel();
    _listening?.Dispose();
    _listening = null;

    _mutex?.ReleaseMutex();
    _mutex?.Dispose();
    _mutex = null;
  }

  private void SignalExisting()
  {
    for (var attempt = 0; attempt < ConnectAttempts; attempt++)
    {
      if (TrySignalExisting())
      {
        return;
      }
    }
  }

  private bool TrySignalExisting()
  {
    try
    {
      using NamedPipeClientStream client = new(".", PipeName, PipeDirection.Out);
      client.Connect(TimeSpan.FromMilliseconds(ConnectAttemptMilliseconds));

      using StreamWriter writer = new(client);
      writer.WriteLine(ActivationSignal);
      writer.Flush();

      return true;
    }
    catch (Exception failure) when (failure is TimeoutException or IOException or UnauthorizedAccessException)
    {
      return false;
    }
  }
}
