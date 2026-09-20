namespace GastronomyApp.Desktop.Setup;

public sealed record ElevatedSetupStepReport(bool NetworkAccessSucceeded, bool DataFolderSucceeded)
{
  public bool EveryStepSucceeded => NetworkAccessSucceeded && DataFolderSucceeded;
}
