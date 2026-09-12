namespace GastronomyApp.Desktop.ViewModels;

public sealed class TechnicalDetailViewModel : ViewModelBase
{
  public TechnicalDetailViewModel(string title, string detail, string closeLabel)
  {
    Title = title;
    Detail = detail;
    CloseLabel = closeLabel;
  }

  public string Title { get; }

  public string Detail { get; }

  public string CloseLabel { get; }
}
