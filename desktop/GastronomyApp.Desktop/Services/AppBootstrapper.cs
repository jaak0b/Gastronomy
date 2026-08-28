using GastronomyApp.Core.Services;
using GastronomyApp.Desktop.ViewModels;

namespace GastronomyApp.Desktop.Services;

public enum BootstrapOutcome
{
  ProceedToWindow,
  ExitImmediately
}

public sealed class AppBootstrapper
{
  private readonly Action bringExistingWindowToFront;
  private readonly Action<Action> dispatchToUserInterface;
  private readonly Func<MainWindowViewModel> mainWindowViewModelFactory;
  private readonly Never never = new();
  private readonly ISingleInstance singleInstance;

  private bool holdsTheInstance;

  public AppBootstrapper(ISingleInstance singleInstance,
                         Func<MainWindowViewModel> mainWindowViewModelFactory,
                         Action bringExistingWindowToFront,
                         Action<Action> dispatchToUserInterface)
  {
    this.singleInstance = singleInstance;
    this.mainWindowViewModelFactory = mainWindowViewModelFactory;
    this.bringExistingWindowToFront = bringExistingWindowToFront;
    this.dispatchToUserInterface = dispatchToUserInterface;
    this.singleInstance.ActivationRequested += OnActivationRequested;
  }

  public MainWindowViewModel? MainWindowViewModel { get; private set; }

  public BootstrapOutcome Start()
  {
    SingleInstanceOutcome outcome;

    try
    {
      outcome = singleInstance.AcquireOrSignalExisting();
    }
    catch (Exception failure) when (failure is IOException or TimeoutException or UnauthorizedAccessException)
    {
      return BootstrapOutcome.ExitImmediately;
    }

    switch (outcome)
    {
      case SingleInstanceOutcome.AcquiredPrimary:
        holdsTheInstance = true;
        MainWindowViewModel = mainWindowViewModelFactory();

        return BootstrapOutcome.ProceedToWindow;

      case SingleInstanceOutcome.SignaledExistingAndShouldExit:
        return BootstrapOutcome.ExitImmediately;

      default:
        return never.OfType<BootstrapOutcome>(outcome);
    }
  }

  public void Release()
  {
    singleInstance.ActivationRequested -= OnActivationRequested;
    singleInstance.Release();
  }

  private void OnActivationRequested()
  {
    if (!holdsTheInstance)
    {
      return;
    }

    dispatchToUserInterface(bringExistingWindowToFront);
  }
}
