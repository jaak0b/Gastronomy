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
  private readonly Action _bringExistingWindowToFront;
  private readonly Action<Action> _dispatchToUserInterface;
  private readonly Func<MainWindowViewModel> _mainWindowViewModelFactory;
  private readonly Never _never = new();
  private readonly ISingleInstance _singleInstance;

  private bool _holdsTheInstance;

  public AppBootstrapper(ISingleInstance singleInstance,
                         Func<MainWindowViewModel> mainWindowViewModelFactory,
                         Action bringExistingWindowToFront,
                         Action<Action> dispatchToUserInterface)
  {
    _singleInstance = singleInstance;
    _mainWindowViewModelFactory = mainWindowViewModelFactory;
    _bringExistingWindowToFront = bringExistingWindowToFront;
    _dispatchToUserInterface = dispatchToUserInterface;
    _singleInstance.ActivationRequested += OnActivationRequested;
  }

  public MainWindowViewModel? MainWindowViewModel { get; private set; }

  public BootstrapOutcome Start()
  {
    SingleInstanceOutcome outcome;

    try
    {
      outcome = _singleInstance.AcquireOrSignalExisting();
    }
    catch (Exception failure) when (failure is IOException or TimeoutException or UnauthorizedAccessException)
    {
      return BootstrapOutcome.ExitImmediately;
    }

    switch (outcome)
    {
      case SingleInstanceOutcome.AcquiredPrimary:
        _holdsTheInstance = true;
        MainWindowViewModel = _mainWindowViewModelFactory();

        return BootstrapOutcome.ProceedToWindow;

      case SingleInstanceOutcome.SignaledExistingAndShouldExit:
        return BootstrapOutcome.ExitImmediately;

      default:
        return _never.OfType<BootstrapOutcome>(outcome);
    }
  }

  public void Release()
  {
    _singleInstance.ActivationRequested -= OnActivationRequested;
    _singleInstance.Release();
  }

  private void OnActivationRequested()
  {
    if (!_holdsTheInstance)
    {
      return;
    }

    _dispatchToUserInterface(_bringExistingWindowToFront);
  }
}
