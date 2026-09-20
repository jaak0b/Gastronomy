using CommunityToolkit.Mvvm.Input;
using GastronomyApp.Desktop.Localization;

namespace GastronomyApp.Desktop.ViewModels;

public sealed class QuitConfirmViewModel : ViewModelBase
{
  private readonly Func<CancellationToken, Task> _prepareUpdateOnQuit;
  private readonly Action _requestApplicationExit;
  private readonly Func<CancellationToken, Task> _stopServer;
  private readonly IDesktopTextProvider _text;

  private bool _isConfirmationVisible;

  public QuitConfirmViewModel(Func<CancellationToken, Task> stopServer, IDesktopTextProvider text, Action requestApplicationExit, Func<CancellationToken, Task> prepareUpdateOnQuit)
  {
    _stopServer = stopServer;
    _text = text;
    _requestApplicationExit = requestApplicationExit;
    _prepareUpdateOnQuit = prepareUpdateOnQuit;
    CancelCommand = new RelayCommand(() => OnCloseRequested(false));
    ConfirmCommand = new RelayCommand(() => OnCloseRequested(true));
  }

  public IRelayCommand CancelCommand { get; }

  public IRelayCommand ConfirmCommand { get; }

  public event EventHandler<DialogClosedEventArgs>? CloseRequested;

  private void OnCloseRequested(bool confirmed)
  {
    CloseRequested?.Invoke(this, new(confirmed));
  }

  public string Title => _text.Get("desktop.quit.title");

  public string Body => _text.Get("desktop.quit.body");

  public string ConfirmLabel => _text.Get("desktop.quit.confirm");

  public string CancelLabel => _text.Get("desktop.quit.cancel");

  public bool IsConfirmationVisible
  {
    get => _isConfirmationVisible;
    private set => SetProperty(ref _isConfirmationVisible, value);
  }

  public void RequestQuit()
  {
    IsConfirmationVisible = true;
  }

  public void Cancel()
  {
    IsConfirmationVisible = false;
  }

  public async Task ConfirmAsync(CancellationToken cancellationToken = default)
  {
    if (!IsConfirmationVisible)
      return;

    await _prepareUpdateOnQuit(cancellationToken);
    await _stopServer(cancellationToken);
    IsConfirmationVisible = false;
    _requestApplicationExit();
  }
}
