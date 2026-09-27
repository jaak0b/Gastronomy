using CommunityToolkit.Mvvm.Input;
using GastronomyApp.Desktop.Events;
using GastronomyApp.Desktop.Ports;
using GastronomyApp.Desktop.Values;

namespace GastronomyApp.Desktop.ViewModels;

public sealed class UpdateConfirmViewModel : ViewModelBase
{
  public UpdateConfirmViewModel(string version, IDesktopTextProvider text)
  {
    Title = text.Format("desktop.update.confirmTitle", new TextPlaceholder("version", version));
    ConfirmLabel = text.Get("desktop.update.confirmUpdateNow");
    CancelLabel = text.Get("desktop.update.confirmNotNow");
    CancelCommand = new RelayCommand(() => OnCloseRequested(false));
    ConfirmCommand = new RelayCommand(() => OnCloseRequested(true));
  }

  public string Title { get; }

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
