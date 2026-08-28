using GastronomyApp.Desktop.Services;

namespace GastronomyApp.Desktop.ViewModels;

public sealed class QuitConfirmViewModel : ViewModelBase
{
  private readonly Func<CancellationToken, Task> stopServer;
  private readonly IDesktopTextProvider text;
  private readonly Action requestApplicationExit;

  private bool isConfirmationVisible;

  public QuitConfirmViewModel(
      Func<CancellationToken, Task> stopServer,
      IDesktopTextProvider text,
      Action requestApplicationExit)
  {
    this.stopServer = stopServer;
    this.text = text;
    this.requestApplicationExit = requestApplicationExit;
  }

  public string Title => text.Get("desktop.quit.title");

  public string Body => text.Get("desktop.quit.body");

  public string ConfirmLabel => text.Get("desktop.quit.confirm");

  public string CancelLabel => text.Get("desktop.quit.cancel");

  public bool IsConfirmationVisible
  {
    get => isConfirmationVisible;
    private set => SetProperty(ref isConfirmationVisible, value);
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
    {
      return;
    }

    await stopServer(cancellationToken);
    IsConfirmationVisible = false;
    requestApplicationExit();
  }
}
