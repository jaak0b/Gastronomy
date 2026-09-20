namespace GastronomyApp.Desktop.ViewModels;

public sealed class UpdateFailureRequestedEventArgs(string failureDetail) : EventArgs
{
  public string FailureDetail { get; } = failureDetail;
}
