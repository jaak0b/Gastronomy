using GastronomyApp.Desktop.Services;

namespace GastronomyApp.Desktop.ViewModels;

public sealed class UpdateConfirmViewModel : ViewModelBase
{
  public UpdateConfirmViewModel(string version, IDesktopTextProvider text)
  {
    Title = text.Get("desktop.update.confirmTitle");
    Body = text.Format("desktop.update.confirmBody", new TextPlaceholder("version", version));
    ConfirmLabel = text.Get("desktop.update.confirmRestart");
    CancelLabel = text.Get("desktop.update.confirmLater");
  }

  public string Title { get; }

  public string Body { get; }

  public string ConfirmLabel { get; }

  public string CancelLabel { get; }
}
