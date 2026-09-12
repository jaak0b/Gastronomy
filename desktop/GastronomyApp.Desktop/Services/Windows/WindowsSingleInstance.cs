using System.IO.Pipes;
using System.Runtime.InteropServices;
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
  private const string ProductMutexName = @"Global\GastronomyApp.Desktop.SingleInstance";
  private const string ProductPipeName = "GastronomyApp.Desktop.Activation";
  private const string ActivationSignal = "activate";
  private const int ConnectAttempts = 5;
  private const int ConnectAttemptMilliseconds = 400;
  private const uint AnyProcess = 0xFFFFFFFF;
  private readonly ActivationPipeListener _listener;
  private readonly string _mutexName;
  private readonly string _pipeName;
  private CancellationTokenSource? _listening;

  private Mutex? _mutex;

  public SingleInstanceCoordinator() : this(ProductMutexName, ProductPipeName)
  {
  }

  public SingleInstanceCoordinator(string mutexName, string pipeName)
  {
    _mutexName = mutexName;
    _pipeName = pipeName;
    _listener = new(pipeName);
  }

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
    _mutex = new(true, _mutexName, out var acquired);

    if (!acquired)
    {
      _mutex.Dispose();
      _mutex = null;
      SignalExisting();

      return SingleInstanceOutcome.SignaledExistingAndShouldExit;
    }

    return SingleInstanceOutcome.AcquiredPrimary;
  }

  public void StartListeningForActivation()
  {
    _listening = new();
    _ = _listener.ListenAsync(_listening.Token);
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
    Exception? lastFailure = null;

    for (var attempt = 0; attempt < ConnectAttempts; attempt++)
    {
      lastFailure = TrySignalExisting();

      if (lastFailure is null)
      {
        return;
      }
    }

    Log.Error(lastFailure,
              "The program is already running, but it did not answer, so its window was not brought "
              + "to the front. This second start is closing again and the operator sees nothing "
              + "happen.");
  }

  private Exception? TrySignalExisting()
  {
    try
    {
      using NamedPipeClientStream client = new(".", _pipeName, PipeDirection.Out);
      client.Connect(TimeSpan.FromMilliseconds(ConnectAttemptMilliseconds));

      AllowTheRunningInstanceToComeToFront();

      using StreamWriter writer = new(client);
      writer.WriteLine(ActivationSignal);
      writer.Flush();

      return null;
    }
    catch (Exception failure) when (failure is TimeoutException or IOException or UnauthorizedAccessException)
    {
      return failure;
    }
  }

  private static void AllowTheRunningInstanceToComeToFront()
  {
    if (OperatingSystem.IsWindows() && !AllowSetForegroundWindow(AnyProcess))
    {
      Log.Warning("Windows did not grant this start the right to raise the running window, so "
                  + "starting the program again may only flash its task bar button (error {ErrorCode}).",
                  Marshal.GetLastWin32Error());
    }
  }

  [DllImport("user32.dll", SetLastError = true)]
  private static extern bool AllowSetForegroundWindow(uint processId);
}
