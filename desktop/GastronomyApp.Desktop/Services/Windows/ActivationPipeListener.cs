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

  public event EventHandler? ActivationRequested;

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

  private void OnActivationRequested()
  {
    ActivationRequested?.Invoke(this, EventArgs.Empty);
  }

  private void ReportStopped(Exception? failure)
  {
    if (failure is null or OperationCanceledException or ObjectDisposedException)
    {
      Log.Information("The listener for a second start stopped because the program is closing.");

      return;
    }

    Log.Error(failure, "The listener for a second start stopped. Starting the program again will no longer " + "bring the open window to the front.");
  }

  private async Task ReceiveActivationSignalsAsync(CancellationToken cancellationToken)
  {
    while (!cancellationToken.IsCancellationRequested)
    {
      using NamedPipeServerStream server = new(_pipeName, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances);

      await server.WaitForConnectionAsync(cancellationToken);

      using StreamReader reader = new(server);
      var signal = await reader.ReadLineAsync(cancellationToken);

      if (signal == ActivationSignal)
        OnActivationRequested();
    }
  }
}
