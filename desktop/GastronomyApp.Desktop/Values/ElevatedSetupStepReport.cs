namespace GastronomyApp.Desktop.Values;

public sealed record ElevatedSetupStepReport(bool NetworkAccessSucceeded, bool DataFolderSucceeded)
{
  public bool EveryStepSucceeded => NetworkAccessSucceeded && DataFolderSucceeded;
}
