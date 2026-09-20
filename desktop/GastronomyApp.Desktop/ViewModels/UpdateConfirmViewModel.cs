using CommunityToolkit.Mvvm.Input;
using GastronomyApp.Desktop.Localization;

namespace GastronomyApp.Desktop.ViewModels;

public sealed class UpdateConfirmViewModel : ViewModelBase
{
  public UpdateConfirmViewModel(string version, IDesktopTextProvider text)
  {
    Title = text.Get("desktop.update.confirmTitle");
    Body = text.Format("desktop.update.confirmBody", new TextPlaceholder("version", version));
    ConfirmLabel = text.Get("desktop.update.confirmRestart");
    CancelLabel = text.Get("desktop.update.confirmLater");
    CancelCommand = new RelayCommand(() => OnCloseRequested(false));
    ConfirmCommand = new RelayCommand(() => OnCloseRequested(true));
  }

  public string Title { get; }

  public string Body { get; }

  public string ConfirmLabel { get; }

  public string CancelLabel { get; }

  public IRelayCommand CancelCommand { get; }

  public IRelayCommand ConfirmCommand { get; }

  public event EventHandler<DialogClosedEventArgs>? CloseRequested;

  private void OnCloseRequested(bool confirmed)
  {
    CloseRequested?.Invoke(this, new(confirmed));
  }
}
