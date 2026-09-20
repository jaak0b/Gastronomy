namespace GastronomyApp.Desktop.Services;

public sealed record ElevatedSetupStepReport(bool NetworkAccessSucceeded, bool DataFolderSucceeded)
{
  public bool EveryStepSucceeded => NetworkAccessSucceeded && DataFolderSucceeded;
}
