using CommunityToolkit.Mvvm.Input;

namespace GastronomyApp.Desktop.ViewModels;

public sealed class TechnicalDetailViewModel : ViewModelBase
{
  public TechnicalDetailViewModel(string title, string detail, string closeLabel)
  {
    Title = title;
    Detail = detail;
    CloseLabel = closeLabel;
    CloseCommand = new RelayCommand(OnCloseRequested);
  }

  public string Title { get; }

  public string Detail { get; }

  public string CloseLabel { get; }

  public IRelayCommand CloseCommand { get; }

  public event EventHandler? CloseRequested;

  private void OnCloseRequested()
  {
    CloseRequested?.Invoke(this, EventArgs.Empty);
  }
}
