namespace GastronomyApp.Desktop.Services;

public interface ISingleInstance
{
  public SingleInstanceOutcome AcquireOrSignalExisting();

  public void StartListeningForActivation();

  public event EventHandler? ActivationRequested;

  public void Release();
}
