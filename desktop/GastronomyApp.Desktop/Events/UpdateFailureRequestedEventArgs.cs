namespace GastronomyApp.Desktop.Events;

public sealed class UpdateFailureRequestedEventArgs(string failureDetail) : EventArgs
{
  public string FailureDetail { get; } = failureDetail;
}
