using GastronomyApp.Desktop.Enums;

namespace GastronomyApp.Desktop.Ports;

public interface ISingleInstance
{
  public SingleInstanceOutcome AcquireOrSignalExisting();

  public void StartListeningForActivation();

  public event EventHandler? ActivationRequested;

  public void Release();
}
