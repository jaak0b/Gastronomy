namespace GastronomyApp.Desktop.Platform;

public interface ISingleInstance
{
  public SingleInstanceOutcome AcquireOrSignalExisting();

  public void StartListeningForActivation();

  public event EventHandler? ActivationRequested;

  public void Release();
}
